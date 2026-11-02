using System.Collections.Generic;
using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 목표 부품: 켜진 모닥불을 끈다. GOAP의 확장성을 보여 주는 시연용 부품이다.
    ///
    /// 적의 인스펙터에서 이 컴포넌트를 켜면, 적 코드를 하나도 고치지 않았는데도 적이 켜진 불을 찾아가 끈다.
    /// 목표에는 "불이 꺼진 상태(fireExtinguished)"만 적었고, 행동의 순서는 어디에도 적지 않았다.
    /// 플래너가 행동의 조건과 효과를 보고 MoveToFire → Extinguish를 스스로 이어 붙인다.
    ///
    /// 우선순위는 60. 소리 조사(50)보다 높고 발견 신호 조사(75)와 추격(100)보다 낮다.
    /// 그래서 불을 끄러 가다가 플레이어를 보면 추격하고, 놓치면 다시 불을 끄러 간다.
    /// 끄던 진행도는 불에 남으므로, 끊겼다 돌아와도 이어서 끈다. (끼어들기로는 늦출 뿐 영원히 막을 수는 없다)
    /// 출구가 열리면 고를 이유가 없어져 목표가 저절로 꺼진다. 다 켠 순간의 성취는 되돌리지 않는다.
    ///
    /// 게임에서는 라운드 구성의 fakeExtinguish가 켜진 라운드(3라운드부터)에 Fake 적만 켜진 채로 시작한다.
    /// 그 밖의 적에게는 꺼진 채로 붙어 있어서, 플레이 중에 인스펙터에서 체크박스로 켜 시연할 수 있다.
    /// 수치는 GameTuning의 ashEnemy 항목(모닥불 끄기)에서 읽는다.
    ///
    /// 계획 실패 시연: 인스펙터에서 "MoveToFire 행동 포함"을 끄면 불 곁으로 갈 행동이 없어진다.
    /// 그러면 플래너가 이 목표까지의 계획을 세우지 못하고, 적은 이 목표를 건너뛰어 다음 목표(배회 등)를 따른다.
    /// 다시 켜면 그제야 계획이 생긴다. 미리 짜 놓은 동작이 아니라 조건과 효과를 따져 계획한다는 증거다.
    ///
    /// 씬 구성:
    ///   붙일 필요가 없다. StageLayout이 적마다 붙여 둔다.
    /// </summary>
    public class ExtinguishFireGoal : GoapModule
    {
        private static AshEnemyTuning Tuning => GameTuning.Instance.ashEnemy;
        private float reachDistance => Tuning.extinguishReach;

        [Tooltip("시연용. 끄면 MoveToFire 행동을 빼고 계획한다. 불 곁으로 갈 방법이 없어 계획이 실패하고, 적은 이 목표를 건너뛴다")]
        [SerializeField] private bool includeMoveToFire = true;

        private Goal _goal;
        private GoapAction[] _actions;

        /// MoveToFire 행동을 넣고 계획하는가. 바꾸면 곧바로 다시 계획한다.
        public bool IncludeMoveToFire
        {
            get => includeMoveToFire;
            set
            {
                includeMoveToFire = value;
                _actions = null;
                Notify();
            }
        }

        // 플레이 중 인스펙터에서 체크박스를 바꿔도 곧바로 반영한다.
        private void OnValidate()
        {
            if (!Application.isPlaying)
                return;
            _actions = null;
            if (isActiveAndEnabled)
                Notify();
        }

        /// 지금 끄러 가는 불
        public Campfire Target { get; private set; }

        public override Goal Goal
        {
            get
            {
                if (_goal == null)
                {
                    _goal = new Goal { Name = "ExtinguishFire" };
                    _goal.Desired.Facts["fireExtinguished"] = true;
                }
                _goal.Priority = Tuning.extinguishPriority;
                return _goal;
            }
        }

        public override IReadOnlyList<GoapAction> Actions =>
            _actions ??= includeMoveToFire
                ? new GoapAction[] { new MoveToFire(this), new Extinguish(this) }
                : new GoapAction[] { new Extinguish(this) };

        public override void WriteFacts(WorldState state, ChaserContext ctx)
        {
            Target = NearestLitFire(ctx.Self.position);
            state.Facts["litFireKnown"] = Target != null;
            state.Facts["atFire"] = false;
            state.Facts["fireExtinguished"] = false;
        }

        // 켜진 불이 있고, 출구가 아직 열리지 않았을 때만 끌 이유가 있다.
        public override bool IsRelevant(WorldState state, ChaserContext ctx)
        {
            Exit exit = GameManager.Instance != null ? GameManager.Instance.CurrentExit : null;
            bool exitOpen = exit != null && exit.IsOpen;
            return Target != null && !exitOpen;
        }

        private static Campfire NearestLitFire(Vector3 from)
        {
            Campfire best = null;
            float bestDistance = float.MaxValue;
            foreach (Campfire campfire in Campfire.All)
            {
                if (campfire == null || !campfire.IsLit)
                    continue;
                float distance = (campfire.transform.position - from).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = campfire;
                }
            }
            return best;
        }

        // 지금 끄러 가는 불을 주황 선으로 잇는다.
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || Target == null)
                return;

            Gizmos.color = new Color(1f, 0.5f, 0.1f);
            Gizmos.DrawLine(transform.position + Vector3.up * 3f, Target.transform.position + Vector3.up);
            Gizmos.DrawWireSphere(Target.transform.position, reachDistance);
        }

        /// 행동: 켜진 불로 간다. (조건: 켜진 불을 앎 / 효과: 불 곁에 있음)
        private class MoveToFire : GoapAction
        {
            private readonly ExtinguishFireGoal _owner;

            public MoveToFire(ExtinguishFireGoal owner)
            {
                _owner = owner;
                Name = "MoveToFire";
                Preconditions.Facts["litFireKnown"] = true;
                Effects.Facts["atFire"] = true;
            }

            public override bool Perform(ChaserContext ctx)
            {
                if (_owner.Target == null || !_owner.Target.IsLit)
                    return true;
                return ctx.Locomotion.MoveTo(_owner.Target.transform.position, _owner.reachDistance, ctx.Alert.Level);
            }
        }

        /// 행동: 불을 끈다. (조건: 불 곁에 있음 / 효과: 불이 꺼짐)
        private class Extinguish : GoapAction
        {
            private readonly ExtinguishFireGoal _owner;

            public Extinguish(ExtinguishFireGoal owner)
            {
                _owner = owner;
                Name = "Extinguish";
                Preconditions.Facts["atFire"] = true;
                Effects.Facts["fireExtinguished"] = true;
            }

            public override bool Perform(ChaserContext ctx)
            {
                if (_owner.Target == null || !_owner.Target.IsLit)
                    return true;

                // 불 앞에 머물며 끈다. 진행도는 불에 쌓인다.
                ctx.Locomotion.LookAround();
                return _owner.Target.AddExtinguish(Time.deltaTime, Tuning.extinguishTime);
            }
        }
    }
}
