// Fake 적(재의 형체)이 함께 쓰는, "횃불 빛이 이 자리에 얼마나 닿는가" 계산. 몸과 결정이 같은 값을 쓴다.
// 값은 AshEnemyLight가 매 프레임 전역으로 넣어 준다.
#ifndef ASH_ENEMY_INCLUDED
#define ASH_ENEMY_INCLUDED

float4 _AshTorchPos;      // 횃불 자리
float4 _AshTorchForward;  // 횃불이 향하는 방향
float _AshTorchOn;        // 횃불을 들고 있으면 1, 숨겼으면 0
float _AshFullReveal;     // 잡힐 때 1: 몸 전체에 드러난다
float _AshRevealCos;      // 빛이 닿는 각도의 절반의 cos
float _AshRevealSoftCos;  // 그 바깥쪽 흐려지는 경계의 cos
float _AshRevealDistance;

float AshReveal(float3 worldPos)
{
    float3 fromTorch = worldPos - _AshTorchPos.xyz;
    float torchDistance = length(fromTorch);
    float aim = dot(fromTorch / max(torchDistance, 0.001), _AshTorchForward.xyz);
    float cone = smoothstep(_AshRevealSoftCos, _AshRevealCos, aim);
    float reach = 1.0 - smoothstep(_AshRevealDistance * 0.7, _AshRevealDistance, torchDistance);
    return max(cone * reach * _AshTorchOn, _AshFullReveal);
}

#endif
