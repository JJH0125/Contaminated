using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 적이 각각 가지는, 정보가 적힌 일종의 문서.
    /// GoapAction이 들여다보는 용도.
    /// GoapAction이 순수 클래스이므로, Context 또한 이에 맞게 순수 클래스로 정의.
    /// 
    /// 저장하고 있는 것 :
    /// 자신과 플레이어의 Transform
    /// 자신이 사용할 블랙보드
    /// 자신의 Locomotion
    /// 자신의 DimensionMember
    /// 자신의 Action이 사용할 수치 값들 (잡는 거리, 도착 거리, 배회 시간)
    /// </summary>
    public class ChaserContext
    {
        // 자신이 사용할 공유 칠판.
        public SquadBlackboard Blackboard;
        // 자신의 Transform.
        public Transform Self;
        // 추격할 플레이어의 Transform.
        public Transform Player;
        // 자신의 차원 이름표.
        public DimensionMember Member;
        // 자신의 경계 상태.
        public ChaserAlert Alert;
        // 자신의 이동을 담당하는 컴포넌트.
        public ChaserLocomotion Locomotion;
        // ReachPlayer가 사용하는 잡는 판정 거리.
        public float CatchRadius;
        // MoveToSound, WanderStep이 사용하는 목표 지점 도착 판정 거리.
        public float ArriveRadius;
        // 한 WanderStep 완료 후 다음 WanderStep 시작 전 대기하는 최소/최대 시간(이 범위에서 매번 랜덤 선택).
        public float WanderPauseMin;
        public float WanderPauseMax;
        // 신호를 받고 합류했는지
        public bool Joined;
        // 대응 중인 사건 번호
        public int ProcessingSpotCount;

        // 추격 겹침: 앞질러 가는 적이 몇 초 뒤의 플레이어 위치를 노리는가
        public float InterceptLeadTime;
        // 앞질러 가는 적끼리 좌우로 벌리는 간격(m)
        public float InterceptSpacing;
        // 플레이어가 이 거리(m) 안이면 역할과 상관없이 곧장 달려든다
        public float InterceptDirectRadius;
        // 지금 맡은 추격 역할(0 = 곧장 쫓음, 1 이상 = 앞질러 감, -1 = 추격 안 함)과 목적지. 시각화용
        public int ChaseRank = -1;
        public Vector3 ChaseTarget;
    }
}
