// Fake 차원 적의 모습. 빛을 먹는 검은 형체이고, 횃불 빛이 닿은 곳에만 흰 오염 결정이 드러난다.
//
//   어둠 속      조명을 받지 않는 거의 검은색. 가장자리(림)만 아주 옅게 보인다
//   가까워지면   플레이어에게 다가올수록 윤곽이 밝아진다. 횃불을 숨겨도 위협을 알아챌 수 있게 하는 단서다
//   빛이 닿으면  횃불이 향하는 쪽 일정 거리 안의 부분에 결정 무늬가 번져 나타난다 (Contamination.cginc)
//
// 무늬는 몸의 UV를 따라 그린다. 월드 좌표로 그리면 움직이는 몸 위에서 무늬가 미끄러진다.
// 값은 모두 AshEnemyLight가 매 프레임 전역으로 넣어 준다. (GameTuning의 ashEnemy 항목)
Shader "Custom/Ash Enemy"
{
    Properties
    {
        // 원래 재질의 값이 남아 있어도 쓰지 않는다. 재질 창에서 만질 값은 없다.
        _MainTex ("쓰지 않음", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "Assets/_Game/Resources/Shaders/Contamination.cginc"
            #include "Assets/_Game/Resources/Shaders/AshEnemy.cginc"

            float _AshNearDistance;   // 이 거리부터 윤곽이 밝아지기 시작한다
            float _AshBaseRim;        // 멀리서의 윤곽 밝기
            float _AshRimPower;
            float _AshPatternScale;
            float _AshWobble;
            fixed4 _AshBodyColor;
            fixed4 _AshRimColor;
            fixed4 _AshPatternColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                UNITY_FOG_COORDS(3)
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.uv = v.uv;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                // 몸이 연기처럼 천천히 일렁인다.
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                world += o.worldNormal * sin(_Time.y * 1.5 + world.y * 0.7 + world.x) * _AshWobble;
                o.worldPos = world;
                o.pos = UnityWorldToClipPos(world);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float2 Hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            // 결정의 경계선 (1: 경계 위, 0: 결정 안쪽). 칸마다 흩뿌린 점 중 가까운 두 점의 거리 차로 구한다.
            float CrystalLines(float2 uv)
            {
                float2 cell = floor(uv);
                float2 inside = frac(uv);
                float nearest = 8.0;
                float second = 8.0;
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 offset = float2(x, y);
                        float gap = length(offset + Hash2(cell + offset) - inside);
                        if (gap < nearest)
                        {
                            second = nearest;
                            nearest = gap;
                        }
                        else if (gap < second)
                        {
                            second = gap;
                        }
                    }
                }
                return 1.0 - saturate((second - nearest) / 0.12);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 normal = normalize(i.worldNormal);
                float3 view = normalize(_WorldSpaceCameraPos - i.worldPos);
                float rim = pow(1.0 - saturate(dot(normal, view)), _AshRimPower);

                float3 fromTorch = i.worldPos - _AshTorchPos.xyz;
                float torchDistance = length(fromTorch);

                // 가까울수록 윤곽이 밝아진다. 횃불을 숨겨도 마찬가지다.
                float closeness = 1.0 - saturate(torchDistance / max(_AshNearDistance, 0.01));
                fixed3 color = _AshBodyColor.rgb + _AshRimColor.rgb * rim * (_AshBaseRim + closeness * closeness);

                // 횃불이 향하는 쪽, 일정 거리 안에 든 부분에만 결정이 번진다.
                float reveal = AshReveal(i.worldPos);

                float mask = ContaminationMask(CrystalLines(i.uv * _AshPatternScale), reveal * 0.8, 0.15);
                color += _AshPatternColor.rgb * mask;

                fixed4 result = fixed4(color, 1.0);
                UNITY_APPLY_FOG(i.fogCoord, result);
                return result;
            }
            ENDCG
        }
    }

    // 그림자와 깊이는 Fallback이 그린다. (오염 얼룩 효과가 깊이를 읽으므로 빠지면 안 된다)
    FallBack "Diffuse"
}
