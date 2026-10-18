using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace Squad
{
    /// <summary>
    /// 화면 가장자리 오염(김서림 + 오염 결정) 후처리 효과의 설정값.
    /// Post Processing 패키지에 없는 효과라 직접 만든 것이다. 실제로 그리는 일은 ContaminationEdgeRenderer와
    /// Resources/Shaders/ContaminationEdge.shader가 하고, 세기는 ContaminationEdgeEffect가 온기 게이지로 정한다.
    /// </summary>
    // System 네임스페이스를 통째로 쓰면 [Range]가 System.Range와 겹쳐 컴파일 에러가 나므로 여기만 풀어 쓴다.
    [System.Serializable]
    [PostProcess(typeof(ContaminationEdgeRenderer), PostProcessEvent.AfterStack, "Custom/Contamination Edge")]
    public sealed class ContaminationEdge : PostProcessEffectSettings
    {
        [Range(0f, 1f), Tooltip("화면 색이 차가운 톤으로 기우는 정도")]
        public FloatParameter chill = new FloatParameter { value = 0f };

        [Range(0f, 1f), Tooltip("바이저 김서림의 정도")]
        public FloatParameter fog = new FloatParameter { value = 0f };

        [Range(0f, 1f), Tooltip("오염 결정이 덮는 정도. 1이면 화면의 3분의 1쯤을 덮는다")]
        public FloatParameter coverage = new FloatParameter { value = 0f };

        [Range(0f, 1f), Tooltip("결정의 가지 끝이 꿈틀거리며 빠르게 뻗는 정도")]
        public FloatParameter growth = new FloatParameter { value = 0f };

        [Range(0f, 1f), Tooltip("오염이 타서 걷히는 경계의 주황빛 그을림")]
        public FloatParameter burn = new FloatParameter { value = 0f };

        // 아무 효과도 없으면 아예 그리지 않아서 화면 처리 비용이 들지 않는다.
        public override bool IsEnabledAndSupported(PostProcessRenderContext context)
        {
            return enabled.value &&
                   (chill.value > 0f || fog.value > 0f || coverage.value > 0f || burn.value > 0f);
        }
    }

    public sealed class ContaminationEdgeRenderer : PostProcessEffectRenderer<ContaminationEdge>
    {
        private static readonly int ChillId = Shader.PropertyToID("_Chill");
        private static readonly int FogId = Shader.PropertyToID("_Fog");
        private static readonly int CoverageId = Shader.PropertyToID("_Coverage");
        private static readonly int GrowthId = Shader.PropertyToID("_Growth");
        private static readonly int BurnId = Shader.PropertyToID("_Burn");
        private static readonly int EdgeTimeId = Shader.PropertyToID("_EdgeTime");
        private static readonly int ResolutionId = Shader.PropertyToID("_Resolution");

        public override void Render(PostProcessRenderContext context)
        {
            PropertySheet sheet = context.propertySheets.Get(Shader.Find("Hidden/Custom/ContaminationEdge"));
            sheet.properties.SetFloat(ChillId, settings.chill);
            sheet.properties.SetFloat(FogId, settings.fog);
            sheet.properties.SetFloat(CoverageId, settings.coverage);
            sheet.properties.SetFloat(GrowthId, settings.growth);
            sheet.properties.SetFloat(BurnId, settings.burn);
            sheet.properties.SetFloat(EdgeTimeId, Time.unscaledTime);
            sheet.properties.SetVector(ResolutionId, new Vector4(context.width, context.height, 0f, 0f));
            context.command.BlitFullscreenTriangle(context.source, context.destination, sheet, 0);
        }
    }
}
