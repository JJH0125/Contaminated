// 오염 텍스처를 덧입힌 표면. 오염 덤불과 엄폐 바위에 쓴다. (ContaminatedGrime.cginc)
// BarrierBuilder가 원래 재질을 복사해 셰이더만 이것으로 바꾸므로, 모델의 색과 잎 모양은 그대로 남는다.
// 잎 모양은 원래처럼 텍스처의 알파로 오려 낸다. 오려 낼 것이 없는 바위는 _Cutoff를 0으로 둔다.
// 그림자와 깊이는 Fallback 셰이더가 그린다.
//
// 빌드에도 포함되도록 Resources 폴더에 둔다. (코드가 Shader.Find로 이름을 찾아 쓰기 때문)
Shader "Hidden/Custom/ContaminatedSurface"
{
    Properties
    {
        _Color ("색", Color) = (1, 1, 1, 1)
        _MainTex ("색상", 2D) = "white" {}
        _Cutoff ("오려 내는 기준", Range(0, 1)) = 0.5
        _Glossiness ("매끄러움", Range(0, 1)) = 0
        _Metallic ("금속성", Range(0, 1)) = 0
        _GrimeTex ("오염 텍스처", 2D) = "black" {}
        _GrimeSize ("텍스처 한 장의 크기(m)", Float) = 5
        _GrimeAmount ("덮는 정도", Range(0, 1)) = 0.8
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

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float3 worldNormal;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex);
            clip(c.a - _Cutoff);

            // _Color는 덧입힌 뒤에 곱한다. 타서 사라지는 연출과 차원별 색이 오염 무늬까지 함께 물들인다.
            o.Albedo = ApplyGrime(c.rgb, IN.worldPos, IN.worldNormal) * _Color.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }

    FallBack "Legacy Shaders/Transparent/Cutout/VertexLit"
}
