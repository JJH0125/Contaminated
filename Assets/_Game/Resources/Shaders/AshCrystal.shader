// 재의 형체에 박힌 결정. 평소에는 어둡게 겨우 보여 앞쪽을 알려 주고, 횃불 빛이 닿으면 하얗게 빛난다.
// 빌드에도 포함되도록 Resources 폴더에 둔다. (코드가 Shader.Find로 이름을 찾아 쓰기 때문)
Shader "Hidden/Custom/AshCrystal"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            #include "Assets/_Game/Resources/Shaders/AshEnemy.cginc"

            fixed4 _AshPatternColor;
            float _AshCrystalGlow;

            struct appdata { float4 vertex : POSITION; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                UNITY_FOG_COORDS(1)
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 result = fixed4(_AshPatternColor.rgb * max(AshReveal(i.worldPos), _AshCrystalGlow), 1.0);
                UNITY_APPLY_FOG(i.fogCoord, result);
                return result;
            }
            ENDCG
        }
    }
}
