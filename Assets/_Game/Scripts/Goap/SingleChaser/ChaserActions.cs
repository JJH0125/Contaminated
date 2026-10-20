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

            Vector3 target = ctx.Player.position;
            bool arrived = ctx.Locomotion.MoveTo(target, ctx.CatchRadius, ctx.Alert.Level);

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
    }

    /// <summary>
    /// 발견 신호 조사를 목표로 하는 첫 번째 Action
    /// </summary>
    public class MoveToSignal : GoapAction
    {
        public MoveToSignal()
        {
            Name = "MoveToSignal";
            Preconditions.Facts["heardSignal"] = true;
            Effects.Facts["atSignalLocation"] = true;
        }

        public override bool Perform(ChaserContext ctx)
        {
            Vector3 signalPosition = ctx.Blackboard.LastSignalPosition;
            bool arrived = ctx.Locomotion.MoveTo(signalPosition, ctx.ArriveRadius, ctx.Alert.Level);

            // 경로가 없을 경우의 방어 코드.
            if (!ctx.Locomotion.HasValidPath)
            {
                ctx.Blackboard.AddInvestigateCompleted(ctx.Blackboard.SignalSource);
                ctx.Blackboard.ClearSignal();
                return true;
            }
            return arrived;
        }
    }

    /// <summary>
    /// 발견 신 조사를 목표로 하는 두 번째 Action
    /// </summary>
    public class SearchSignalArea : GoapAction
    {
        private float _searchTimer;
        private const float SearchDuration = 3f;

        public SearchSignalArea()
        {
            Name = "SearchSignalArea";
            Preconditions.Facts["atSignalLocation"] = true;
            Effects.Facts["signalInvestigated"] = true;
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
                ctx.Blackboard.AddInvestigateCompleted(ctx.Blackboard.SignalSource);
                // 같은 제보를 다시 조사하지 않도록 제보 제거
                ctx.Blackboard.ClearSignal();
                // 완료 처리
                return true;
            }
            return false;
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
    }
}
