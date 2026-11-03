using System.Collections.Generic;
using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 같은 영역을 함께 다니는 적의 느슨한 무리. Real 적은 packSize마리씩 무리를 짓는다. (차원별 값)
    ///
    /// 무리는 한 덩어리로 붙어 다니지 않는다. 영역(중심과 반지름)만 함께 쓰고, 몸은 서로 떨어져 다닌다.
    ///   영역       적은 배회할 곳을 무리의 중심에서 packTerritoryRadius 안에서만 고른다
    ///   거리 두기   다른 무리원이 있는 곳과 가는 곳에서 packSeparation 넘게 떨어진 자리를 고른다
    ///   떠도는 중심 무리원들이 배회를 packDriftLegs번 마칠 때마다 중심을 packDriftDistance만큼 옮긴다
    ///              중심이 한곳에 머물면 "저 구역에는 늘 셋이 있다"를 외우게 되므로, 영역 자체가 숲을 천천히 떠돈다
    /// 그래서 한 마리를 봐도 나머지가 어디 있는지는 알 수 없고, "이 근처 어딘가에 더 있다"만 알 수 있다.
    ///
    /// 무리는 평소에 다니는 모습에만 쓴다. 플레이어를 보거나 소리를 들으면 각자 판단한다. (WanderStep)
    /// StageLayout이 라운드를 시작할 때 무리를 짜고 첫 중심을 정해 적마다 넘겨준다.
    /// </summary>
    public class ChaserPack
    {
        private readonly List<HorrorChaserAgent> _members = new();
        private int _legsSinceDrift;

        /// 영역의 중심. 배회할 곳은 이 둘레에서 고른다.
        public Vector3 Center { get; private set; }

        // 0보다 크면 영역 반지름을 이 값으로 고정하고 중심을 옮기지 않는다. (시연 씬에서 적을 한자리에 묶어 둘 때)
        private readonly float _fixedRadius;

        public ChaserPack(Vector3 center, float fixedRadius = 0f)
        {
            Center = center;
            _fixedRadius = fixedRadius;
        }

        public void Add(HorrorChaserAgent member)
        {
            if (member != null && !_members.Contains(member))
                _members.Add(member);
        }

        /// <summary>
        /// self가 다음에 배회할 자리를 고른다. 영역 안의 걸을 수 있는 자리를 여러 개 뽑아 보고,
        /// 다른 무리원(있는 곳과 가는 곳)과 가장 멀리 떨어진 자리를 고른다. 거리 두기를 만족하면 바로 고른다.
        /// 고를 때마다 배회 한 번으로 세어, 정해 둔 횟수가 되면 중심을 옮긴다.
        /// </summary>
        public bool TryChooseSpot(ChaserContext ctx, EnemyTuning tuning, out Vector3 spot)
        {
            if (_fixedRadius <= 0f)
                CountLeg(ctx, tuning);

            float radius = _fixedRadius > 0f ? _fixedRadius : tuning.packTerritoryRadius;
            spot = Vector3.zero;
            float bestGap = -1f;
            for (int i = 0; i < 12; i++)
            {
                Vector2 r = Random.insideUnitCircle * radius;
                Vector3 candidate = Center + new Vector3(r.x, 0f, r.y);
                if (!ctx.Locomotion.IsWalkable(candidate))
                    continue;

                float gap = GapToOthers(candidate, ctx.Self);
                if (gap > bestGap)
                {
                    bestGap = gap;
                    spot = candidate;
                }
                if (gap >= tuning.packSeparation)
                    break;
            }
            return bestGap >= 0f;
        }

        // candidate에서 다른 무리원(지금 있는 곳, 가고 있는 곳) 중 가장 가까운 것까지의 평면 거리.
        private float GapToOthers(Vector3 candidate, Transform self)
        {
            float nearest = float.MaxValue;
            foreach (HorrorChaserAgent other in _members)
            {
                if (other == null || !other.isActiveAndEnabled || other.transform == self)
                    continue;
                nearest = Mathf.Min(nearest, FlatDistance(candidate, other.transform.position));
                if (other.Locomotion != null && other.Locomotion.HasWanderTarget)
                    nearest = Mathf.Min(nearest, FlatDistance(candidate, other.Locomotion.WanderTarget));
            }
            return nearest;
        }

        // 배회를 한 번 셈하고, 정해 둔 횟수가 되면 중심을 걸을 수 있는 이웃 자리로 옮긴다.
        // 옮길 자리를 못 찾으면 이번에는 그대로 두고 다음 배회 때 다시 해 본다.
        private void CountLeg(ChaserContext ctx, EnemyTuning tuning)
        {
            if (tuning.packDriftLegs <= 0 || ++_legsSinceDrift < tuning.packDriftLegs)
                return;

            for (int i = 0; i < 8; i++)
            {
                Vector3 next = Center + ZoneMap.Direction(Random.Range(0f, 360f)) * tuning.packDriftDistance;
                if (ctx.Locomotion.IsWalkable(next))
                {
                    Center = next;
                    _legsSinceDrift = 0;
                    return;
                }
            }
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
