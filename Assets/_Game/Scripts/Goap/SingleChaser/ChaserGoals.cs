using System.Collections.Generic;

namespace Squad
{
    /// <summary>
    /// The three goals for a horror-game chaser that CANNOT be fought:
    ///
    ///   1. CatchPlayer      — highest priority. Fires when the player is
    ///                          detected (seen, or a fresh last-known position).
    ///   2. InvestigateSound — middle priority. Fires when a sound (e.g. a
    ///                          generator being switched on) has been reported
    ///                          but the player isn't currently detected. The
    ///                          chaser goes to the SOUND'S location to check it
    ///                          out (interpretation A).
    ///   3. Wander           — the fallback. Always achievable, lowest priority,
    ///                          so it only runs when nothing else applies.
    ///
    /// There is deliberately NO "survive" goal: the enemy can't be beaten, so it
    /// never retreats. This keeps the whole decision space to "how do I find and
    /// reach the player", which is exactly the tension a stealth-horror chaser
    /// should have.
    ///
    /// Goal selection (in SquadAgent.SelectGoal) picks the highest-priority goal
    /// whose desired state is NOT already satisfied. Because Wander's desired
    /// fact is never "already true", it's always available as a floor — but its
    /// low priority means chase/investigate win whenever their facts are set.
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
