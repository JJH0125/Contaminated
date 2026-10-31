using System.Collections.Generic;
using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 출구를 막은 오염 덤불 아치. 시작 자리 바로 뒤에 있어, 첫 화면에서 목표를 글 없이 보여 준다.
    ///
    ///   앞면   검은 오염 덤불. 두 차원 모두에서 막는다. 가운데가 높아 아치처럼 보인다
    ///   양옆   큰 나무 벽. 맵 가장자리까지 이어져 아치를 돌아갈 수 없다 (StageLayout이 세운다)
    ///   틈새   덤불 뒤의 빛. 불을 켤수록 밝아진다
    ///   말뚝   조사팀이 남긴 표지
    ///
    /// 모닥불을 켤 때마다 덤불 일부가 타서 듬성해지고(SetProgress), 정해 둔 수를 다 켜면 남은 덤불이 타서 열린다.
    /// 그 전까지는 틈이 생기지 않을 만큼만 태우고, 몸과 길은 계속 막는다.
    ///
    /// 칸 수를 고정하지 않고 "켠 수 / 켜야 하는 수"의 비율로 태우므로, 라운드마다 모닥불 수가 달라도 맞는다.
    ///
    /// 씬 구성:
    ///   붙일 필요가 없다. StageLayout이 라운드를 시작할 때 만든다.
    /// </summary>
    public class ExitArch : MonoBehaviour
    {
        // 모두 켜기 전까지 태울 수 있는 덤불의 비율. 이보다 많이 타면 틈이 뚫려 보인다.
        private const float MaxThinning = 0.6f;
        // 플레이어가 이 거리(m) 안에 오면 안내 문구를 띄운다.
        private const float PromptDistance = 10f;

        /// 이번 라운드의 아치. GameManager가 모닥불 수가 바뀔 때 알려 준다.
        public static ExitArch Current { get; private set; }

        private DimensionObstacle _obstacle;
        private List<GameObject> _bushes;
        private int _total;
        private Light _glow;
        private Vector3 _front;
        private bool _open;
        private bool _promptShown;
        private string _prompt;

        /// <summary>
        /// from에서 to까지 앞면을 세운다. behind는 덤불 뒤(출구 쪽)에서 빛이 날 자리다.
        /// </summary>
        public static ExitArch Build(Transform parent, Vector3 from, Vector3 to, Vector3 behind, string prompt)
        {
            MapTuning tuning = GameTuning.Instance.map;
            DimensionObstacle obstacle = BarrierBuilder.BuildExitFront(parent, from, to, out List<GameObject> bushes);

            ExitArch arch = obstacle.gameObject.AddComponent<ExitArch>();
            arch._obstacle = obstacle;
            arch._bushes = bushes;
            arch._total = bushes.Count;
            arch._front = Vector3.Lerp(from, to, 0.5f);
            arch._prompt = prompt;

            // 아무도 켜지 않아도 틈새로 빛이 조금 새어 나온다. 나중에 탈 덤불이 빛을 가리지 않게 따로 둔다.
            var light = new GameObject("Exit Glow").AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.position = BarrierBuilder.Ground(behind) + Vector3.up * 3f;
            light.type = LightType.Point;
            light.color = tuning.exitGlowColor;
            light.range = tuning.exitGlowRange;
            light.intensity = tuning.exitGlowMin;
            arch._glow = light;

            arch.PlaceStakes(from, to, parent);
            Current = arch;
            return arch;
        }

        /// <summary>켠 수에 맞춰 덤불을 태우고 빛을 키운다. 다 켰으면 열린다.</summary>
        public void SetProgress(int lit, int required)
        {
            if (_open)
                return;

            MapTuning tuning = GameTuning.Instance.map;
            bool complete = lit >= required;
            float progress = required > 0 ? Mathf.Clamp01(lit / (float)required) : 1f;
            if (_glow != null)
                _glow.intensity = Mathf.Lerp(tuning.exitGlowMin, tuning.exitGlowMax, progress);

            if (complete)
            {
                _open = true;
                _obstacle.Open();
                SetPrompt(false);
            }

            // 남겨 둘 덤불 수. 아직 다 켜지 않았으면 비율만큼만 태운다.
            int keep = complete ? 0 : Mathf.CeilToInt(_total * (1f - progress * MaxThinning));
            while (_bushes.Count > keep)
            {
                int index = Random.Range(0, _bushes.Count);
                BurnAway.Play(_bushes[index], tuning.burnTime);
                _bushes.RemoveAt(index);
            }
        }

        private void Update()
        {
            DimensionController controller = DimensionController.Instance;
            if (_open || controller == null || controller.player == null)
                return;

            Vector3 offset = controller.player.transform.position - _front;
            offset.y = 0f;
            SetPrompt(offset.sqrMagnitude <= PromptDistance * PromptDistance);
        }

        private void OnDestroy()
        {
            SetPrompt(false);
            if (Current == this)
                Current = null;
        }

        private void SetPrompt(bool show)
        {
            if (show == _promptShown || InteractionPrompt.Instance == null)
                return;

            _promptShown = show;
            if (show)
                InteractionPrompt.Instance.Show(this, _prompt);
            else
                InteractionPrompt.Instance.Hide(this);
        }

        // 아치 양쪽 앞에 조사팀의 표지 말뚝을 세운다.
        private void PlaceStakes(Vector3 from, Vector3 to, Transform parent)
        {
            Vector3 along = (to - from).normalized;
            Vector3 outward = Vector3.Cross(along, Vector3.up);
            // 말뚝은 플레이어 쪽(behind의 반대)에 선다.
            if (Vector3.Dot(outward, _glow.transform.position - _front) > 0f)
                outward = -outward;

            Stake(parent, from + along * 3f + outward * 4f, along);
            Stake(parent, to - along * 3f + outward * 4f, along);
        }

        private static void Stake(Transform parent, Vector3 spot, Vector3 along)
        {
            Vector3 ground = BarrierBuilder.Ground(spot);
            Quaternion rotation = Quaternion.LookRotation(Vector3.Cross(along, Vector3.up), Vector3.up)
                                  * Quaternion.Euler(Random.Range(-6f, 6f), 0f, Random.Range(-6f, 6f));

            Part(parent, "Stake Post", ground + rotation * (Vector3.up * 2f), rotation,
                 new Vector3(0.3f, 4f, 0.3f), new Color(0.2f, 0.15f, 0.1f));
            Part(parent, "Stake Sign", ground + rotation * (Vector3.up * 3.4f), rotation,
                 new Vector3(2.4f, 1.2f, 0.12f), new Color(0.75f, 0.55f, 0.1f));
        }

        private static void Part(Transform parent, string name, Vector3 position, Quaternion rotation, Vector3 size, Color color)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.SetPositionAndRotation(position, rotation);
            part.transform.localScale = size;

            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", color);
            part.GetComponent<Renderer>().SetPropertyBlock(block);
        }
    }
}
