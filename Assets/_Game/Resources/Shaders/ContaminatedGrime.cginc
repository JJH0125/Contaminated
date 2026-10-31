// 표면에 오염 텍스처를 덧입히는 계산. 오염 덤불과 엄폐 바위(ContaminatedSurface)가 쓰고,
// 그것들에 창이 뚫리는 동안(FoliageCutout)에도 같은 모습이 유지되도록 함께 쓴다.
//
// 복제한 모델마다 UV가 제각각이라, 텍스처를 월드 좌표로 세 방향에서 투영한다(트라이플래너).
// 어떤 모델이든 같은 크기의 무늬가 늘어나거나 끊기지 않고 입혀진다.
// 번지는 방식은 보호복과 같다(Contamination.cginc). 텍스처의 어두운 곳(틈, 균열)부터 덮인다.
//
//   _GrimeTex     오염 텍스처
//   _GrimeSize    텍스처 한 장이 덮는 크기(m)
//   _GrimeAmount  덮는 정도 (0이면 원래 모습 그대로)
//   _GrimeTint    텍스처에 곱할 색
#ifndef CONTAMINATED_GRIME_INCLUDED
#define CONTAMINATED_GRIME_INCLUDED

#include "Assets/_Game/Resources/Shaders/Contamination.cginc"

sampler2D _GrimeTex;
float _GrimeSize;
half _GrimeAmount;
fixed4 _GrimeTint;

fixed3 ApplyGrime(fixed3 albedo, float3 worldPos, float3 worldNormal)
{
    if (_GrimeAmount <= 0.001)
        return albedo;

    // 면이 향하는 축일수록 그 방향의 투영을 많이 쓴다.
    float3 weights = pow(abs(worldNormal), 4.0);
    weights /= max(weights.x + weights.y + weights.z, 0.0001);

    float3 uvw = worldPos / max(_GrimeSize, 0.01);
    fixed3 grime = tex2D(_GrimeTex, uvw.zy).rgb * weights.x
                 + tex2D(_GrimeTex, uvw.xz).rgb * weights.y
                 + tex2D(_GrimeTex, uvw.xy).rgb * weights.z;

    float brightness = dot(grime, float3(0.3, 0.59, 0.11));
    float mask = ContaminationMask(1.0 - brightness, _GrimeAmount, 0.15);
    return lerp(albedo, grime * _GrimeTint.rgb, mask);
}

#endif
