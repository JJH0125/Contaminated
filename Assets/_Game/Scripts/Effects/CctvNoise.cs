using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace Squad
{
    /// <summary>
    /// 감시카메라 노이즈 후처리 효과의 설정값.
    /// Post Processing 패키지에 없는 효과라 직접 만든 것이다. 실제로 그리는 일은 CctvNoiseRenderer와
    /// Resources/Shaders/CctvNoise.shader가 하고, 세기는 CctvNoiseEffect가 적과의 거리로 정한다.
    /// </summary>
    // System 네임스페이스를 통째로 쓰면 [Range]가 System.Range와 겹쳐 컴파일 에러가 나므로 여기만 풀어 쓴다.
    [System.Serializable]
    [PostProcess(typeof(CctvNoiseRenderer), PostProcessEvent.AfterStack, "Custom/CCTV Noise")]
    public sealed class CctvNoise : PostProcessEffectSettings
    {
        [Range(0f, 1f), Tooltip("노이즈 전체의 세기. 0이면 화면이 그대로다")]
        public FloatParameter intensity = new FloatParameter { value = 0f };

        [Range(0f, 0.2f), Tooltip("가로줄이 옆으로 밀리는 최대 거리 (화면 폭 기준)")]
        public FloatParameter lineJitter = new FloatParameter { value = 0.06f };

        [Range(0f, 0.02f), Tooltip("빨강과 파랑이 어긋나는 거리 (화면 폭 기준)")]
        public FloatParameter colorSplit = new FloatParameter { value = 0.006f };

        [Range(0f, 1f), Tooltip("지지직 입자가 화면을 덮는 정도")]
        public FloatParameter staticAmount = new FloatParameter { value = 0.35f };

        [Range(0f, 1f), Tooltip("주사선이 어두워지는 정도")]
        public FloatParameter scanline = new FloatParameter { value = 0.25f };

        // 세기가 0이면 아예 그리지 않아서 화면 처리 비용이 들지 않는다.
        public override bool IsEnabledAndSupported(PostProcessRenderContext context)
        {
            return enabled.value && intensity.value > 0f;
        }
    }

    public sealed class CctvNoiseRenderer : PostProcessEffectRenderer<CctvNoise>
    {
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int NoiseTimeId = Shader.PropertyToID("_NoiseTime");
        private static readonly int ResolutionId = Shader.PropertyToID("_Resolution");
        private static readonly int LineJitterId = Shader.PropertyToID("_LineJitter");
        private static readonly int ColorSplitId = Shader.PropertyToID("_ColorSplit");
        private static readonly int StaticId = Shader.PropertyToID("_Static");
        private static readonly int ScanlineId = Shader.PropertyToID("_Scanline");

        public override void Render(PostProcessRenderContext context)
        {
            PropertySheet sheet = context.propertySheets.Get(Shader.Find("Hidden/Custom/CctvNoise"));
            sheet.properties.SetFloat(IntensityId, settings.intensity);
            // 일시정지 중에도 노이즈가 멈추지 않도록 실제 시간을 쓴다.
            sheet.properties.SetFloat(NoiseTimeId, Time.unscaledTime);
            sheet.properties.SetVector(ResolutionId, new Vector4(context.width, context.height, 0f, 0f));
            sheet.properties.SetFloat(LineJitterId, settings.lineJitter);
            sheet.properties.SetFloat(ColorSplitId, settings.colorSplit);
            sheet.properties.SetFloat(StaticId, settings.staticAmount);
            sheet.properties.SetFloat(ScanlineId, settings.scanline);
            context.command.BlitFullscreenTriangle(context.source, context.destination, sheet, 0);
        }
    }
}
