// 오염이 번져 가는 계산. 보호복(ContaminatedSuit)과 Fake 차원의 오염 흔적(FakeContamination)이 함께 쓴다.
//
// 표면의 자리마다 "얼마나 오염되기 쉬운가"(difference, 0~1)가 정해져 있고,
// 오염 정도(contamination)가 오를수록 기준선이 내려가서 오염되기 쉬운 곳부터 차례로 덮인다.
// 그래서 전체가 고르게 흐려지지 않고, 진한 얼룩이 먼저 드러난 뒤 옅은 곳까지 번져 나간다.
//   보호복        difference = 깨끗한 텍스처와 오염된 텍스처의 색 차이 (진한 얼룩, 바이저의 무늬부터)
//   Fake의 흔적   difference = 월드 공간의 노이즈 (맵 곳곳의 얼룩 덩어리부터)
//
//   contamination = 0  아무 곳도 덮이지 않는다
//   contamination = 1  모든 곳이 덮인다
//   spread             번지는 경계의 부드러움
#ifndef CONTAMINATION_INCLUDED
#define CONTAMINATION_INCLUDED

float ContaminationMask(float difference, float contamination, float spread)
{
    // 오염 정도가 0이면 기준선이 1보다 위에 있어 아무 곳도 바뀌지 않는다.
    float threshold = 1 - contamination * (1 + spread * 2) + spread;
    float mask = smoothstep(threshold - spread, threshold + spread, difference);
    // 끝에서는 오염되기 어려운 곳까지 모두 덮이도록 한다.
    return max(mask, saturate((contamination - 0.9) * 10));
}

#endif
