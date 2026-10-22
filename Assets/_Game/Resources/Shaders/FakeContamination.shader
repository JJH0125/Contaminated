// Fake 차원의 오염 흔적을 그리는 후처리 셰이더.
//
// Fake에 들어가는 순간 플레이어 자리에서 오염의 물결이 퍼져 나가고, 물결이 지나간 땅과 나무, 바위에
// 그동안 보이지 않던 오염의 얼룩이 드러난다. Fake가 Real보다 훨씬 오염된 곳이라는 것을 보여 준다.
//
// 얼룩이 번지는 방식은 보호복과 같다(Contamination.cginc).
//   보호복은 "깨끗한 텍스처와 오염된 텍스처의 색 차이"가 큰 곳부터 덮이고,
//   여기서는 "월드 공간 노이즈"가 큰 곳부터 덮인다. 물결이 지나간 뒤 오염 정도가 0에서 정해 둔 값까지 오르므로,
//   진한 얼룩 덩어리가 먼저 드러나고 그 둘레로 번져 나간다.
//   (보호복의 텍스처는 옷의 펼침에 맞춰 조각조각 그려져 있어, 맵에 그대로 입히면 모자이크처럼 보인다)
//
// 화면에 보이는 모든 것에 한꺼번에 입히기 위해, 재질을 바꾸지 않고 화면 처리로 그린다.
// 카메라의 깊이 텍스처에서 픽셀마다 월드 위치를 되살려 얼룩을 정한다.
//
// 이 파일과 값을 넣는 코드: FakeContamination.cs(효과), FakeContaminationEffect.cs(언제 퍼지는지)
// 빌드에도 포함되도록 Resources 폴더에 둔다. (코드가 Shader.Find로 이름을 찾아 쓰기 때문)
Shader "Hidden/Custom/FakeContamination"
{
    HLSLINCLUDE

        #include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"
        #include "Assets/_Game/Resources/Shaders/Contamination.cginc"

        TEXTURE2D_SAMPLER2D(_MainTex, sampler_MainTex);
        TEXTURE2D_SAMPLER2D(_CameraDepthTexture, sampler_CameraDepthTexture);

        // 카메라: 위치와, 화면 네 귀퉁이로 향하는 방향(카메라 앞쪽 성분이 1이 되도록 맞춘 것)
        float3 _CamPos;
        float3 _RayBL;
        float3 _RayBR;
        float3 _RayTL;
        float3 _RayTR;
        float _Aspect;

        // 퍼지는 물결
        float _Presence;      // 효과 전체의 세기 (Real로 돌아갈 때 0으로 줄어든다)
        float3 _Origin;       // 물결이 시작된 자리
        float _Radius;        // 물결이 지금까지 퍼진 거리(m)
        float _GrowDistance;
        float _FrontWidth;
        float4 _FrontColor;
        float _FrontGlow;
        float _FrontFade;

        // 얼룩
        float _Amount;
        float _Spread;
        float _StainSize;
        float4 _StainColor;
        float _StainStrength;
        float4 _RimColor;
        float _RimGlow;
        float _EffectTime;

        // 안개 (mode, density, start, end). mode 0이면 안개 없음
        float4 _FogInfo;

        // 캐릭터(플레이어, 적) 몸에는 얼룩을 묻히지 않는다. 움직이는 몸에 월드 무늬가 미끄러져 보이기 때문
        float4 _ClearCylinders[8];   // (x, 발 높이, z, 반경)
        float _ClearHeights[8];      // 발에서 머리까지의 높이
        float _ClearCount;

        // FoliageFader가 플레이어를 가리는 나무에 뚫은 창 (FoliageCutout 셰이더와 같은 전역 값)
        float4 _CutoutCenter;
        float _CutoutRadius;
        float _CutoutDepth;

        float Hash(float3 p)
        {
            p = frac(p * 0.3183099 + 0.1);
            p *= 17.0;
            return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
        }

        float Noise(float3 x)
        {
            float3 i = floor(x);
            float3 f = frac(x);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(lerp(Hash(i + float3(0, 0, 0)), Hash(i + float3(1, 0, 0)), f.x),
                             lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                        lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x),
                             lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
        }

        // 여러 크기의 노이즈를 겹친 것 (0~1). 큰 덩어리에 잔 얼룩이 섞인 모양이 된다.
        float Fbm(float3 p)
        {
            float value = 0.0;
            float weight = 0.5;
            for (int k = 0; k < 4; k++)
            {
                value += weight * Noise(p);
                p = p * 2.03 + 17.1;
                weight *= 0.5;
            }
            return value / 0.9375;
        }

        float FogFactor(float distance)
        {
            if (_FogInfo.x < 0.5)
                return 1.0;
            if (_FogInfo.x < 1.5)   // Linear
                return saturate((_FogInfo.w - distance) / max(_FogInfo.w - _FogInfo.z, 0.001));
            if (_FogInfo.x < 2.5)   // Exponential
                return exp(-_FogInfo.y * distance);
            float d = _FogInfo.y * distance;   // Exponential Squared
            return exp(-d * d);
        }

        float4 Frag(VaryingsDefault i) : SV_Target
        {
            float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoordStereo);

            float raw = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, i.texcoordStereo);
            // 하늘처럼 아무것도 없는 곳은 그대로 둔다.
            if (Linear01Depth(raw) > 0.999)
                return color;

            float depth = LinearEyeDepth(raw);
            float2 uv = i.texcoord;

            // 플레이어를 가리는 나무에 창이 뚫려 있으면, 창 안의 깊이는 지워진 나무의 것이라 얼룩을 그리지 않는다.
            float2 delta = (uv - _CutoutCenter.xy) * float2(_Aspect, 1.0);
            if (_CutoutRadius > 0.0 && depth < _CutoutDepth && length(delta) < _CutoutRadius)
                return color;

            float3 ray = lerp(lerp(_RayBL, _RayBR, uv.x), lerp(_RayTL, _RayTR, uv.x), uv.y);
            float3 world = _CamPos + ray * depth;

            // 캐릭터의 몸 안이면 지운다. (발 높이에서 조금 위부터 재서, 발밑의 땅에는 그대로 묻는다)
            float clear = 0.0;
            for (int k = 0; k < 8; k++)
            {
                if (k >= (int)_ClearCount)
                    break;
                float4 c = _ClearCylinders[k];
                float d = length(world.xz - c.xz);
                float h = world.y - c.y;
                float inside = (1.0 - smoothstep(c.w * 0.75, c.w, d)) * step(0.3, h) * step(h, _ClearHeights[k]);
                clear = max(clear, inside);
            }

            // 물결: 시작 자리에서 평면 거리로 퍼진다. 지나간 뒤 growDistance만큼 더 나아가는 동안 얼룩이 다 번진다.
            float distance = length(world.xz - _Origin.xz);
            float reveal = saturate((_Radius - distance) / max(_GrowDistance, 0.01));

            // 얼룩: 보호복과 같은 방식으로, 노이즈가 큰 곳부터 덮인다. 세로로 조금 늘여 흘러내린 자국처럼 보이게 한다.
            float3 p = world / max(_StainSize, 0.01) * float3(1.0, 0.6, 1.0);
            float difference = saturate((Fbm(p) - 0.3) * 2.0);
            float mask = ContaminationMask(difference, _Amount * reveal, _Spread);
            // 얼룩의 가장자리 (덮인 곳과 아닌 곳의 경계)
            float rim = saturate(mask * (1.0 - mask) * 4.0);
            rim *= 0.75 + 0.25 * sin(_EffectTime * 2.0 + difference * 12.0);

            // 물결의 앞머리: 지금 퍼진 거리 근처의 띠. 멀리 퍼질수록 흐려진다.
            float frontOffset = (distance - _Radius) / max(_FrontWidth, 0.01);
            float front = exp(-frontOffset * frontOffset) * saturate(1.0 - _Radius / max(_FrontFade, 0.01));

            float strength = _Presence * FogFactor(length(ray * depth)) * (1.0 - clear);

            float3 stained = color.rgb * lerp(float3(1, 1, 1), _StainColor.rgb, _StainStrength * mask);
            stained += _RimColor.rgb * rim * _RimGlow;
            stained += _FrontColor.rgb * front * _FrontGlow;

            color.rgb = lerp(color.rgb, stained, strength);
            return color;
        }

    ENDHLSL

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
                #pragma vertex VertDefault
                #pragma fragment Frag
            ENDHLSL
        }
    }
}
