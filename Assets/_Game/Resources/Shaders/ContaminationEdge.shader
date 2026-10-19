// 횃불을 숨기는 동안 화면 가장자리부터 바이저에 김이 서리고, 그 위로 오염이 결정처럼 자라 들어오는 후처리 셰이더.
// ContaminationEdge(PostProcessEffectSettings)가 이 셰이더로 화면 전체를 다시 그린다.
//
// 불 = 정화. 불이 없으면 오염이 스며들고, 불을 다시 들면 오염이 타서 걷힌다.
// 이미지 파일 없이 수식으로만 만든다.
//   _Chill     가장자리 쪽 색이 살짝 차가운 톤으로 기운다. "춥다"고 말하지 않고 색 온도로만 느끼게 한다
//
// 모든 효과는 화면 가장자리 띠 안에서만 일어나고, 가운데는 원래 화면 그대로 둔다. (산만하지 않게)
//   _Fog       바이저 김서림. 가장자리부터 뿌옇게 흐려지고 물방울이 맺힌다
//   _Coverage  오염 결정. 가장자리에서 가지를 치며 자라 들어온다. 흰빛 섞인 회색 바탕에 바이저와 같은 검은 핏줄
//              (색을 쓰지 않아서, 나중에 Fake를 흑백으로 바꿔도 "색은 불뿐" 규칙과 어긋나지 않는다)
//   _Growth    결정의 가지 끝이 꿈틀거리며 빠르게 뻗는 정도
//   _Burn      횃불을 들어 오염이 타서 걷히는 동안, 물러나는 경계가 주황빛으로 그을린다
//
// 빌드에도 포함되도록 Resources 폴더에 둔다. (코드가 Shader.Find로 이름을 찾아 쓰기 때문)
Shader "Hidden/Custom/ContaminationEdge"
{
    HLSLINCLUDE

        #include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"

        TEXTURE2D_SAMPLER2D(_MainTex, sampler_MainTex);
        float _Chill;
        float _Fog;
        float _Coverage;
        float _Growth;
        float _Burn;
        float _EdgeTime;
        float4 _Resolution;   // (가로 픽셀, 세로 픽셀, 0, 0)

        // 오염 결정이 가장 깊이 들어올 때의 깊이 (화면 높이에 대한 비율). 16:9 화면에서 넓이의 약 3분의 1이다.
        static const float MaxDepth = 0.13;
        // 김서림이 가장 깊이 들어올 때의 깊이. 화면 가운데는 건드리지 않도록 가장자리 띠 안에서만 낀다.
        static const float FogDepth = 0.16;
        // 차가운 색 톤이 드는 가장자리 띠의 폭. 이보다 안쪽(화면 가운데)은 색이 그대로다.
        static const float ChillDepth = 0.25;

        float Hash(float2 p)
        {
            return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
        }

        float Noise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            float2 u = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x),
                        lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
        }

        float Fbm(float2 p)
        {
            float sum = 0.0;
            float amp = 0.5;
            for (int k = 0; k < 5; k++)
            {
                sum += Noise(p) * amp;
                p = p * 2.03 + 17.0;
                amp *= 0.5;
            }
            return sum;
        }

        // 능선 노이즈: 값이 0.5 부근인 곳에 가늘고 갈라지는 선이 생긴다. 가지 치는 결정과 핏줄을 만든다.
        float Ridge(float2 p)
        {
            return 1.0 - abs(Fbm(p) * 2.0 - 1.0);
        }

        float4 Frag(VaryingsDefault i) : SV_Target
        {
            float2 uv = i.texcoord;
            float3 scene = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;
            if (_Chill <= 0.0001 && _Fog <= 0.0001 && _Coverage <= 0.0001 && _Burn <= 0.0001)
                return float4(scene, 1.0);

            float aspect = _Resolution.x / _Resolution.y;
            float2 p = uv * float2(aspect, 1.0);
            // 화면 가장자리까지의 거리 (화면 높이 단위)
            float edge = min(min(uv.x * aspect, (1.0 - uv.x) * aspect), min(uv.y, 1.0 - uv.y));

            // ── 1. 차가운 색 온도 ──
            float luminance = dot(scene, float3(0.299, 0.587, 0.114));
            float3 cold = lerp(scene, luminance.xxx, 0.25) * float3(0.86, 0.95, 1.08);
            // 가장자리에서만 차가워지고, 안쪽으로 갈수록 원래 색으로 돌아온다.
            float chillMask = _Chill * (1.0 - smoothstep(0.0, ChillDepth, edge));
            float3 color = lerp(scene, cold, chillMask);

            // ── 2. 바이저 김서림 ──
            float fogField = edge + (Fbm(p * 3.0) - 0.5) * 0.12;
            float fogMask = _Fog * (1.0 - smoothstep(0.0, FogDepth * _Fog + 0.0001, fogField));
            if (fogMask > 0.001)
            {
                // 흐림: 주변 여덟 곳을 섞는다. 가장자리일수록 넓게 섞어 더 뿌옇다.
                float r = fogMask * 0.012;
                float3 blur = color;
                blur += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( r, 0)).rgb;
                blur += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-r, 0)).rgb;
                blur += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0,  r * aspect)).rgb;
                blur += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0, -r * aspect)).rgb;
                blur += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( r,  r * aspect) * 0.7).rgb;
                blur += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-r,  r * aspect) * 0.7).rgb;
                blur += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( r, -r * aspect) * 0.7).rgb;
                blur += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-r, -r * aspect) * 0.7).rgb;
                blur /= 9.0;
                blur = lerp(blur, cold, chillMask);

                // 뿌연 막과 맺힌 물방울
                float3 haze = float3(0.62, 0.66, 0.70);
                float2 dropCell = floor(p * 60.0);
                float2 dropPos = frac(p * 60.0) - 0.5;
                float dropOn = step(0.82, Hash(dropCell));
                float drop = dropOn * (1.0 - smoothstep(0.15, 0.32, length(dropPos))) * fogMask;
                color = lerp(blur, haze, fogMask * 0.55);
                color = lerp(color, color * 0.75 + 0.12, drop * 0.6);
            }

            // ── 3. 오염 결정 ──
            float2 flow = float2(_EdgeTime * 0.3, -_EdgeTime * 0.23) * _Growth;
            float rough = Fbm(p * 6.0 + flow) - 0.5;
            float front = _Coverage * MaxDepth * (1.0 + 0.25 * _Growth * sin(_EdgeTime * 3.0 + rough * 6.0));
            float field = edge + rough * 0.06;

            // 가지: 능선 노이즈를 가장자리 쪽으로 늘어뜨려, 가장자리에서 안쪽으로 뻗는 줄기와 잔가지처럼 보이게 한다.
            float2 warp = float2(Fbm(p * 4.0 + 5.1), Fbm(p * 4.0 + 9.3)) * 0.35;
            float branch = smoothstep(0.82, 0.96, Ridge(p * 9.0 + warp + flow * 0.5));
            // 가지 끝은 경계보다 조금 더 안쪽까지 뻗는다.
            float reach = front + branch * 0.035 * _Coverage;
            float crust = 1.0 - smoothstep(front - 0.01, front + 0.01, field);   // 빽빽하게 덮인 부분
            float tips = (1.0 - smoothstep(reach - 0.005, reach + 0.005, field)) * branch;
            float growth = saturate(max(crust, tips));

            if (growth > 0.001)
            {
                float depth = saturate(1.0 - field / max(front, 0.0001));
                float grain = Fbm(p * 70.0);
                float3 crystal = lerp(float3(0.55, 0.56, 0.57), float3(0.86, 0.87, 0.88), saturate(branch * 0.7 + grain * 0.5));
                color = lerp(color, crystal, growth * (0.55 + depth * 0.35));

                // 검은 핏줄: 바이저 오염과 같은 색. 결정 안쪽(가장자리 쪽)일수록 짙다.
                float vein = smoothstep(0.88, 0.97, Ridge(p * 5.0 + 3.7 + warp));
                vein *= growth * saturate(depth * 1.5 + branch * 0.3);
                color = lerp(color, float3(0.05, 0.0, 0.02), vein * 0.9);
            }

            // ── 4. 타서 걷힘: 물러나는 경계가 주황빛으로 그을린다 ──
            if (_Burn > 0.001)
            {
                float rim = exp(-pow((field - front) / 0.012, 2.0)) * (0.6 + 0.4 * Noise(p * 40.0 + _EdgeTime * 4.0));
                color += float3(1.0, 0.45, 0.12) * rim * _Burn * 0.9;
            }

            return float4(color, 1.0);
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
