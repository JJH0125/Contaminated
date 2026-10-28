using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Squad
{
    /// <summary>
    /// 카메라와 플레이어 사이에서 플레이어를 가리는 나무와 풀에, 플레이어 주변만 동그랗게 창을 뚫는다.
    /// 가리지 않게 되면 창이 서서히 닫혀 원래대로 돌아온다.
    ///
    /// 가리는지 판단하는 방법:
    ///   카메라에서 플레이어 몸의 여러 높이(발, 가운데, 머리)로 직선을 긋고,
    ///   그 직선이 나무나 풀의 크기 상자(Renderer.bounds)를 지나가면 가린다고 본다.
    ///   콜라이더가 없는 풀도 판단할 수 있도록 물리 검사(Raycast) 대신 크기 상자를 쓴다.
    ///
    /// 창을 뚫는 방법:
    ///   가리는 오브젝트에만 FoliageCutout 셰이더를 쓰는 재질을 잠깐 끼운다. 셰이더는 화면에서 플레이어 주변의 원 안에 있고
    ///   플레이어보다 카메라에 가까운 픽셀만 지운다. 그래서 나무의 나머지와 플레이어 뒤의 풀은 그대로 보인다.
    ///   (예전에는 오브젝트 전체를 점 무늬로 반투명하게 했는데, 적이 가까울 때의 노이즈 효과와 헷갈려서 바꿨다)
    ///   원의 위치와 크기는 이 스크립트가 매 프레임 셰이더의 전역 값으로 넣어 준다.
    ///   원래대로 돌아오면 원래 재질을 다시 끼운다. 재질 파일 자체는 바꾸지 않는다.
    ///
    /// 대상은 씬의 "Forest Props"(안쪽 숲)와 "Border Forest"(경계 바깥의 숲) 아래에 있는
    /// 나무와 풀(ForestMapTool이 만든 것)이다. 숲은 씬이 시작될 때 한 번 모은다.
    /// 라운드마다 새로 생기는 덤불(구역 벽, 모닥불 고리)은 만드는 쪽이 Register로 따로 알려 준다.
    ///
    /// 씬 구성:
    ///   붙일 필요가 없다. 씬이 열릴 때마다 Main Camera에 스스로 붙는다.
    /// </summary>
    public class FoliageFader : MonoBehaviour
    {
        // 수치는 GameTuning 에셋(Resources/GameTuning)에서 읽는다.
        private float cutoutRadius => GameTuning.Instance.foliage.cutoutRadius;
        private float edgeWidth => GameTuning.Instance.foliage.edgeWidth;
        private float fadeTime => GameTuning.Instance.foliage.fadeTime;
        private float sightRadius => GameTuning.Instance.foliage.sightRadius;

        // 대상 나무와 풀을 모아 둔 오브젝트의 이름 (안쪽 숲, 경계 바깥의 보기만 하는 숲)
        private static readonly string[] ForestRootNames = { "Forest Props", "Border Forest" };
        private const string FadeShaderName = "Hidden/Custom/FoliageCutout";
        // 플레이어 몸에서 시선을 그을 높이 (몸 높이에 대한 비율: 발, 가운데, 머리)
        private static readonly float[] SightHeights = { 0.1f, 0.5f, 0.9f };

        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int CutoutCenterId = Shader.PropertyToID("_CutoutCenter");
        private static readonly int CutoutRadiusId = Shader.PropertyToID("_CutoutRadius");
        private static readonly int CutoutDepthId = Shader.PropertyToID("_CutoutDepth");
        private static readonly int CutoutEdgeId = Shader.PropertyToID("_CutoutEdge");

        // 창이 뚫린(또는 닫히는 중인) 오브젝트 하나의 상태
        private class Faded
        {
            public Material[] Originals;
            // 1: 창이 닫힘(그대로), 0: 창이 다 열림
            public float Closed = 1f;
        }

        private Renderer[] _foliage = new Renderer[0];
        // 라운드 중에 생긴 덤불. Stage와 함께 사라지면 목록에서 저절로 빠진다.
        private static readonly List<Renderer> _registered = new();

        /// <summary>라운드 중에 만든 덤불도 플레이어를 가리면 창을 뚫도록 알려 준다.</summary>
        public static void Register(GameObject root)
        {
            if (root != null)
                _registered.AddRange(root.GetComponentsInChildren<Renderer>(true));
        }
        private readonly Dictionary<Renderer, Faded> _faded = new();
        // 원래 재질마다 만들어 둔 창 뚫기용 재질
        private readonly Dictionary<Material, Material> _fadeMaterials = new();
        private readonly List<Renderer> _finished = new();
        private readonly Vector3[] _sightTargets = new Vector3[3];
        private MaterialPropertyBlock _block;
        private Shader _fadeShader;
        private Camera _camera;

        // 씬이 열릴 때마다 Main Camera에 이 효과를 붙인다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            AttachToMainCamera();
            SceneManager.sceneLoaded += (_, _) => AttachToMainCamera();
        }

        private static void AttachToMainCamera()
        {
            Camera cam = Camera.main;
            if (cam == null || cam.GetComponent<FoliageFader>() != null)
                return;

            cam.gameObject.AddComponent<FoliageFader>();
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _block = new MaterialPropertyBlock();
            _fadeShader = Shader.Find(FadeShaderName);

            // 모닥불 주변처럼 꺼져 있는 나무도 나중에 켜질 수 있으므로 꺼진 것까지 모아 둔다.
            var foliage = new List<Renderer>();
            foreach (string rootName in ForestRootNames)
            {
                GameObject root = GameObject.Find(rootName);
                if (root != null)
                    foliage.AddRange(root.GetComponentsInChildren<Renderer>(true));
            }
            _foliage = foliage.ToArray();
        }

        // 플레이어를 가리기 시작한 덤불이면 창을 뚫기 시작한다.
        private void CheckBlocking(Renderer foliage, Vector3 eye, int targetCount)
        {
            if (foliage == null || !foliage.enabled || !foliage.gameObject.activeInHierarchy)
                return;

            bool blocking = targetCount > 0 && BlocksSight(foliage.bounds, eye, targetCount);
            if (blocking && !_faded.ContainsKey(foliage))
                StartFading(foliage);
        }

        private void LateUpdate()
        {
            _registered.RemoveAll(part => part == null);
            if (_fadeShader == null || (_foliage.Length == 0 && _registered.Count == 0))
                return;

            int targetCount = FindSightTargets();
            UpdateCutoutWindow(targetCount);
            Vector3 eye = _camera.transform.position;
            float step = fadeTime > 0f ? Time.deltaTime / fadeTime : 1f;

            foreach (Renderer foliage in _foliage)
                CheckBlocking(foliage, eye, targetCount);
            foreach (Renderer foliage in _registered)
                CheckBlocking(foliage, eye, targetCount);

            // 창이 뚫린 것들의 열린 정도를 목표로 옮기고, 다 닫힌 것은 원래 재질로 되돌린다.
            _finished.Clear();
            foreach (KeyValuePair<Renderer, Faded> pair in _faded)
            {
                Renderer foliage = pair.Key;
                if (foliage == null)
                {
                    _finished.Add(foliage);
                    continue;
                }

                bool blocking = targetCount > 0 && foliage.gameObject.activeInHierarchy &&
                                BlocksSight(foliage.bounds, eye, targetCount);
                float target = blocking ? 0f : 1f;
                Faded state = pair.Value;
                state.Closed = Mathf.MoveTowards(state.Closed, target, step);

                if (!blocking && state.Closed >= 1f)
                {
                    foliage.sharedMaterials = state.Originals;
                    foliage.SetPropertyBlock(null);
                    _finished.Add(foliage);
                    continue;
                }

                foliage.GetPropertyBlock(_block);
                _block.SetFloat(FadeId, state.Closed);
                foliage.SetPropertyBlock(_block);
            }

            foreach (Renderer foliage in _finished)
                _faded.Remove(foliage);
        }

        // 시선을 그을 플레이어 몸의 지점들을 _sightTargets에 채우고 그 수를 돌려준다. 플레이어가 없으면 0.
        private int FindSightTargets()
        {
            DimensionController controller = DimensionController.Instance;
            Player player = controller != null ? controller.player : null;
            if (player == null)
                return 0;

            // 모델의 크기 상자로 몸의 높이를 잰다. 모델이 없으면 플레이어 위치 한 곳만 본다.
            SkinnedMeshRenderer body = player.GetComponentInChildren<SkinnedMeshRenderer>();
            if (body == null)
            {
                _sightTargets[0] = player.transform.position;
                return 1;
            }

            Bounds bounds = body.bounds;
            for (int i = 0; i < SightHeights.Length; i++)
                _sightTargets[i] = new Vector3(bounds.center.x,
                    Mathf.Lerp(bounds.min.y, bounds.max.y, SightHeights[i]), bounds.center.z);
            return SightHeights.Length;
        }

        /// <summary>
        /// 셰이더가 창을 뚫을 자리를 전역 값으로 넣는다. 화면에서 플레이어 몸 한가운데를 중심으로,
        /// cutoutRadius(m)를 그 깊이에서 화면 높이에 대한 비율로 바꿔 넣는다. 그래서 카메라가 멀어져도
        /// 창은 플레이어에 비해 늘 같은 크기로 보인다. 플레이어가 없으면 창을 닫는다.
        /// </summary>
        private void UpdateCutoutWindow(int targetCount)
        {
            if (targetCount == 0)
            {
                Shader.SetGlobalFloat(CutoutRadiusId, 0f);
                return;
            }

            // 시선을 긋는 높이 중 가운데가 몸 한가운데다.
            Vector3 center = _sightTargets[targetCount > 1 ? 1 : 0];
            Vector3 viewport = _camera.WorldToViewportPoint(center);
            float depth = Mathf.Max(viewport.z, 0.01f);
            // 이 깊이에서 화면 높이 하나가 몇 미터인가
            float screenHeight = 2f * depth * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);

            Shader.SetGlobalVector(CutoutCenterId, new Vector4(viewport.x, viewport.y, 0f, 0f));
            Shader.SetGlobalFloat(CutoutRadiusId, cutoutRadius / screenHeight);
            Shader.SetGlobalFloat(CutoutEdgeId, edgeWidth / screenHeight);
            Shader.SetGlobalFloat(CutoutDepthId, depth);
        }

        // 카메라에서 플레이어 몸의 지점들로 그은 시선 중 하나라도 이 크기 상자를 지나가는가.
        private bool BlocksSight(Bounds bounds, Vector3 eye, int targetCount)
        {
            bounds.Expand(sightRadius * 2f);
            for (int i = 0; i < targetCount; i++)
            {
                Vector3 toTarget = _sightTargets[i] - eye;
                float length = toTarget.magnitude;
                // 상자가 플레이어보다 뒤에 있으면 가리지 않는다.
                if (bounds.IntersectRay(new Ray(eye, toTarget), out float distance) && distance < length)
                    return true;
            }

            return false;
        }

        private void StartFading(Renderer foliage)
        {
            Material[] originals = foliage.sharedMaterials;
            var fadeMaterials = new Material[originals.Length];
            for (int i = 0; i < originals.Length; i++)
                fadeMaterials[i] = FadeMaterialFor(originals[i]);

            foliage.sharedMaterials = fadeMaterials;
            _faded.Add(foliage, new Faded { Originals = originals });
        }

        // 원래 재질과 색, 텍스처, 오려 내는 기준이 같고 셰이더만 FoliageCutout인 재질. 원래 재질마다 하나씩 만든다.
        private Material FadeMaterialFor(Material original)
        {
            if (original == null)
                return null;

            if (!_fadeMaterials.TryGetValue(original, out Material fade))
            {
                fade = new Material(original) { shader = _fadeShader, name = original.name + " (Fade)" };
                _fadeMaterials.Add(original, fade);
            }

            return fade;
        }

        // 꺼질 때 창을 뚫어 둔 것들을 모두 원래대로 돌려놓는다.
        private void OnDisable()
        {
            foreach (KeyValuePair<Renderer, Faded> pair in _faded)
            {
                if (pair.Key == null)
                    continue;

                pair.Key.sharedMaterials = pair.Value.Originals;
                pair.Key.SetPropertyBlock(null);
            }

            _faded.Clear();
        }
    }
}
