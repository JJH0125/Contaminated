// 돌에 흰 테두리를 그리는 셰이더. 아무리 어두워도 돌이 어디 있는지 보이게 한다.
// Stone이 돌 모양의 복사본을 하나 만들어 이 셰이더로 그린다.
//
// 복사본을 면 방향으로 살짝 부풀리고, 앞면은 그리지 않고 뒷면만 그린다(Cull Front).
// 그러면 원래 돌이 가운데를 가리고, 부풀어 삐져나온 가장자리만 테두리로 남는다.
// 부풀리는 양은 화면 픽셀 기준이라, 돌이 멀리 있어도 테두리 굵기가 같다.
// 조명과 안개를 받지 않아서 어둠 속에서도 늘 같은 밝기로 보인다.
//
// 빌드에도 포함되도록 Resources 폴더에 둔다. (코드가 Shader.Find로 이름을 찾아 쓰기 때문)
Shader "Hidden/Custom/StoneOutline"
{
    Properties
    {
        _OutlineColor ("테두리 색", Color) = (1, 1, 1, 1)
        _OutlineWidth ("테두리 굵기 (픽셀)", Float) = 3
    }

    SubShader
    {
        Tags { "Queue" = "Geometry+10" "RenderType" = "Opaque" }

        Pass
        {
            Cull Front
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _OutlineColor;
            float _OutlineWidth;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // 면 방향을 화면 위의 방향으로 바꿔서, 그쪽으로 정해진 픽셀만큼 밀어낸다.
                float3 viewNormal = mul((float3x3)UNITY_MATRIX_IT_MV, v.normal);
                float2 screenNormal = normalize(mul((float2x2)UNITY_MATRIX_P, viewNormal.xy) + 0.0001);
                o.pos.xy += screenNormal * _OutlineWidth * 2.0 / _ScreenParams.xy * o.pos.w;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }
    }
}
