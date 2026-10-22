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

        // 수치는 GameTuning 에셋의 적 항목(Real 적 / Fake 적)에서 이 적의 차원에 맞는 것을 읽는다.
        private EnemyTuning Tuning => GameTuning.Instance.EnemyFor(enemy);
        private float catchRadius => Tuning.catchRadius;
        private float arriveRadius => Tuning.arriveRadius;
        private float replanInterval => Tuning.replanInterval;
        private float wanderPauseMin => Tuning.wanderPauseMin;
        private float wanderPauseMax => Tuning.wanderPauseMax;

        // 발견 신호 (포효)
        private float roarRadius => Tuning.roarRadius;
        private float joinTimeout => Tuning.joinTimeout;

        // 추격 겹침 (여러 적이 함께 볼 때)
        private float interceptLeadTime => Tuning.interceptLeadTime;
        private float interceptSpacing => Tuning.interceptSpacing;
        private float interceptDirectRadius => Tuning.interceptDirectRadius;

        // 씬에 있는 모든 추격자. 포효할 때 받을 적을 고르는 데 쓴다.
        private static readonly List<HorrorChaserAgent> _all = new();

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
                Joined = false,
                ProcessingSpotCount = 0,
                InterceptLeadTime = interceptLeadTime,
                InterceptSpacing = interceptSpacing,
                InterceptDirectRadius = interceptDirectRadius
            };

            // Action과 Goal 불러오기
            _actions = ChaserActions.Build();
            _goals = ChaserGoals.Build();
        }

        private void OnEnable() => _all.Add(this);

        private void OnDisable() => _all.Remove(this);

        /// <summary>
        /// 발견 신호(포효)를 보낸다. 이 적이 아무도 보지 않던 플레이어를 새로 발견했을 때 VisionCensor가 부른다.
        /// 받는 적: 같은 차원, roarRadius 안, 자기 자신이 아님, 지금 플레이어를 보고 있지 않음.
        /// (지금 보고 있는 적은 이미 자기 눈으로 추격 중이므로 부를 필요가 없다)
        /// </summary>
        public void Roar()
        {
            SquadBlackboard bb = SquadBlackboard.Instance;
            if (bb == null || enemy == null)
                return;

            foreach (HorrorChaserAgent other in _all)
            {
                if (other == this || other.enemy == null || other.enemy.Dimension != enemy.Dimension)
                    continue;
                if (bb.IsSeeingPlayerNow(other.enemy))
                    continue;

                Vector3 offset = other.transform.position - transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude <= roarRadius * roarRadius)
                    other.ReceiveSignal(bb.spotCount);
            }
        }

        /// <summary>
        /// 다른 적의 발견 신호를 받았을 때. 합류해서 발견 지점으로 가 수색한다.
        /// 직접 본 게 아니므로 경계는 Suspicious까지만 올린다. (Alerted는 플레이어를 직접 본 적에게만)
        /// </summary>
        public void ReceiveSignal(int spotNumber)
        {
            if (_ctx == null)
                return;

            _ctx.Joined = true;
            _ctx.ProcessingSpotCount = spotNumber;
            if (_ctx.Alert != null)
                _ctx.Alert.RaiseAlert(AlertLevel.Suspicious);

            // 하던 일을 멈추고 바로 다시 계획을 세운다.
            ReplanNow();
        }

        private void ReplanNow()
        {
            Replan();
            _replanTimer = replanInterval;
        }

        /// <summary>
        /// 합류한 동안 매 프레임 확인하는 것.
        ///   - 경계가 Calm으로 내려가지 않게 Suspicious를 유지한다 (같은 단계로 올리면 타이머만 0이 된다)
        ///   - 같은 차원에서 새 사건(플레이어를 다시 발견)이 생기면 번호를 새로 적고 그곳으로 다시 간다
        ///   - 아무도 오랫동안(joinTimeout) 플레이어를 못 보면 합류를 풀고 돌아간다
        /// 합류를 풀면 경계 타이머가 다시 흘러서, 한동안 Suspicious로 돌아다니다 Calm으로 내려간다.
        /// </summary>
        private void UpdateJoined()
        {
            if (!_ctx.Joined)
                return;

            SquadBlackboard bb = _ctx.Blackboard;
            if (_ctx.Alert != null)
                _ctx.Alert.RaiseAlert(AlertLevel.Suspicious);

            if (bb.spotCount > _ctx.ProcessingSpotCount && bb.SpotDimension == enemy.Dimension)
            {
                _ctx.ProcessingSpotCount = bb.spotCount;
                ReplanNow();
                return;
            }

            if (!bb.PlayerCurrentlyVisible && bb.TimeSinceLastSeen > joinTimeout)
            {
                _ctx.Joined = false;
                bb.ReleaseSearchPoints(transform.GetInstanceID());
                ReplanNow();
            }
        }

        private void Update()
        {
            UpdateJoined();

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
        /// 10.20 수정: 같은 차원이면 맵의 반대편에서 적이 플레이어를 발견할 경우 곧장 추격을 시작하는 버그를 수정.
        private WorldState BuildWorldState()
        {
            var bb = _ctx.Blackboard;
            var s = new WorldState();

            // Catch-goal facts.
            s.Facts["playerVisible"] = bb.IsSeeingPlayerNow(enemy);
            s.Facts["playerCaught"] = false;

            // 발견 신호 조사(InvestigateSignal) 관련 사실.
            // 다른 적의 신호를 받고 합류했는가. 합류는 ReceiveSignal에서 켜고, 수색을 끝내거나 시간이 지나면 끈다.
            s.Facts["joinedSignal"] = _ctx.Joined;
            s.Facts["atSignalLocation"] = false;
            s.Facts["signalInvestigated"] = false;

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
                // 선택할 이유가 없는 (도달 불가능한) Goal은 패스
                if (!GoalIsRelevant(g, state))
                    continue;
                // 우선순위 비교하여 높은 우선순위의 Goal로 갱신
                if (best == null || g.Priority > best.Priority)
                    best = g;
            }
            return best;
        }

        /// <summary>
        /// 선택할 이유가 있는 Goal인가?
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
                case "InvestigateSignal":
                    return Fact(s, "joinedSignal");
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

            // 발견 신호를 받고 합류한 적: 주황색 선으로 발견 지점과 연결
            // 발견 지점: 빨간 구, 그때 플레이어가 움직이던 방향: 빨간 선
            if (_ctx != null && _ctx.Blackboard != null && _ctx.Blackboard.spotCount > 0)
            {
                Vector3 spot = _ctx.Blackboard.LastSightingPosition;
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(spot, 1.5f);
                Gizmos.DrawLine(spot, spot + _ctx.Blackboard.LastPlayerDirection * 8f);

                if (_ctx.Joined)
                {
                    Gizmos.color = new Color(1f, 0.5f, 0f);
                    Gizmos.DrawLine(transform.position + Vector3.up * 2f, spot);
                }
            }

            // 수색 지점: 노랑 = 아직 아무도 안 맡음, 주황 = 누군가 맡음(이 적이 맡았으면 선으로 연결), 초록 = 확인함
            if (_ctx != null && _ctx.Blackboard != null)
            {
                int id = transform.GetInstanceID();
                foreach (SquadBlackboard.SearchPoint point in _ctx.Blackboard.SearchPoints)
                {
                    Gizmos.color = point.Checked ? Color.green : point.Owner != 0 ? new Color(1f, 0.5f, 0f) : Color.yellow;
                    Gizmos.DrawWireCube(point.Position + Vector3.up, Vector3.one * 2f);
                    if (point.Owner == id && !point.Checked)
                        Gizmos.DrawLine(transform.position + Vector3.up * 2f, point.Position + Vector3.up);
                }
            }

            // 추격 역할: 곧장 쫓는 적은 빨간 선, 앞질러 가는 적은 자홍색 선과 목적지 구
            if (_ctx != null && _ctx.ChaseRank >= 0 && CurrentActionName == "ReachPlayer")
            {
                Gizmos.color = _ctx.ChaseRank == 0 ? Color.red : Color.magenta;
                Gizmos.DrawLine(transform.position + Vector3.up * 3f, _ctx.ChaseTarget + Vector3.up * 3f);
                if (_ctx.ChaseRank > 0)
                    Gizmos.DrawWireSphere(_ctx.ChaseTarget + Vector3.up, 1.2f);
            }

            // 포효가 닿는 범위
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, roarRadius);
        }
    }
}
