using System.Collections.Generic;
using UnityEngine;

namespace Squad
{
    public static class SoundEmitter
    {
        // 소리가 영향을 미칠 수 있는 최대 적 : 16
        private const int MaxHits = 16;
        private static readonly Collider[] _hits = new Collider[MaxHits];
        
        // 한 적의 몸에 콜라이더가 여럿이어도 한 번만 듣게 한다.
        private static readonly HashSet<HorrorChaserAgent> _heard = new();

        /// position에서 sound를 방출한다.
        /// 반경(× 적마다의 청각 배율) 안에서 들을 수 있는 적에게만 전한다. 들은 적은 경계가 오르고,
        /// 자기 기록(ChaserContext)에 소리의 위치를 적는다. 블랙보드에는 적지 않는다. 듣는 것은 들은 적만 안다.
        /// 차원을 넘는 소리가 아니면, 소리가 난 차원(dimension)의 적만 듣는다.
        /// playEffect: 효과음과 반경 표시를 낼지. 같은 소리를 차원마다 따로 낼 때 두 번째부터는 끈다.
        public static void Emit(Vector3 position, Sound sound, LayerMask enemyLayer, Dimension dimension, bool playEffect = true)
        {
            if (playEffect)
            {
                // 소리 반경 표시와 효과음 — 들은 적이 없어도 보여주고 들려준다
                if (SoundRingDisplay.Instance != null)
                    SoundRingDisplay.Instance.Show(position, sound);
                if (SfxPlayer.Instance != null)
                    SfxPlayer.Instance.PlayAt(sound.Clip, position);
            }

            // 청각이 좋은 적(Fake)까지 찾을 수 있도록 넉넉하게 찾은 뒤, 적마다 자기 청각으로 거른다.
            GameTuning tuning = GameTuning.Instance;
            float widest = sound.Radius * Mathf.Max(tuning.realEnemy.hearingMultiplier, tuning.fakeEnemy.hearingMultiplier);
            int count = Physics.OverlapSphereNonAlloc(
                position, widest, _hits, enemyLayer, QueryTriggerInteraction.Ignore);

            _heard.Clear();
            for (int i = 0; i < count; i++)
            {
                if (!CanHear(_hits[i], position, sound, dimension))
                    continue;

                HorrorChaserAgent agent = _hits[i].GetComponentInParent<HorrorChaserAgent>();
                if (agent == null || !_heard.Add(agent))
                    continue;

                // 경계를 먼저 올려야 다시 세우는 계획이 오른 경계의 속도로 움직인다.
                ChaserAlert alert = agent.GetComponent<ChaserAlert>();
                if (alert != null)
                    alert.RaiseAlert(sound.Alert);
                agent.HearSound(position);
            }
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
