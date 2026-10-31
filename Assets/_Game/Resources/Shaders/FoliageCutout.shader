// 카메라와 플레이어 사이를 가리는 나무와 풀에, 플레이어 주변만 동그랗게 창을 뚫는 셰이더.
// FoliageFader가 가리는 오브젝트에만 잠깐 이 셰이더의 재질을 끼우고, 다 지나가면 원래 재질로 되돌린다.
//
// 화면에서 플레이어가 있는 자리를 중심으로 한 원 안의 픽셀을 그리지 않는다.
// 단, 플레이어보다 카메라에 가까운 픽셀만 지운다. 그래서 플레이어 뒤나 발밑의 풀은 그대로 보인다.
// 나무의 나머지 부분도 그대로 남아서, 숲의 모양을 해치지 않고 플레이어만 들여다보인다.
//
// 원의 위치, 크기, 깊이는 FoliageFader가 매 프레임 전역 값으로 넣어 준다.
//   _CutoutCenter   화면에서 플레이어 몸 한가운데의 위치 (0~1 비율)
//   _CutoutRadius   원의 반지름 (화면 높이에 대한 비율)
//   _CutoutDepth    카메라에서 플레이어까지의 깊이(m). 이보다 가까운 픽셀만 지운다
//   _CutoutEdge     원의 가장자리를 부드럽게 흩뜨리는 폭 (화면 높이에 대한 비율)
// 오브젝트마다 다른 _Fade(1: 창 없음, 0: 창이 다 열림)로 창이 서서히 열리고 닫힌다.
//
// 나머지는 숲 재질(Standard 셰이더의 Cutout 모드)과 같다. 잎 모양은 텍스처의 알파로 오려 낸다.
// 그림자는 Fallback 셰이더가 그리므로, 창이 뚫린 동안에도 그림자는 그대로 남는다.
//
// 빌드에도 포함되도록 Resources 폴더에 둔다. (코드가 Shader.Find로 이름을 찾아 쓰기 때문)
Shader "Hidden/Custom/FoliageCutout"
{
    Properties
    {
        _Color ("색", Color) = (1, 1, 1, 1)
        _MainTex ("색상", 2D) = "white" {}
        _Cutoff ("오려 내는 기준", Range(0, 1)) = 0.5
        _Glossiness ("매끄러움", Range(0, 1)) = 0
        _Metallic ("금속성", Range(0, 1)) = 0
        _Fade ("창이 닫힌 정도", Range(0, 1)) = 1
        _GrimeTex ("오염 텍스처", 2D) = "black" {}
        _GrimeSize ("텍스처 한 장의 크기(m)", Float) = 5
        _GrimeAmount ("덮는 정도 (오염 덤불·바위만)", Range(0, 1)) = 0
        _GrimeTint ("오염 색", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        #include "Assets/_Game/Resources/Shaders/ContaminatedGrime.cginc"

        sampler2D _MainTex;
        fixed4 _Color;
        half _Cutoff;
        half _Glossiness;
        half _Metallic;
        half _Fade;

        float4 _CutoutCenter;
        float _CutoutRadius;
        float _CutoutDepth;
        float _CutoutEdge;

        struct Input
        {
            float2 uv_MainTex;
            float4 screenPos;
            float3 worldPos;
            float3 worldNormal;
        };

        // 4x4 바둑판의 칸마다 정해 둔 기준값(0~1). 원의 가장자리를 고르게 흩뜨리는 데만 쓴다.
        static const float Bayer[16] =
        {
             0.0 / 16.0,  8.0 / 16.0,  2.0 / 16.0, 10.0 / 16.0,
            12.0 / 16.0,  4.0 / 16.0, 14.0 / 16.0,  6.0 / 16.0,
             3.0 / 16.0, 11.0 / 16.0,  1.0 / 16.0,  9.0 / 16.0,
            15.0 / 16.0,  7.0 / 16.0, 13.0 / 16.0,  5.0 / 16.0
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            // 잎 모양 오려 내기 (원래 재질과 같다)
            clip(c.a - _Cutoff);

            // 플레이어보다 카메라에 가까운 픽셀만 창의 대상이다. (원근 카메라에서 screenPos.w는 카메라로부터의 깊이)
            if (IN.screenPos.w < _CutoutDepth)
            {
                float2 uv = IN.screenPos.xy / max(IN.screenPos.w, 0.0001);
                // 화면이 가로로 길어도 원이 찌그러지지 않도록 가로를 화면 비율만큼 늘려 잰다.
                float2 delta = (uv - _CutoutCenter.xy) * float2(_ScreenParams.x / _ScreenParams.y, 1.0);
                float radius = _CutoutRadius * (1.0 - _Fade);

                // 0: 원 안(지움), 1: 원 밖(그림). 그 사이의 가장자리는 바둑판 무늬로 흩뜨린다.
                float keep = saturate((length(delta) - (radius - _CutoutEdge)) / max(_CutoutEdge, 0.0001));
                float2 pixel = floor(uv * _ScreenParams.xy);
                int index = (int)fmod(pixel.y, 4.0) * 4 + (int)fmod(pixel.x, 4.0);
                clip(keep - Bayer[index] - 0.001);
            }

            o.Albedo = ApplyGrime(c.rgb / max(_Color.rgb, 0.001), IN.worldPos, IN.worldNormal) * _Color.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }

    FallBack "Legacy Shaders/Transparent/Cutout/VertexLit"
}
