using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;

namespace Squad
{
    /// <summary>
    /// 횃불을 숨기고 있는 동안, 온기 게이지(TorchHolder.Warmth)에 따라 화면 가장자리에 오염을 끼운다.
    /// 불 = 정화. 불이 없으면 오염이 스며들고, 불을 다시 들면 오염이 타서 걷힌다.
    ///   숨기기 시작  화면 색이 살짝 차가운 톤으로 기운다
    ///   100~50%      바이저에 김이 서린다. 가장자리부터 뿌옇게 흐려지고 물방울이 맺힌다 (마스크 호흡과 짝)
    ///   50% 아래     김서림 위로 오염이 결정처럼 가지를 치며 자라 들어온다. 회색 결정에 바이저와 같은 검은 핏줄
    ///   0% 직전      결정이 화면의 3분의 1 가까이 덮는다
    ///                (예전에는 이때 가지 끝이 꿈틀거렸지만, 이글거려 보여서 뺐다. 셰이더의 _Growth는 0으로 둔다)
    ///   횃불을 들면  게이지가 다 차기를 기다리지 않고 오염이 타서 걷힌다. 물러나는 경계가 주황빛으로 그을리고,
    ///                색 온도도 따뜻하게 돌아온다
    ///
    /// 화면 효과 자체는 ContaminationEdge(직접 만든 후처리 효과)가 그린다. 이 스크립트는 CctvNoiseEffect처럼
    /// 시작할 때 Volume을 코드로 하나 만들고, 그 효과의 세기만 조절한다.
    ///
    /// 씬 구성:
    ///   붙일 필요가 없다. 씬이 열릴 때마다 Main Camera(PostProcessLayer가 있는 카메라)에 스스로 붙는다.
    /// </summary>
    [RequireComponent(typeof(PostProcessLayer))]
    public class ContaminationEdgeEffect : MonoBehaviour
    {
        // 수치는 GameTuning 에셋(Resources/GameTuning)에서 읽는다.
        private float chillTime => GameTuning.Instance.contaminationEdge.chillTime;
        private float growSpeed => GameTuning.Instance.contaminationEdge.growSpeed;
        private float burnAwaySpeed => GameTuning.Instance.contaminationEdge.burnAwaySpeed;

        // PostProcessLayer에 Volume 레이어가 지정되어 있지 않을 때 대신 쓸 레이어 (Unity 기본 레이어인 TransparentFX)
        private const int FallbackVolumeLayer = 1;
        // 오염 결정이 자라기 시작하는 게이지 값. 김서림은 이 값까지 짙어진다.
        private const float CrystalStart = 0.5f;

        private PostProcessVolume _volume;
        private ContaminationEdge _edge;

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
            if (cam == null || cam.GetComponent<PostProcessLayer>() == null || cam.GetComponent<ContaminationEdgeEffect>() != null)
                return;

            cam.gameObject.AddComponent<ContaminationEdgeEffect>();
        }

        private void Awake()
        {
            PostProcessLayer layer = GetComponent<PostProcessLayer>();
            if (layer.volumeLayer.value == 0)
                layer.volumeLayer = 1 << FallbackVolumeLayer;

            _edge = ScriptableObject.CreateInstance<ContaminationEdge>();
            _edge.enabled.Override(true);
            _edge.chill.Override(0f);
            _edge.fog.Override(0f);
            _edge.coverage.Override(0f);
            _edge.growth.Override(0f);
            _edge.burn.Override(0f);

            // 두 효과의 그리는 순서는 카메라의 PostProcessLayer(Custom Effect Sorting)에서 정한다.
            _volume = PostProcessManager.instance.QuickVolume(LowestLayerIn(layer.volumeLayer), 100.5f, _edge);
        }

        private void Update()
        {
            float warmth = TorchHolder.Warmth;
            bool hiding = TorchHolder.IsHiding;
            float dt = Time.unscaledDeltaTime;

            // 숨기는 동안의 목표. 들고 있으면 모두 0으로 돌아간다.
            float targetChill = hiding ? 1f : 0f;
            // 김서림은 게이지가 절반이 될 때까지 짙어지고, 그 아래에서는 짙은 채로 남는다.
            float targetFog = hiding ? Mathf.Clamp01((1f - warmth) / (1f - CrystalStart)) : 0f;
            // 오염 결정은 게이지 절반 아래에서 자라기 시작한다.
            float targetCoverage = hiding ? Mathf.Clamp01((CrystalStart - warmth) / CrystalStart) : 0f;

            _edge.chill.value = Mathf.MoveTowards(_edge.chill.value, targetChill,
                (hiding ? 1f / Mathf.Max(chillTime, 0.01f) : burnAwaySpeed) * dt);
            _edge.fog.value = Mathf.MoveTowards(_edge.fog.value, targetFog, (hiding ? 1f : burnAwaySpeed) * dt);

            float coverage = _edge.coverage.value;
            bool burning = targetCoverage < coverage;
            _edge.coverage.value = Mathf.MoveTowards(coverage, targetCoverage, (burning ? burnAwaySpeed : growSpeed) * dt);

            // 오염이 타서 걷히는 동안에만 경계가 그을린다.
            float targetBurn = burning && !hiding && _edge.coverage.value > 0.01f ? 1f : 0f;
            _edge.burn.value = Mathf.MoveTowards(_edge.burn.value, targetBurn, 4f * dt);
        }

        // 코드로 만든 Volume은 씬과 함께 저절로 정리되지 않으므로 직접 지운다.
        private void OnDestroy()
        {
            if (_volume != null)
                RuntimeUtilities.DestroyVolume(_volume, true, true);
        }

        // mask에 켜져 있는 레이어 중 번호가 가장 작은 것
        private static int LowestLayerIn(LayerMask mask)
        {
            for (int i = 0; i < 32; i++)
                if ((mask.value & (1 << i)) != 0)
                    return i;

            return FallbackVolumeLayer;
        }
    }
}
