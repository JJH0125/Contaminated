using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace Squad
{
    /// <summary>
    /// Fake 차원의 오염 흔적 후처리 효과의 설정값.
    /// Post Processing 패키지에 없는 효과라 직접 만든 것이다. 실제로 그리는 일은 FakeContaminationRenderer와
    /// Resources/Shaders/FakeContamination.shader가 하고, 물결이 언제 어디서 퍼지는지는 FakeContaminationEffect가 정한다.
    /// 색과 크기 같은 수치는 GameTuning 에셋의 fakeContamination 항목에서 매 프레임 읽는다.
    ///
    /// 화면 색 보정(Fake 차원의 색 빼기, 색조) 전에 그려서, 얼룩도 Fake 차원의 색으로 함께 물든다.
    /// </summary>
    [System.Serializable]
    [PostProcess(typeof(FakeContaminationRenderer), PostProcessEvent.BeforeStack, "Custom/Fake Contamination")]
    public sealed class FakeContamination : PostProcessEffectSettings
    {
        [Range(0f, 1f), Tooltip("효과 전체의 세기. 0이면 그리지 않는다")]
        public FloatParameter presence = new FloatParameter { value = 0f };

        [Tooltip("물결이 시작된 자리")]
        public Vector3Parameter origin = new Vector3Parameter { value = Vector3.zero };

        [Tooltip("물결이 지금까지 퍼진 거리(m)")]
        public FloatParameter radius = new FloatParameter { value = 0f };

        [Range(0f, 1f), Tooltip("물결이 지나간 자리의 오염 정도 (이번 라운드의 Fake 오염)")]
        public FloatParameter amount = new FloatParameter { value = 0f };

        [Range(0f, 1f), Tooltip("물결과 상관없이 어디에나 비치는 오염 정도 (이번 라운드의 Real 오염)")]
        public FloatParameter baseAmount = new FloatParameter { value = 0f };

        [Range(0f, 1f), Tooltip("물결의 세기. Fake에 있으면 1, Real로 돌아오면 0으로 줄어든다")]
        public FloatParameter waveStrength = new FloatParameter { value = 0f };

        // Fake에 있지 않으면 아예 그리지 않아서 화면 처리 비용이 들지 않는다.
        public override bool IsEnabledAndSupported(PostProcessRenderContext context)
        {
            return enabled.value && presence.value > 0f;
        }
    }

    public sealed class FakeContaminationRenderer : PostProcessEffectRenderer<FakeContamination>
    {
        private static readonly int CamPosId = Shader.PropertyToID("_CamPos");
        private static readonly int RayBLId = Shader.PropertyToID("_RayBL");
        private static readonly int RayBRId = Shader.PropertyToID("_RayBR");
        private static readonly int RayTLId = Shader.PropertyToID("_RayTL");
        private static readonly int RayTRId = Shader.PropertyToID("_RayTR");
        private static readonly int AspectId = Shader.PropertyToID("_Aspect");
        private static readonly int PresenceId = Shader.PropertyToID("_Presence");
        private static readonly int OriginId = Shader.PropertyToID("_Origin");
        private static readonly int RadiusId = Shader.PropertyToID("_Radius");
        private static readonly int GrowDistanceId = Shader.PropertyToID("_GrowDistance");
        private static readonly int FrontWidthId = Shader.PropertyToID("_FrontWidth");
        private static readonly int FrontColorId = Shader.PropertyToID("_FrontColor");
        private static readonly int FrontGlowId = Shader.PropertyToID("_FrontGlow");
        private static readonly int FrontFadeId = Shader.PropertyToID("_FrontFade");
        private static readonly int AmountId = Shader.PropertyToID("_Amount");
        private static readonly int BaseAmountId = Shader.PropertyToID("_BaseAmount");
        private static readonly int WaveStrengthId = Shader.PropertyToID("_WaveStrength");
        private static readonly int SpreadId = Shader.PropertyToID("_Spread");
        private static readonly int StainSizeId = Shader.PropertyToID("_StainSize");
        private static readonly int StainColorId = Shader.PropertyToID("_StainColor");
        private static readonly int StainStrengthId = Shader.PropertyToID("_StainStrength");
        private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
        private static readonly int RimGlowId = Shader.PropertyToID("_RimGlow");
        private static readonly int EffectTimeId = Shader.PropertyToID("_EffectTime");
        private static readonly int FogInfoId = Shader.PropertyToID("_FogInfo");
        private static readonly int ClearCylindersId = Shader.PropertyToID("_ClearCylinders");
        private static readonly int ClearHeightsId = Shader.PropertyToID("_ClearHeights");
        private static readonly int ClearCountId = Shader.PropertyToID("_ClearCount");

        // 픽셀마다 월드 위치를 되살리려면 카메라의 깊이 텍스처가 있어야 한다.
        public override DepthTextureMode GetCameraFlags() => DepthTextureMode.Depth;

        public override void Render(PostProcessRenderContext context)
        {
            FakeContaminationTuning tuning = GameTuning.Instance.fakeContamination;
            PropertySheet sheet = context.propertySheets.Get(Shader.Find("Hidden/Custom/FakeContamination"));
            MaterialPropertyBlock props = sheet.properties;

            // 화면 네 귀퉁이로 향하는 방향. 카메라 앞쪽 성분을 1로 맞춰 두면, 깊이(m)를 곱하는 것만으로 월드 위치가 된다.
            Camera cam = context.camera;
            Transform view = cam.transform;
            float tanY = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            Vector3 up = view.up * tanY;
            Vector3 right = view.right * tanY * cam.aspect;
            Vector3 forward = view.forward;
            props.SetVector(CamPosId, view.position);
            props.SetVector(RayBLId, forward - right - up);
            props.SetVector(RayBRId, forward + right - up);
            props.SetVector(RayTLId, forward - right + up);
            props.SetVector(RayTRId, forward + right + up);
            props.SetFloat(AspectId, cam.aspect);

            props.SetFloat(PresenceId, settings.presence);
            props.SetVector(OriginId, settings.origin.value);
            props.SetFloat(RadiusId, settings.radius);
            props.SetFloat(GrowDistanceId, tuning.growDistance);
            props.SetFloat(FrontWidthId, tuning.frontWidth);
            props.SetColor(FrontColorId, tuning.frontColor);
            props.SetFloat(FrontGlowId, tuning.frontGlow);
            props.SetFloat(FrontFadeId, tuning.frontFadeDistance);
            props.SetFloat(AmountId, settings.amount);
            props.SetFloat(BaseAmountId, settings.baseAmount);
            props.SetFloat(WaveStrengthId, settings.waveStrength);
            props.SetFloat(SpreadId, tuning.spread);
            props.SetFloat(StainSizeId, tuning.stainSize);
            props.SetColor(StainColorId, tuning.stainColor);
            props.SetFloat(StainStrengthId, tuning.stainStrength);
            props.SetColor(RimColorId, tuning.rimColor);
            props.SetFloat(RimGlowId, tuning.rimGlow);
            props.SetFloat(EffectTimeId, Time.time);
            props.SetVector(FogInfoId, FogInfo());

            props.SetVectorArray(ClearCylindersId, FakeContaminationEffect.ClearCylinders);
            props.SetFloatArray(ClearHeightsId, FakeContaminationEffect.ClearHeights);
            props.SetFloat(ClearCountId, FakeContaminationEffect.ClearCount);

            context.command.BlitFullscreenTriangle(context.source, context.destination, sheet, 0);
        }

        // 씬의 안개 설정 (방식, 짙기, 시작, 끝). 멀리 있는 얼룩이 안개 속에서 튀어 보이지 않게 함께 흐리게 한다.
        private static Vector4 FogInfo()
        {
            if (!RenderSettings.fog)
                return Vector4.zero;

            float mode = RenderSettings.fogMode switch
            {
                FogMode.Linear => 1f,
                FogMode.Exponential => 2f,
                _ => 3f,
            };
            return new Vector4(mode, RenderSettings.fogDensity, RenderSettings.fogStartDistance, RenderSettings.fogEndDistance);
        }
    }
}
