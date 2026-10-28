using System.Collections.Generic;
using UnityEngine;

namespace Squad
{
    /// <summary>
    /// Actions for the three-goal chaser (catch / investigate sound / wander).
    /// 적의 goal은 추격/소리 조사/배회 3가지
    ///
    /// 습격 : ReachPlayer 행동을 통해 도달 가능
    /// ReachPlayer의 전제조건 : playerVisible=true
    ///               효과     : playerCaught=true
    /// 
    /// 소리 조사 : MoveToSound와 SerchSoundArea 행동을 차례로 행하여 도달 가능
    /// MoveToSound의 전제조건 : heardSound=true
    ///               효과     : atSoundLocation=true
    /// SearchSoundArea의 전제조건 : atSoundLocation=true
    ///                   효과     : soundInvestigated=true
    /// 
    /// 배회 : WanderStep을 통해 도달 가능
    /// WanderStep의 전제조건 : 없음
    ///              효과     : wandering=true
    ///
    /// 각 행동의 Perform 함수를 통해 적이 실제로 이동하고, 이동이 끝나면 true를 return한다
    /// Planner는 추상적인 개념인 facts들 사이에서만 관여할 뿐이고,
    /// 실제 월드에 영향을 미치는 것은 오직 Perform 뿐이다
    ///
    /// Movement here calls into the chaser's Locomotion helper (see
    /// ChaserLocomotion.cs), which wraps the A* Pathfinder so these actions
    /// don't need to know how pathfinding works.
    /// </summary>
    public static class ChaserActions
    {
        /// <summary>
        /// 적 개체가 할 수 있는 행동의 목록을 불러온다.
        /// </summary>
        /// <returns></returns>
        public static List<GoapAction> Build()
        {
            return new List<GoapAction>
            {
                // ChaserGoals와 달리 각 Action들이 서로 다른 실행 코드를 가지고 있어
                // GoapAction과 상속 구조를 띄기 때문에,
                // 자식 클래스들의 인스턴스를 생성하여 List에 담아 반환한다.

                // 습격
                new ReachPlayer(),
                // 발견 신호 조사
                new MoveToSignal(),
                new SearchSignalArea(),
                // 들은 소리 조사
                new MoveToSound(),
                new SearchSoundArea(),
                // 배회
                new WanderStep()
            };
        }
    }
    
    /// <summary>
    /// 습격을 목표로 하는 Action
    /// </summary>
    public class ReachPlayer : GoapAction
    {
        public ReachPlayer()
        {
            Name = "ReachPlayer";
            Preconditions.Facts["playerVisible"] = true;
            Effects.Facts["playerCaught"] = true;
        }

        public override float GetCost(ChaserContext ctx) => 1f;

        public override bool Perform(ChaserContext ctx)
        {
            // 추격할 플레이어가 없다면 true를 return하여 상태를 빠져나온다
            if (ctx.Player == null)
                return true;

            Vector3 target = ChooseTarget(ctx);
            bool arrived = ctx.Locomotion.MoveTo(target, ctx.CatchRadius, ctx.Alert.Level);

            // 앞질러 가는 목적지에 도착한 것은 잡은 것이 아니다. 실제로 플레이어 곁에 있을 때만 잡는다.
            if (arrived)
            {
                Vector3 toPlayer = ctx.Player.position - ctx.Self.position;
                toPlayer.y = 0f;
                arrived = toPlayer.magnitude <= ctx.CatchRadius;
            }

            /// 잡았다는 것은 플레이어에게 충분히 접근했고 동시에 플레이어가 보이는 상황
            /// 만약 적과 플레이어가 서로 다른 차원에 있다면,
            /// 플레이어와 아무리 가까워도 playerVisible이 이미 false이기 때문에
            /// 다른 Plan을 재설계할 것이다.
            
            /// 도달했다 = 게임 오버
            /// 잡힌 연출(Jumpscare)이 붙은 적이면 연출을 먼저 보여주고
            /// 연출이 끝날 때 게임 오버가 된다.
            if (arrived)
            {
                // 계획은 일정한 간격으로만 다시 세우므로, 플레이어가 방금 다른 차원으로 넘어갔어도
                // 다음 계획 전까지는 이 행동이 계속 실행된다. 그 사이에 닿았다고 잡아 버리지 않도록,
                // 잡는 순간에 지금도 감지할 수 있는 상대인지 한 번 더 확인한다.
                DimensionController dimensions = DimensionController.Instance;
                if (dimensions != null && !dimensions.CanDetectPlayer(ctx.Member))
                    return true;

                Jumpscare jumpscare = ctx.Self.GetComponent<Jumpscare>();
                if (jumpscare != null)
                    jumpscare.Play(ctx.Player);
                else
                    GameManager.Instance.GameOver();
                return true;
            }

            return false;
        }

        /// <summary>
        /// 추격 겹침: 여러 적이 함께 플레이어를 보고 있으면 같은 길로 줄지어 쫓지 않게 역할을 나눈다.
        ///   플레이어와 가장 가까운 적(순위 0)   플레이어에게 곧장 간다
        ///   나머지(순위 1, 2, …)               몇 초 뒤 플레이어가 있을 자리로 앞질러 간다.
        ///                                      순위마다 좌우로 번갈아 벌려(+1, -1, +2, -2칸) 서로 다른 쪽에서 다가온다
        /// 순위는 매 프레임 거리로 다시 정하므로, 곧장 쫓던 적이 뒤처지면 더 가까운 적이 그 역할을 넘겨받는다.
        /// 플레이어가 가까이 있거나, 멈춰 있거나, 앞질러 갈 자리가 막혀 있으면 곧장 간다.
        /// </summary>
        private static Vector3 ChooseTarget(ChaserContext ctx)
        {
            SquadBlackboard bb = ctx.Blackboard;
            Vector3 playerPos = ctx.Player.position;
            ctx.ChaseRank = bb != null ? bb.ChaseRank(ctx.Member, playerPos) : -1;
            ctx.ChaseTarget = playerPos;

            if (ctx.ChaseRank <= 0)
                return playerPos;

            Vector3 toPlayer = playerPos - ctx.Self.position;
            toPlayer.y = 0f;
            Vector3 direction = bb.LastPlayerDirection;
            float lead = bb.LastPlayerSpeed * ctx.InterceptLeadTime;
            if (toPlayer.magnitude <= ctx.InterceptDirectRadius || direction == Vector3.zero || lead < 1f)
                return playerPos;

            // 순위 1 → 오른쪽 1칸, 2 → 왼쪽 1칸, 3 → 오른쪽 2칸, …
            int slot = (ctx.ChaseRank + 1) / 2;
            float side = ctx.ChaseRank % 2 == 1 ? 1f : -1f;
            Vector3 right = Vector3.Cross(Vector3.up, direction);
            Vector3 ahead = playerPos + direction * lead;
            Vector3 spread = ahead + right * (side * slot * ctx.InterceptSpacing);

            // 벌린 자리가 막혀 있으면 벌리지 않은 자리, 그것도 막혀 있으면 곧장 간다.
            Vector3 target = ctx.Locomotion.IsWalkable(spread) ? spread
                           : ctx.Locomotion.IsWalkable(ahead) ? ahead
                           : playerPos;
            ctx.ChaseTarget = target;
            return target;
        }
    }

    /// <summary>
    /// 발견 신호 조사를 목표로 하는 첫 번째 Action.
    /// 다른 적이 플레이어를 본 마지막 자리(블랙보드의 LastSightingPosition)로 간다.
    /// 목적지를 매 프레임 다시 읽으므로, 이동하는 동안 그 적이 계속 쫓으며 위치가 바뀌면 따라간다.
    ///
    /// 이 사건에서 처음 도착한 적이 수색 지도를 만든다. 지도가 이미 있으면(다른 적이 먼저 도착했으면)
    /// 발견 지점까지 갈 필요 없이 바로 수색 지점을 맡으러 간다.
    /// </summary>
    public class MoveToSignal : GoapAction
    {
        // 발견 지점에 이만큼(m) 가까워지면 도착한 것으로 본다.
        private const float SignalArriveRadius = 5f;

        public MoveToSignal()
        {
            Name = "MoveToSignal";
            Preconditions.Facts["joinedSignal"] = true;
            Effects.Facts["atSignalLocation"] = true;
        }

        public override bool Perform(ChaserContext ctx)
        {
            SquadBlackboard bb = ctx.Blackboard;
            if (bb.SearchPlanSpot == ctx.ProcessingSpotCount)
                return true;

            Vector3 target = bb.LastSightingPosition;
            bool arrived = ctx.Locomotion.MoveTo(target, SignalArriveRadius, ctx.Alert.Level);

            // 갈 수 있는 길이 없으면 합류를 풀고 끝낸다.
            if (!ctx.Locomotion.HasValidPath)
            {
                ctx.Joined = false;
                bb.ReleaseSearchPoints(ctx.Self.GetInstanceID());
                return true;
            }

            if (arrived)
                bb.EnsureSearchPlan(ctx.ProcessingSpotCount, target, bb.LastPlayerDirection);
            return arrived;
        }
    }

    /// <summary>
    /// 발견 신호 조사를 목표로 하는 두 번째 Action. 수색 지도의 지점을 다른 적들과 나눠 맡아 수색한다.
    ///   1. 아무도 맡지 않은 지점 중 가장 가까운 곳을 맡는다
    ///   2. 그곳으로 가서 잠깐 둘러본 뒤 "확인함"으로 표시하고 반납한다
    ///   3. 다음 지점을 맡는다. 남은 지점이 없으면 합류를 풀고 끝낸다
    /// 이동하다가 다른 지점 근처를 지나가면 그 지점도 본 것으로 친다.
    /// 합류를 풀면 경계 타이머가 다시 흘러서, 한동안 Suspicious로 돌아다니다 Calm으로 내려간다.
    /// </summary>
    public class SearchSignalArea : GoapAction
    {
        // 지금 맡은 지점과, 그 지점이 어느 사건의 지도에 있는 것인가
        private SquadBlackboard.SearchPoint _point;
        private int _pointSpot;
        private bool _lookingAround;
        private float _lookTimer;

        // 수색 지점에 이만큼(m) 가까워지면 도착한 것으로 본다.
        private const float PointArriveRadius = 2.5f;
        // 수색 지점에서 둘러보는 시간(초)
        private const float LookDuration = 1.5f;
        // 이 거리(m) 안을 지나가면 그 지점도 본 것으로 친다.
        private const float PassBySeeRadius = 6f;

        public SearchSignalArea()
        {
            Name = "SearchSignalArea";
            Preconditions.Facts["atSignalLocation"] = true;
            Effects.Facts["signalInvestigated"] = true;
        }

        public override bool Perform(ChaserContext ctx)
        {
            SquadBlackboard bb = ctx.Blackboard;
            int id = ctx.Self.GetInstanceID();

            // 새 사건으로 지도가 바뀌었으면 예전 지도의 지점은 버린다.
            if (_point != null && _pointSpot != bb.SearchPlanSpot)
                ResetPoint();

            if (_point == null)
            {
                _point = bb.ClaimSearchPoint(id, ctx.Self.position);
                _pointSpot = bb.SearchPlanSpot;
                if (_point == null)
                {
                    // 남은 지점이 없다: 수색 끝
                    ctx.Joined = false;
                    bb.ReleaseSearchPoints(id);
                    return true;
                }
            }

            bb.MarkSearchPointsSeen(ctx.Self.position, PassBySeeRadius, id);

            if (!_lookingAround)
            {
                bool arrived = ctx.Locomotion.MoveTo(_point.Position, PointArriveRadius, ctx.Alert.Level);
                if (!ctx.Locomotion.HasValidPath)
                {
                    // 갈 수 없는 지점은 확인한 것으로 치고 다음으로
                    bb.CompleteSearchPoint(_point);
                    ResetPoint();
                    return false;
                }
                if (arrived)
                {
                    _lookingAround = true;
                    _lookTimer = 0f;
                }
                return false;
            }

            ctx.Locomotion.LookAround();
            _lookTimer += Time.deltaTime;
            if (_lookTimer >= LookDuration)
            {
                bb.CompleteSearchPoint(_point);
                ResetPoint();
            }
            return false;
        }

        private void ResetPoint()
        {
            _point = null;
            _lookingAround = false;
            _lookTimer = 0f;
        }
    }

    /// <summary>
    /// 소리 조사를 목표로 하는 첫 번째 Action
    /// </summary>
    public class MoveToSound : GoapAction
    {
        public MoveToSound()
        {
            Name = "MoveToSound";
            Preconditions.Facts["heardSound"] = true;
            Effects.Facts["atSoundLocation"] = true;
        }

        public override bool Perform(ChaserContext ctx)
        {
            Vector3 soundPosition = ctx.Blackboard.LastSoundPosition;
            bool arrived = ctx.Locomotion.MoveTo(soundPosition, ctx.ArriveRadius, ctx.Alert.Level);

            // 경로가 없을 경우의 방어 코드.
            if (!ctx.Locomotion.HasValidPath)
            {
                ctx.Blackboard.AddInvestigateCompleted(ctx.Blackboard.SoundSource);
                ctx.Blackboard.ClearSound();
                return true;
            }
            return arrived;
        }
    }

    /// <summary>
    /// 소리 조사를 목표로 하는 두 번째 Action
    /// </summary>
    public class SearchSoundArea : GoapAction
    {
        private float _searchTimer;
        private const float SearchDuration = 3f;

        public SearchSoundArea()
        {
            Name = "SearchSoundArea";
            Preconditions.Facts["atSoundLocation"] = true;
            Effects.Facts["soundInvestigated"] = true;
        }

        public override bool Perform(ChaserContext ctx)
        {
            // 검색에 든 시간 추가
            _searchTimer += Time.deltaTime;
            ctx.Locomotion.LookAround();

            // 검색을 충분히 했다면
            if (_searchTimer >= SearchDuration)
            {
                // 검색에 든 시간 초기화
                _searchTimer = 0f;
                // 조사 완료 목록 추가
                ctx.Blackboard.AddInvestigateCompleted(ctx.Blackboard.SoundSource);
                // 같은 소리를 다시 조사하지 않도록 소리 제거
                ctx.Blackboard.ClearSound();
                // 완료 처리
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// 배회를 목표로 하는 Action
    /// </summary>
    public class WanderStep : GoapAction
    {
        // 도착 후 다음 배회를 시작하기 전 대기 중인지, 대기 시간이 얼마나 지났는지,
        // 그리고 이번에 대기할 시간(매번 랜덤하게 새로 뽑음).
        private bool _pausing;
        private float _pauseTimer;
        private float _rewanderInterval;

        public WanderStep()
        {
            Name = "WanderStep";
            // 전제조건 없음
            Effects.Facts["wandering"] = true;
        }

        public override bool Perform(ChaserContext ctx)
        {
            // 도착 직후 대기 중이라면 이동하지 않고 타이머만 진행시킨다.
            // 대기가 끝나야 비로소 완료 처리하여 다음 배회 지점으로 넘어간다.
            if (_pausing)
            {
                ctx.Locomotion.LookAround();
                _pauseTimer -= Time.deltaTime;
                if (_pauseTimer <= 0f)
                {
                    _pausing = false;
                    return true;
                }
                    return false;
            }

            // Ask locomotion for a wander target if we don't have one yet, then
            // walk to it. Completing one leg satisfies "wandering" for this
            // planning cycle; the next replan will simply wander again unless a
            // higher-priority goal (chase / investigate) has become available.
            if (!ctx.Locomotion.HasWanderTarget)
                ChooseWanderTarget(ctx);
            Vector3 target = ctx.Locomotion.GetWanderTarget();
            bool arrived = ctx.Locomotion.MoveTo(target, ctx.ArriveRadius, ctx.Alert.Level);
            // 마찬가지로 배회 장소에 도착하면
            // 또 다시 배회하지 않도록 장소 제거
            if (arrived || !ctx.Locomotion.HasValidPath)
            {
                ctx.Locomotion.ClearWanderTarget();
                // 도착했다면 바로 완료 처리하지 않고 대기 단계로 넘어간다.
                if (arrived)
                {
                    _pausing = true;
                    _pauseTimer = Random.Range(ctx.WanderPauseMin, ctx.WanderPauseMax);
                }
            }
            return false;
        }

        /// <summary>
        /// 다음에 배회할 곳을 고른다. 아무것도 정하지 않으면 혼자 다닐 때처럼 지금 자리 둘레의 무작위 자리로 간다.
        ///   켜진 불에 끌림   켜진 모닥불이 있으면 정해 둔 확률(fireAttraction)로 그 불 둘레로 간다 (Fake 적)
        ///   무리를 따라감   무리의 졸개는 우두머리의 목적지 둘레에서 자기 몫의 자리로 간다 (Real 적)
        /// </summary>
        private static void ChooseWanderTarget(ChaserContext ctx)
        {
            EnemyTuning tuning = GameTuning.Instance.EnemyFor(ctx.Member);

            if (tuning.fireAttraction > 0f && Random.value < tuning.fireAttraction && TryFireSpot(ctx, tuning, out Vector3 fireSpot))
            {
                ctx.Locomotion.SetWanderTarget(fireSpot);
                return;
            }

            ChaserPack pack = ctx.Pack;
            HorrorChaserAgent leader = pack != null ? pack.Leader : null;
            if (leader == null || leader.transform == ctx.Self)
                return;

            // 우두머리가 가는 곳(없으면 우두머리가 있는 곳) 둘레에서, 졸개마다 다른 방향의 자리를 맡는다.
            Vector3 center = leader.Locomotion.HasWanderTarget ? leader.Locomotion.WanderTarget : leader.transform.position;
            int slot = Mathf.Max(1, pack.SlotOf(ctx.Self.GetComponent<HorrorChaserAgent>()));
            for (int i = 0; i < 6; i++)
            {
                float angle = slot * 137.5f + i * 45f + Random.Range(-20f, 20f);
                Vector3 spot = center + ZoneMap.Direction(angle) * tuning.packSpread;
                if (ctx.Locomotion.IsWalkable(spot))
                {
                    ctx.Locomotion.SetWanderTarget(spot);
                    return;
                }
            }
            ctx.Locomotion.SetWanderTarget(center);
        }

        // 켜진 모닥불 하나를 골라 그 둘레의 걸을 수 있는 자리를 찾는다.
        private static bool TryFireSpot(ChaserContext ctx, EnemyTuning tuning, out Vector3 spot)
        {
            spot = Vector3.zero;
            var lit = new List<Campfire>();
            foreach (Campfire campfire in Campfire.All)
                if (campfire != null && campfire.IsLit)
                    lit.Add(campfire);
            if (lit.Count == 0)
                return false;

            Vector3 fire = lit[Random.Range(0, lit.Count)].transform.position;
            for (int i = 0; i < 8; i++)
            {
                spot = fire + ZoneMap.Direction(Random.Range(0f, 360f)) * Random.Range(4f, tuning.fireGatherRadius);
                if (ctx.Locomotion.IsWalkable(spot))
                    return true;
            }
            return false;
        }
    }
}
