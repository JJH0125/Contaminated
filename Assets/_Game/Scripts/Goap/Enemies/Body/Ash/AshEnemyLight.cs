using UnityEngine;

namespace Squad
{
    /// <summary>
    /// Fake 적의 검은 형체(Custom/Ash Enemy, AshCrystal 셰이더)에 횃불의 자리와 방향, 수치를 매 프레임 넘겨준다.
    /// 화면에 보이는 빛은 사방으로 퍼지는 점 조명 그대로이고, 무늬가 드러나는 범위만 앞쪽 원뿔로 잰다.
    /// 이 값은 모습에만 쓴다. 적이 플레이어를 보는 판정(VisionSensor)과는 상관없다.
    ///
    /// 씬이나 프리팹에 붙일 필요가 없다. 게임이 시작될 때 스스로 만들어진다.
    /// </summary>
    public class AshEnemyLight : MonoBehaviour
    {
        // 플레이어 오브젝트 위치에서 횃불까지의 높이(m)
        private const float TorchHeight = 3f;
        // 무늬가 흐려지는 경계의 폭(도)
        private const float SoftAngle = 10f;

        /// 잡힐 때의 연출처럼, 무늬를 몸 전체에 드러내야 할 때 켠다. (Jumpscare)
        public static bool FullReveal { get; set; }

        private static readonly int TorchPosId = Shader.PropertyToID("_AshTorchPos");
        private static readonly int TorchForwardId = Shader.PropertyToID("_AshTorchForward");
        private static readonly int TorchOnId = Shader.PropertyToID("_AshTorchOn");
        private static readonly int FullRevealId = Shader.PropertyToID("_AshFullReveal");
        private static readonly int RevealCosId = Shader.PropertyToID("_AshRevealCos");
        private static readonly int RevealSoftCosId = Shader.PropertyToID("_AshRevealSoftCos");
        private static readonly int RevealDistanceId = Shader.PropertyToID("_AshRevealDistance");
        private static readonly int NearDistanceId = Shader.PropertyToID("_AshNearDistance");
        private static readonly int BaseRimId = Shader.PropertyToID("_AshBaseRim");
        private static readonly int RimPowerId = Shader.PropertyToID("_AshRimPower");
        private static readonly int PatternScaleId = Shader.PropertyToID("_AshPatternScale");
        private static readonly int BodyColorId = Shader.PropertyToID("_AshBodyColor");
        private static readonly int RimColorId = Shader.PropertyToID("_AshRimColor");
        private static readonly int PatternColorId = Shader.PropertyToID("_AshPatternColor");
        private static readonly int WobbleId = Shader.PropertyToID("_AshWobble");
        private static readonly int CrystalGlowId = Shader.PropertyToID("_AshCrystalGlow");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            FullReveal = false;
            var lightObject = new GameObject("Ash Enemy Light");
            lightObject.AddComponent<AshEnemyLight>();
            DontDestroyOnLoad(lightObject);
        }

        private void Update()
        {
            AshEnemyTuning tuning = GameTuning.Instance.ashEnemy;
            float halfAngle = tuning.revealAngle * 0.5f;
            Shader.SetGlobalFloat(RevealCosId, Mathf.Cos(halfAngle * Mathf.Deg2Rad));
            Shader.SetGlobalFloat(RevealSoftCosId, Mathf.Cos((halfAngle + SoftAngle) * Mathf.Deg2Rad));
            Shader.SetGlobalFloat(RevealDistanceId, tuning.revealDistance);
            Shader.SetGlobalFloat(NearDistanceId, tuning.nearDistance);
            Shader.SetGlobalFloat(BaseRimId, tuning.baseRim);
            Shader.SetGlobalFloat(RimPowerId, tuning.rimPower);
            Shader.SetGlobalFloat(PatternScaleId, tuning.patternScale);
            Shader.SetGlobalColor(BodyColorId, tuning.bodyColor);
            Shader.SetGlobalColor(RimColorId, tuning.rimColor);
            Shader.SetGlobalColor(PatternColorId, tuning.patternColor);
            Shader.SetGlobalFloat(WobbleId, tuning.wobble);
            Shader.SetGlobalFloat(CrystalGlowId, tuning.crystalGlow);
            Shader.SetGlobalFloat(FullRevealId, FullReveal ? 1f : 0f);

            Player player = DimensionController.Instance != null ? DimensionController.Instance.player : null;
            if (player == null)
                return;

            // 1인칭에서는 시선을 따라가고, 쿼터뷰에서는 몸이 향하는 쪽을 비춘다.
            Vector3 forward = FirstPersonView.ControlsFirstPerson
                ? FirstPersonView.ViewRotation * Vector3.forward
                : player.transform.forward;
            Shader.SetGlobalVector(TorchPosId, player.transform.position + Vector3.up * TorchHeight);
            Shader.SetGlobalVector(TorchForwardId, forward);
            Shader.SetGlobalFloat(TorchOnId, TorchHolder.IsHiding ? 0f : 1f);
        }
    }
}
