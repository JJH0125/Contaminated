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
        /// 발전기가 내는 소리가 아니면 오브젝트가 현재 위치한 차원을 같이 넘긴다.
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
            
            int count = Physics.OverlapSphereNonAlloc(
                position, sound.Radius, _hits, enemyLayer, QueryTriggerInteraction.Ignore);

            // ReportSound는 한 명이라도 소리를 들어야 호출한다.
            bool AnyOneHeard = false;

            // 반경 안에 있다고 모두 들을 수 있는 건 아니기 때문에
            // 차원을 관통하지 못하는 소리는 같은 차원의 적이 있어야만 보고한다.
            // (다른 차원의 적만 반경 안에 있을 때 보고하면, 블랙보드를 공유하는
            //  멀리 있는 같은 차원의 적이 그 소리를 조사하러 오게 된다)
            for (int i = 0; i < count; i++)
            {
                if (CanHear(_hits[i], sound, dimension))
                {
                    AnyOneHeard = true;
                    _hits[i].GetComponentInParent<ChaserAlert>()?.RaiseAlert(sound.Alert);
                }                
            }
            
            if (AnyOneHeard)
                    SquadBlackboard.Instance.ReportSound(position, sound, dimension, source);
        }

        private static bool CanHear(Collider enemy, Sound sound, Dimension dimension)
        {
            if (sound.CanCrossDimension)
                return true;

            DimensionMember member = enemy.GetComponentInParent<DimensionMember>();
            return member != null && member.Dimension == dimension;
        }
    }
}
