#if false // 다중 추격자 데모: 지금은 쓰지 않아 컴파일에서 뺀다. 다시 쓰려면 이 줄과 맨 아래 #endif를 지운다.
using System.Collections.Generic;
using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 추격자의 GOAP가 동작하는 데 필요한 여러 정보를
    /// 하나로 묶어 전달하는 정보 꾸러미.
    /// ChaserActions와 Planner가 이 정보들을 단위로 움직인다. 
    /// </summary>
    

    /// <summary>
    /// 추격자들의 사고회로. 각각의 추격자들은 다음을 수행한다.
    ///   1. 공유된 BlackBoard를 토대로 자신만의 WorldState를 설정
    ///   2. 달성할 수 있는 최우선의 목표를 설정
    ///   3. Planner에게 plan을 요청
    ///   4. 받은 plan을 따라 하나씩 실행
    ///
    /// "Personality" comes from the action list + costs you assign per chaser,
    /// NOT from different code. Same brain, different cost tables.
    /// </summary>
    public class SquadAgent : MonoBehaviour
    {
        [Header("Identity")]
        public int chaserId;
        [Tooltip("Aggressive / Ambusher / Searcher — drives which action costs are cheap.")]
        public ChaserPersonality personality = ChaserPersonality.Aggressive;

        [Header("References")]
        public Transform player;

        // 할 수 있는 행동들 (순서 없음)
        private List<GoapAction> _actions;
        // 목표에 따라 세워진 순차적인 행동들 (순서 있음)
        private Queue<GoapAction> _plan;
        // 현재 이 개체가 하고 있는 행동
        private GoapAction _currentAction;
        // 이 추격자가 가지고 있는 정보 꾸러미.
        private ChaserContext _ctx;

        // 디버그 시각화를 위한 코드
        public string CurrentGoalName { get; private set; } = "-";
        public string CurrentActionName => _currentAction?.Name ?? "-";

        private void Start()
        {
            _ctx = new ChaserContext
            {
                Agent = this,
                Blackboard = SquadBlackboard.Instance,
                Self = transform,
                Player = player
            };
            _actions = ActionFactory.BuildActions(personality);
        }

        private void Update()
        {
            // plan이 없거나 이미 끝냈고
            if (_plan == null || _plan.Count == 0)
            {
                // 지금 하는 행동도 없다면
                if (_currentAction == null)
                    Replan();
            }

            if (_currentAction != null)
            {
                bool done = _currentAction.Perform(_ctx);

                if (done)
                {
                    _currentAction = (_plan != null && _plan.Count > 0) ? _plan.Dequeue() : null;
                }
            }
        }

        /// <summary>
        /// 계획이 없거나 다 끝냈고
        /// 지금 하는 행동도 없다면 호출되는 함수
        /// </summary>
        private void Replan()
        {
            WorldState state = BuildWorldState();
            Goal goal = SelectGoal(state);
            CurrentGoalName = goal?.Name ?? "-";
            if (goal == null)
                return;

            _plan = GoapPlanner.Plan(_actions, state, goal, _ctx);
            _currentAction = (_plan != null && _plan.Count > 0)
            ? _plan.Dequeue() : null;
        }

        /// <summary>
        /// Current world state — note that several facts come straight from the
        /// shared blackboard. This is the mechanism by which one chaser's
        /// sighting changes another chaser's plan.
        /// </summary>
        private WorldState BuildWorldState()
        {
            var blackboard = _ctx.Blackboard;
            var state = new WorldState();
            state.Facts["playerVisible"]
            = blackboard.PlayerCurrentlyVisible;
            state.Facts["hasLastKnownPos"]
            = blackboard.TimeSinceLastSeen < Mathf.Infinity;
            state.Facts["alerted"] =
            blackboard.Alert != SquadBlackboard.AlertLevel.Calm;
            state.Facts["nearPlayer"] = player != null &&
                Vector3.Distance(transform.position, player.position) < 1.5f;
            state.Facts["playerDead"] = false;
            return state;
        }

        /// <summary>
        /// 아직 충족하지 못한 goal들 중
        /// 우선순위가 가장 높은 goal을 선택
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        private Goal SelectGoal(WorldState state)
        {
            Goal best = null;
            foreach (Goal goal in GoalSet.ForPersonality(personality))
            {
                // 이미 충족된 goal이라면 선택지에서 패스
                if (state.Matches(goal.Desired))
                    continue;
                // 선택된 goal이 없거나 이 goal보다 우선순위가 적다면
                // 이 goal을 선택
                if (best == null || goal.Priority > best.Priority)
                    best = goal;
            }
            return best;
        }
    }

    public enum ChaserPersonality { Aggressive, Ambusher, Searcher }
}
#endif
