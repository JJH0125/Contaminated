#if false // 다중 추격자 데모: 지금은 쓰지 않아 컴파일에서 뺀다. 다시 쓰려면 이 줄과 맨 아래 #endif를 지운다.
using System.Collections.Generic;
using UnityEngine;

namespace Squad
{
    // ----------------------------------------------------------------------
    // GOALS — what each personality wants, and how badly.
    // ----------------------------------------------------------------------
    public static class GoalSet
    {
        public static List<Goal> ForPersonality(ChaserPersonality p)
        {
            var killPlayer = new Goal { Name = "KillPlayer", Priority = 10 };
            killPlayer.Desired.Facts["playerDead"] = true;

            var findPlayer = new Goal { Name = "FindPlayer", Priority = 5 };
            findPlayer.Desired.Facts["playerVisible"] = true;

            var patrol = new Goal { Name = "Patrol", Priority = 1 };
            patrol.Desired.Facts["patrolled"] = true;

            return new List<Goal> { killPlayer, findPlayer, patrol };
        }
    }

    // ----------------------------------------------------------------------
    // ACTION FACTORY — same actions for everyone, but costs differ by
    // personality. This is the heart of "same brain, different behavior".
    // ----------------------------------------------------------------------
    public static class ActionFactory
    {
        public static List<GoapAction> BuildActions(ChaserPersonality p)
        {
            var list = new List<GoapAction>
            {
                new ChasePlayerAction(p),
                new MoveToChokepointAction(p),
                new SearchLastKnownAction(p),
                new AttackAction(),
                new PatrolAction()
            };
            return list;
        }
    }

    // --- Direct chase: cheap for Aggressive, expensive for Ambusher ---
    public class ChasePlayerAction : GoapAction
    {
        private readonly float _cost;
        public ChasePlayerAction(ChaserPersonality p)
        {
            Name = "ChasePlayer";
            Preconditions.Facts["playerVisible"] = true;
            Effects.Facts["nearPlayer"] = true;
            _cost = p == ChaserPersonality.Aggressive ? 1f : 4f;
        }
        public override float GetCost(ChaserContext ctx) => _cost;
        public override bool Perform(ChaserContext ctx)
        {
            // Hook your 3D A* pathfinder here: path to ctx.Blackboard.LastPlayerPosition.
            Vector3 target = ctx.Blackboard.LastPlayerPosition;
            ctx.Self.position = Vector3.MoveTowards(ctx.Self.position, target, 3f * Time.deltaTime);
            return Vector3.Distance(ctx.Self.position, target) < 1.5f;
        }
    }

    // --- Cut off escape route: cheap for Ambusher ---
    public class MoveToChokepointAction : GoapAction
    {
        private readonly float _cost;
        public MoveToChokepointAction(ChaserPersonality p)
        {
            Name = "MoveToChokepoint";
            Preconditions.Facts["alerted"] = true;
            Effects.Facts["playerVisible"] = true; // expects to re-spot at the chokepoint
            _cost = p == ChaserPersonality.Ambusher ? 1f : 5f;
        }
        public override float GetCost(ChaserContext ctx) => _cost;
        public override bool CheckProceduralPrecondition(ChaserContext ctx)
        {
            // Only one chaser should claim the ambush role at a time.
            return ctx.Blackboard.TryClaimRole("ambush", ctx.Agent.chaserId);
        }
        public override bool Perform(ChaserContext ctx)
        {
            // Pick a chokepoint between player's last pos and likely escape.
            // Placeholder: move toward last known pos from a flanking offset.
            Vector3 target = ctx.Blackboard.LastPlayerPosition + Vector3.right * 3f;
            ctx.Self.position = Vector3.MoveTowards(ctx.Self.position, target, 3f * Time.deltaTime);
            bool arrived = Vector3.Distance(ctx.Self.position, target) < 1f;
            if (arrived) ctx.Blackboard.ReleaseRole("ambush", ctx.Agent.chaserId);
            return arrived;
        }
    }

    // --- Search last known position: cheap for Searcher ---
    public class SearchLastKnownAction : GoapAction
    {
        private readonly float _cost;
        public SearchLastKnownAction(ChaserPersonality p)
        {
            Name = "SearchLastKnown";
            Preconditions.Facts["hasLastKnownPos"] = true;
            Effects.Facts["playerVisible"] = true; // hopes to find them
            _cost = p == ChaserPersonality.Searcher ? 1f : 3f;
        }
        public override float GetCost(ChaserContext ctx) => _cost;
        public override bool Perform(ChaserContext ctx)
        {
            Vector3 target = ctx.Blackboard.LastPlayerPosition;
            ctx.Self.position = Vector3.MoveTowards(ctx.Self.position, target, 2.5f * Time.deltaTime);
            return Vector3.Distance(ctx.Self.position, target) < 1f;
        }
    }

    public class AttackAction : GoapAction
    {
        public AttackAction()
        {
            Name = "Attack";
            Preconditions.Facts["nearPlayer"] = true;
            Effects.Facts["playerDead"] = true;
        }
        public override float GetCost(ChaserContext ctx) => 1f;
        public override bool Perform(ChaserContext ctx)
        {
            // Trigger attack animation / damage here.
            return true;
        }
    }

    public class PatrolAction : GoapAction
    {
        public PatrolAction()
        {
            Name = "Patrol";
            Effects.Facts["patrolled"] = true;
        }
        public override float GetCost(ChaserContext ctx) => 2f;
        public override bool Perform(ChaserContext ctx)
        {
            // Wander between waypoints here.
            return true;
        }
    }
}
#endif
