using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 라운드가 오를수록 플레이어의 보호복이 오염되어 가게 한다.
    /// 첫 라운드에는 깨끗하고, 마지막 라운드에는 정해 둔 만큼 오염된다. 그 사이는 고르게 나눈다.
    ///
    /// 실제 색을 섞는 일은 "Custom/Contaminated Suit" 셰이더가 하고, 이 스크립트는 오염 정도만 정해 준다.
    /// Stage는 라운드마다 새로 만들어지므로, 시작할 때 한 번 지금 라운드를 읽어서 정하면 된다.
    ///
    /// 오염 정도는 재질 자체를 바꾸지 않고 이 플레이어의 Renderer에만 따로 넣는다(MaterialPropertyBlock).
    /// 재질 파일을 바꾸면 플레이를 끈 뒤에도 그 값이 재질에 남기 때문이다.
    ///
    /// 씬 구성:
    ///   Player가 붙은 오브젝트에 붙이고, suitMaterial에 Contaminated Suit 셰이더를 쓰는 재질을 연결한다.
    ///   모델은 Player의 자식으로 두며, 꺼져 있는 모델(예전 모델 등)은 건드리지 않는다.
    /// </summary>
    public class SuitContamination : MonoBehaviour
    {
        [Header("■ 필수 연결 — 비워두면 에러")]
        [Tooltip("Contaminated Suit 셰이더를 쓰는 재질")]
        [SerializeField] private Material suitMaterial;

        // 오염 정도는 GameTuning 에셋의 suit 항목에서 읽는다.
        private float firstRoundContamination => GameTuning.Instance.suit.firstRoundContamination;
        private float lastRoundContamination => GameTuning.Instance.suit.lastRoundContamination;

        private static readonly int ContaminationId = Shader.PropertyToID("_Contamination");

        // GameManager가 없을 때(테스트 씬 등) 쓰는 전체 라운드 수
        private const int FallbackTotalRounds = 5;

        /// <summary>전체 라운드 수. GameManager가 없으면 5로 본다.</summary>
        public static int TotalRounds =>
            GameManager.Instance != null ? GameManager.Instance.TotalRounds : FallbackTotalRounds;

        private void Start()
        {
            int round = GameManager.Instance != null ? GameManager.Instance.CurrentRound : 1;
            ApplyTo(gameObject, ContaminationForRound(round));
        }

        /// <summary>
        /// 그 라운드의 오염 정도. 첫 라운드를 0, 마지막 라운드를 1로 본 진행도에 맞춰 나눈다.
        /// </summary>
        public float ContaminationForRound(int round)
        {
            float progress = TotalRounds > 1 ? (round - 1f) / (TotalRounds - 1f) : 0f;
            return Mathf.Lerp(firstRoundContamination, lastRoundContamination, Mathf.Clamp01(progress));
        }

        /// <summary>
        /// target 아래의 켜져 있는 모델에 보호복 재질과 오염 정도를 입힌다. (꺼 둔 예전 모델은 그대로 둔다)
        /// </summary>
        public void ApplyTo(GameObject target, float contamination)
        {
            if (suitMaterial == null)
                return;

            var block = new MaterialPropertyBlock();
            block.SetFloat(ContaminationId, contamination);

            foreach (SkinnedMeshRenderer suit in target.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                suit.sharedMaterial = suitMaterial;
                suit.SetPropertyBlock(block);
            }
        }
    }
}
