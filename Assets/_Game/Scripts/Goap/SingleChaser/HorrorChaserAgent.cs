using System.Collections.Generic;
using UnityEngine;

namespace Squad
{
    /// <summary>
    /// A horror-game chaser driven by GOAP with exactly three goals:
    ///   CatchPlayer  (priority 100) — reach the player when detected
    ///   InvestigateSound (50)       — walk to a heard sound and search it
    ///   Wander (1)                  — fallback when there's nothing to chase
    ///
    /// The enemy cannot be fought, so there is no survive/retreat goal. Goal
    /// selection each cycle picks the highest-priority goal whose desired fact
    /// isn't already satisfied; the planner then chains actions to reach it.
    ///
    /// This reuses the exact same GoapPlanner (A* over world states) as the
    /// squad demo — only the goal set and action list differ.
    /// 
    /// 이 스크립트를 오브젝트에 추가하면
    /// ChaserLocomotion도 자동으로 추가된다
    /// ChaserLocomotion이 있어야 작동하는 HorrorChaserAgent에 있어서
    /// 에러 방지 역할을 함.
    /// </summary>
    [RequireComponent(typeof(ChaserLocomotion))]
    public class HorrorChaserAgent : MonoBehaviour
    {
        [Header("■ 필수 연결 — 비워두면 에러")]
        [Tooltip("플레이어")]
        [SerializeField] private Transform player;

        [Header("○ 튜닝 값 — 자유롭게 조절")]
        [Tooltip("플레이어를 게임오버 시키는 거리")]
        [SerializeField] private float catchRadius = 3.0f;
        [Tooltip("소리/배회 target에 접근하여 행동을 시작하는 거리")]
        [SerializeField] private float arriveRadius = 0.6f;
        [Tooltip("replan 하는 주기")]
        [SerializeField] private float replanInterval = 0.4f;
        [Tooltip("다음 Wander까지 대기 시간 (최소)")]
        [SerializeField] private float wanderPauseMin = 1.5f;
        [Tooltip("다음 Wander까지 대기 시간 (최대)")]
        [SerializeField] private float wanderPauseMax = 4f;

        public ChaserLocomotion Locomotion { get; private set; }

        // Exposed for on-screen debug visualization (see the gizmo section).
        public string CurrentGoalName { get; private set; } = "-";
        public string CurrentActionName => _currentAction?.Name ?? "-";

        private List<GoapAction> _actions;
        private List<Goal> _goals;
        private Queue<GoapAction> _plan;
        private GoapAction _currentAction;
        private ChaserContext _ctx;
        private float _replanTimer;
        private DimensionMember enemy;

        private void Awake()
        {
            Locomotion = GetComponent<ChaserLocomotion>();
            enemy = GetComponent<DimensionMember>();
        }

        private void Start()
        {
            /// 인스펙터에서 설정된 값을 토대로, 자신의 문서를 만든다.
            _ctx = new ChaserContext
            {
                Blackboard = SquadBlackboard.Instance,
                Self = transform,
                Player = player,
                Member = enemy,
                Alert = GetComponent<ChaserAlert>(),
                Locomotion = Locomotion,
                CatchRadius = catchRadius,
                ArriveRadius = arriveRadius,
                WanderPauseMin = wanderPauseMin,
                WanderPauseMax = wanderPauseMax,
            };

            // Action과 Goal 불러오기
            _actions = ChaserActions.Build();
            _goals = ChaserGoals.Build();
        }

        private void Update()
        {
            _replanTimer -= Time.deltaTime;

            // replan 주기가 돌았거나
            // plan을 세운 적이 없거나 모두 수행했고 수행할 Action이 없으면
            // replan하고 타이머를 초기화
            if (_replanTimer <= 0f || (_plan == null || _plan.Count == 0) && _currentAction == null)
            {
                Replan();
                _replanTimer = replanInterval;
            }

            // 행동을 완료했으면 다음 행동으로
            if (_currentAction != null)
            {
                bool done = _currentAction.Perform(_ctx);
                if (done)
                    DoNextAction();
            }
        }

        private void Replan()
        {
            WorldState state = BuildWorldState();
            Goal goal = SelectGoal(state);
            CurrentGoalName = goal?.Name ?? "-";
            if (goal == null)
            {
                _plan = null;
                _currentAction = null;
                return;
            }

            _plan = GoapPlanner.Plan(_actions, state, goal, _ctx);
            DoNextAction();
        }

        /// 현재 추격자 입장에서의 WorldState 생성
        /// 존재할 수 있는 Key를 모두 생성한다.
        /// playerVisible과 heardSound는 블랙보드에서 가져오고, 나머지는 false로 초기화한다.
        ///
        /// 9.6 수정: 블랙보드에서 가져온 시야와 소리 정보를 곧이곧대로 읽지 말고
        /// 이 추격자 또한 보고 들을 수 있었던 정보인지를 한번 검사함으로써
        /// 날 보지도 못하는 추격자가 날 인식하게 되는 버그를 수정.
        private WorldState BuildWorldState()
        {
            var bb = _ctx.Blackboard;
            var s = new WorldState();

            // Catch-goal facts.
            s.Facts["playerVisible"] = bb.PlayerCurrentlyVisible && CanSeePlayerNow();
            s.Facts["playerCaught"] = false;

            // Investigate-goal facts.
            s.Facts["heardSound"] = bb.HasSound && CanHearSoundNow();
            s.Facts["atSoundLocation"] = false;
            s.Facts["soundInvestigated"] = false;

            // Wander is always achievable; its fact starts false so the goal is
            // never "already satisfied" and thus always available as a floor.
            s.Facts["wandering"] = false;
            return s;
        }

        /// <summary>달성하지 않은 Goal 중 가장 우선순위 높은 Goal 선택</summary>
        private Goal SelectGoal(WorldState state)
        {
            Goal best = null;
            foreach (Goal g in _goals)
            {
                // 이미 이룬 Goal은 패스
                if (state.Matches(g.Desired))
                    continue;
                // 관련 없는 Goal은 패스
                if (!GoalIsRelevant(g, state))
                    continue;
                // 우선순위 비교하여 높은 우선순위의 Goal로 갱신
                if (best == null || g.Priority > best.Priority)
                    best = g;
            }
            return best;
        }

        /// <summary>
        /// 좇을 이유가 있는 Goal인가?
        /// Player가 보이지 않는 상황에서 CatchPlayer를 목표로 잡아봤자
        /// 어차피 달성하지 못하기 때문에
        /// 이런 상황의 Goal이 선택되는 걸 사전에 방지하는 필터
        /// </summary>
        private bool GoalIsRelevant(Goal g, WorldState s)
        {
            switch (g.Name)
            {
                case "CatchPlayer":
                    return Fact(s, "playerVisible");
                case "InvestigateSound":
                    return Fact(s, "heardSound");
                case "Wander": // State에 관계없이 항상 이유 있는 행동
                default:
                    return true;
            }
        }

        /// <summary>
        /// GoalIsRelevant 함수를 보조하는 함수.
        /// </summary>
        private static bool Fact(WorldState s, string key)
            => s.Facts.TryGetValue(key, out bool v) && v;

        /// <summary>
        /// Update, Replan 함수를 보조하는 함수.
        /// _plan Queue에서 하나를 뽑아 수행할 Action에 저장
        /// </summary>
        private void DoNextAction() => _currentAction =
        (_plan != null && _plan.Count > 0) ? _plan.Dequeue() : null;

        /// BuildWorldState 함수를 보조하는 함수 (시야)
        private bool CanSeePlayerNow()
        {
            if (DimensionController.Instance != null && enemy != null)
                return DimensionController.Instance.CanDetectPlayer(enemy);

            return false;
        }

        /// BuildWorldState 함수를 보조하는 함수 (소리)
        private bool CanHearSoundNow()
        {
            if (_ctx.Blackboard.SoundCanCrossDimension)
                return true;
            else if (_ctx.Blackboard.SoundDimension == enemy.Dimension)
                return true;

            return false;
        }

        // 시각화 함수
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying)
                return;

            // 적에게 잡히면 게임 오버되는 반경을 노란 구 형태로 표시
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, catchRadius);

            // 소리가 발생한 곳을 시안색 구로 표시, 적과 선으로 연결
            if (_ctx != null && _ctx.Blackboard != null && _ctx.Blackboard.HasSound)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(_ctx.Blackboard.LastSoundPosition, 0.4f);
                Gizmos.DrawLine(transform.position, _ctx.Blackboard.LastSoundPosition);
            }
        }
    }
}
