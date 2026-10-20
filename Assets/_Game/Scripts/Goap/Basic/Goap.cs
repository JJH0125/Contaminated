using System.Collections.Generic;
using UnityEngine;

namespace Squad
{
    /// <summary>
    /// bool 값의 집합으로 이루어진 WorldState.
    /// </summary>
    public class WorldState
    {
        /// <summary>
        /// 세계의 상태를 저장할,
        /// string 타입의 key와
        /// bool 타입의 value로 이루어진
        /// Dictionary 자료구조.
        /// </summary>
        public readonly Dictionary<string, bool> Facts;

        /// <summary>
        /// 매개변수가 없는 생성자
        /// </summary>
        public WorldState() => Facts = new Dictionary<string, bool>();
        
        /// <summary>
        /// Dictionary 매개변수를 받는 생성자.
        /// Facts = facts를 하게 되면 주소를 공유해버리기 때문에
        /// 이후 facts의 내용이 변경될 때마다
        /// Facts의 내용도 그에 따라 변경되어버린다.
        /// </summary>
        /// <param name="facts"></param>
        public WorldState(Dictionary<string, bool> facts) => Facts = new Dictionary<string, bool>(facts);

        /// <summary>
        /// 내 Facts의 내용을 본떠 복사본을 생성
        /// </summary>
        /// <returns></returns>
        public WorldState Clone() => new WorldState(Facts);

        /// <summary>
        /// 'goal의 Facts가 내 Facts에 모두 있는가?'
        /// 즉 'goal의 모든 요소를 만족하는가?' 를 return한다.
        /// 호출되는 때는 총 2가지.
        /// 1. 현재 상태가 최종 목표의 상태인가?를 검사할 때
        /// 2. 이 행동을 할 수 있는가?를 가리기 위해 현재 상태가 전제조건과 일치하는지 검사할 때
        public bool Matches(WorldState goal)
        {
            // || 연산자는 조건이 여러 개일 때 앞 조건이 true로 정해지면
            // 뒤의 조건의 검사를 생략하는 성질이 있다
            // 따라서 앞 조건에서 Key를 찾지 못해 False가 나버리면,
            // v를 검사하지 않고 바로 True 처리한다
            foreach (var kv in goal.Facts)
                if (!Facts.TryGetValue(kv.Key, out bool v) ||
                v != kv.Value)
                    return false;
            return true;
        }

        /// <summary>
        /// 행동 완료 후 effect를 나의 worldstate에 적용.
        /// </summary>
        /// <param name="effects"></param>
        public void Apply(WorldState effects)
        {
            foreach (var kv in effects.Facts)
                Facts[kv.Key] = kv.Value;
        }

        /// <summary>
        /// goal과 나의 worldstate를 비교하여
        /// value의 정보가 서로 다른 것의 개수를 return
        /// </summary>
        /// <param name="goal"></param>
        /// <returns></returns>
        public int DistanceTo(WorldState goal)
        {
            int diff = 0;
            foreach (var kv in goal.Facts)
                if (!Facts.TryGetValue(kv.Key, out bool v) || v != kv.Value)
                    diff++;
            return diff;
        }
    }

    /// <summary>
    /// 적이 할 수 있는 행동.
    /// 행동의 이름, 행동을 위한 전제조건, 행동 후 효과가 있다.
    /// </summary>
    public abstract class GoapAction
    {
        public string Name;
        public WorldState Preconditions = new WorldState();
        public WorldState Effects = new WorldState();

        public virtual float GetCost(ChaserContext ctx) => 1f;

        /// <summary>Can this action even be attempted right now? (dynamic check)</summary>
        public virtual bool CheckProceduralPrecondition
        (ChaserContext ctx) => true;

        /// <summary>
        /// 실제로 행동을 수행하고, 행동이 완료되었는지 여부를 반환한다.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns>행동이 이번 프레임에 완료되었는지의 bool 값</returns>
        public abstract bool Perform(ChaserContext ctx);
    }

    /// 적이 달성하고자 하는 목표. 목표의 이름, 우선순위 값, 목표를 달성했을 때의 state 값이 있다.
    public class Goal
    {
        public string Name;
        public float Priority;
        public WorldState Desired = new WorldState();
    }

    /// <summary>
    /// WorldState를 노드로, Action을 엣지로 하는 A* 기반의 행동
    /// </summary>
    public static class GoapPlanner
    {
        // PlanNode는 GoapPlanner에서만 쓰이는 클래스이기에 중첩으로 private로 선언
        private class PlanNode
        {
            public float GCost;
            public float HCost;
            // 이 노드에 오기 직전 위치했던 노드
            public PlanNode Parent;
            // 이 노드에 도달하기 위해 행한 Action
            public GoapAction Action;
            public WorldState State;

            public float FCost => GCost + HCost;
        }

        /// <summary>
        /// 완성된 계획(행동의 순서)를 반환하는 Plan 함수.
        /// Queue는 FIFO를 만족하는, 먼저 들어간 것이 먼저 나오는 자료구조.
        /// 가능한 행동의 List와 최초의 WorldState, Goal, ChaserContext를 넣는다.
        /// </summary>
        /// <param name="available"></param>
        /// <param name="start"></param>
        /// <param name="goal"></param>
        /// <param name="ctx"></param>
        /// <returns>GoapAction의 순서</returns>
        public static Queue<GoapAction> Plan(
            List<GoapAction> available, // 할 수 있는 행동들
            WorldState start,           // 시작 상태
            Goal goal,                  // 목표
            ChaserContext ctx)
        {
            var open = new List<PlanNode>();
            var startNode = new PlanNode
            {
                Parent = null, GCost = 0,
                HCost = start.DistanceTo(goal.Desired),
                Action = null, State = start.Clone()
            };
            open.Add(startNode);

            // 탐색 횟수 기록. 무한 루프 방지용.
            int guard = 0;
            while (open.Count > 0 && guard++ < 500)
            {
                // Sort의 인자값으로 정렬하는 기준을 넘겼다
                // (a, b)를 받아 CompareTo를 실행하고
                // a가 b보다 작으면 b의 앞에, b보다 크면 b의 뒤에 놓는다
                // 힙을 이용해 정렬했던 NodeHeap과 달리
                // GOAP에서는 노드의 전체 수가 많지 않기에
                // 좀더 느린 Sort를 사용해도 됨
                open.Sort((a, b) => a.FCost.CompareTo(b.FCost));                
                // FCost 오름차순으로 정렬 후, 값이 가장 적은 노드를 Pop
                PlanNode current = open[0];
                open.RemoveAt(0);

                // 현재 상태가 목표 상태가 되었다면
                if (current.State.Matches(goal.Desired))
                    return BuildPlan(current);

                foreach (GoapAction action in available)
                {
                    // 현재 상태가 전제조건과 맞지 않으면 패스
                    if (!current.State.Matches(action.Preconditions))
                        continue;
                    if (!action.CheckProceduralPrecondition(ctx))
                        continue;

                    // 조건이 모두 맞으면,
                    // 현재 상태의 복사본을 만들어
                    // 행동 후 효과를 적용시킴
                    WorldState next = current.State.Clone();
                    next.Apply(action.Effects);

                    open.Add(new PlanNode
                    {
                        Parent = current,
                        GCost = current.GCost + action.GetCost(ctx),
                        HCost = next.DistanceTo(goal.Desired),
                        Action = action,
                        State = next
                    });
                }
            }
            // Plan을 찾지 못했을 경우
            return null;
        }

        // Parent 노드와 그 노드에서 어떤 행동을 통해 다음 노드에 도달했는지를 따라
        // 행동을 역추적하여 행동의 순서 완성
        private static Queue<GoapAction> BuildPlan(PlanNode end)
        {
            var stack = new Stack<GoapAction>();
            for (PlanNode n = end; n?.Action != null; n = n.Parent)
                // stack에 하나씩 push하여, 나중에 꺼낼 때 정순으로 꺼내지도록 함
                stack.Push(n.Action);
            return new Queue<GoapAction>(stack);
        }
    }
}
