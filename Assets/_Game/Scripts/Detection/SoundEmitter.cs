using UnityEngine;

namespace Squad
{
    public static class SoundEmitter
    {
        // 소리가 영향을 미칠 수 있는 최대 적 : 16
        private const int MaxHits = 16;
        private static readonly Collider[] _hits = new Collider[MaxHits];
        
        /// position에서 sound를 방출한다.
        /// Radius 안에 있으면서 enemyLayer 안에 속한 개체가 있다면 ReportSound한다.
        /// 플레이어가 내는 소리가 아니면 오브젝트의 정보를 같이 넘긴다.
        /// 차원을 넘는 소리가 아니면 오브젝트가 현재 위치한 차원을 같이 넘긴다.
        public static void Emit(Vector3 position, Sound sound, LayerMask enemyLayer, Dimension dimension, GameObject source = null)
        {
             // 소리 반경 표시 — 들은 적이 없어도 보여줘야 하므로 맨 앞에서 호출
            if (SoundRingDisplay.Instance != null)
                SoundRingDisplay.Instance.Show(position, sound);

            // 효과음 재생 — 반경 표시와 같은 이유로 들은 적이 없어도 재생한다
            if (SfxPlayer.Instance != null)
                SfxPlayer.Instance.PlayAt(sound.Clip, position);

            if (SquadBlackboard.Instance == null)
                return;
            
            // 청각이 좋은 적(Fake)까지 찾을 수 있도록 넉넉하게 찾은 뒤, 적마다 자기 청각으로 거른다.
            GameTuning tuning = GameTuning.Instance;
            float widest = sound.Radius * Mathf.Max(tuning.realEnemy.hearingMultiplier, tuning.fakeEnemy.hearingMultiplier);
            int count = Physics.OverlapSphereNonAlloc(
                position, widest, _hits, enemyLayer, QueryTriggerInteraction.Ignore);

            // ReportSound는 한 명이라도 소리를 들어야 호출한다.
            bool AnyOneHeard = false;

            // 반경 안에 있다고 모두 들을 수 있는 건 아니기 때문에
            // 차원을 관통하지 못하는 소리는 같은 차원의 적이 있어야만 보고한다.
            // (다른 차원의 적만 반경 안에 있을 때 보고하면, 블랙보드를 공유하는
            //  멀리 있는 같은 차원의 적이 그 소리를 조사하러 오게 된다)
            for (int i = 0; i < count; i++)
            {
                if (CanHear(_hits[i], position, sound, dimension))
                {
                    AnyOneHeard = true;
                    _hits[i].GetComponentInParent<ChaserAlert>()?.RaiseAlert(sound.Alert);
                }                
            }
            
            if (AnyOneHeard)
                    SquadBlackboard.Instance.ReportSound(position, sound, dimension, source);
        }

        private static bool CanHear(Collider enemy, Vector3 position, Sound sound, Dimension dimension)
        {
            DimensionMember member = enemy.GetComponentInParent<DimensionMember>();

            // 소리의 반경에 이 적의 청각 배율을 곱한 거리 안에 있어야 들린다.
            float hearing = sound.Radius * GameTuning.Instance.EnemyFor(member).hearingMultiplier;
            if ((enemy.ClosestPoint(position) - position).sqrMagnitude > hearing * hearing)
                return false;

            if (sound.CanCrossDimension)
                return true;

            return member != null && member.Dimension == dimension;
        }
    }
}
