using System.Collections.Generic;

namespace Squad
{
    /// <summary>
    /// 적의 기본 목표 네 가지. 목표는 실행 코드가 없는 데이터라 상속하지 않고 값만 채운다.
    ///   CatchPlayer (100)        직접 본 플레이어를 잡는다 (playerCaught)
    ///   InvestigateSignal (75)   발견 신호를 받고 합류해 수색한다 (signalInvestigated)
    ///   InvestigateSound (50)    들은 소리의 자리를 조사한다 (soundInvestigated)
    ///   Wander (1)               할 일이 없을 때 배회한다. 이 사실은 늘 거짓이라 언제나 마지막 선택지로 남는다
    /// 우선순위 사이에 간격을 두어, 목표 부품(GoapModule)의 목표를 사이에 끼울 수 있다. (예: ExtinguishFire 60)
    ///
    /// 적은 싸워서 이길 수 없으므로 도망 목표는 일부러 두지 않았다.
    /// 목표를 고르는 일은 HorrorChaserAgent.SelectGoal이 한다: 아직 이루지 않았고 고를 이유가 있는 것 중 우선순위가 가장 높은 것.
    /// </summary>
    public static class ChaserGoals
    {
        // 우선순위가 더 높은 목표를 향해 행동.
        public const float PriorityCatch = 100f;
        public const float PriorityInvestigateSignal = 75f;
        public const float PriorityInvestigateSound = 50f;
        public const float PriorityWander = 1f;

        /// <summary>
        /// 각 Goal들의 이름과 우선순위를 정하고,
        /// 어떤 것이 true여야 하는지 조건을 넣는다
        /// 따라서 각 Facts들은
        /// 그 조건 이외에 다른 것은 아무것도 없는 Dictionary이다.
        /// </summary>
        public static List<Goal> Build()
        {
            // ChaserActions와 달리 Goal들은 상속 구조를 띌 필요가 없기 때문에
            // 같은 Goal 클래스의 인스턴스를 여럿 생성한 후
            // 각기 다른 값들을 채워 List에 담아 반환한다.
            var catchPlayer = new Goal { Name = "CatchPlayer", Priority = PriorityCatch };
            catchPlayer.Desired.Facts["playerCaught"] = true;

            var investigateSignal = new Goal { Name = "InvestigateSignal", Priority = PriorityInvestigateSignal };
            investigateSignal.Desired.Facts["signalInvestigated"] = true;

            var investigateSound = new Goal { Name = "InvestigateSound", Priority = PriorityInvestigateSound };
            investigateSound.Desired.Facts["soundInvestigated"] = true;

            var wander = new Goal { Name = "Wander", Priority = PriorityWander };
            wander.Desired.Facts["wandering"] = true;

            return new List<Goal> { catchPlayer, investigateSignal, investigateSound, wander };
        }
    }
}
