// 보호복이 서서히 오염되어 가는 모습을 그리는 셰이더.
//
// 같은 펼침(UV)을 쓰는 텍스처 두 장을 받는다.
//   _MainTex   깨끗한 보호복
//   _DirtyTex  오염된 보호복 (때, 얼룩, 바이저의 무늬)
//
// 두 장을 통째로 섞으면 옷 전체가 고르게 흐려질 뿐이라 "번져 간다"는 느낌이 나지 않는다.
// 그래서 두 텍스처의 색이 많이 다른 곳(진한 얼룩, 바이저의 무늬)부터 먼저 드러나고,
// 오염 정도가 오를수록 옅은 얼룩까지 차례로 퍼지게 한다.
//   _Contamination = 0  깨끗한 보호복 그대로
//   _Contamination = 1  오염된 보호복 그대로
Shader "Custom/Contaminated Suit"
{
    Properties
    {
        _Color ("색", Color) = (1, 1, 1, 1)
        _MainTex ("깨끗한 색상", 2D) = "white" {}
        _DirtyTex ("오염된 색상", 2D) = "white" {}
        [Normal] _BumpMap ("노멀맵", 2D) = "bump" {}
        _Contamination ("오염 정도", Range(0, 1)) = 0
        _Spread ("번지는 경계의 부드러움", Range(0.01, 0.5)) = 0.15
        _Glossiness ("매끄러움", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        // Standard 셰이더와 같은 방식으로 빛을 받고, 모든 조명의 그림자를 받는다.
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        // 오염이 번지는 계산은 Fake 차원의 오염 흔적과 함께 쓴다.
        #include "Assets/_Game/Resources/Shaders/Contamination.cginc"

        sampler2D _MainTex;
        sampler2D _DirtyTex;
        sampler2D _BumpMap;
        fixed4 _Color;
        half _Contamination;
        half _Spread;
        half _Glossiness;

        struct Input
        {
            float2 uv_MainTex;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed3 clean = tex2D(_MainTex, IN.uv_MainTex).rgb;
            fixed3 dirty = tex2D(_DirtyTex, IN.uv_MainTex).rgb;

            // 두 색이 얼마나 다른가 (0: 같음, 1: 아주 다름)
            half difference = saturate(length(dirty - clean) * 1.5);

            // 오염 정도가 오를수록 덜 다른 곳까지 오염된 색으로 바뀐다.
            half mask = ContaminationMask(difference, _Contamination, _Spread);

            o.Albedo = lerp(clean, dirty, mask) * _Color.rgb;
            // 노멀맵은 오염된 보호복의 것이라 얼룩과 바이저 무늬의 굴곡이 들어 있다.
            // 깨끗한 곳까지 굴곡을 주면 빛을 받을 때 무늬가 드러나므로, 오염된 만큼만 굴곡을 준다.
            half3 dirtyNormal = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));
            o.Normal = normalize(lerp(half3(0, 0, 1), dirtyNormal, mask));
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }

    FallBack "Diffuse"
}
