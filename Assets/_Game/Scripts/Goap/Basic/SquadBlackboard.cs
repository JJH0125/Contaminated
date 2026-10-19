using System.Collections.Generic;
using UnityEngine;

namespace Squad
{
    /// <summary>
    /// Shared "squad memory" — the generalization of Half-Life's squad-leader
    /// information relay. Any chaser's perception writes here; every chaser's
    /// GOAP planner reads here. One sighting propagates to the whole squad.
    ///
    /// This is a scene-level singleton (one squad). For multiple independent
    /// squads, make it a regular component and give each chaser a reference
    /// to its own squad's blackboard.
    /// </summary>
    public class SquadBlackboard : MonoBehaviour
    {        
        public static SquadBlackboard Instance { get; private set; }

        // **** 시야에 대한 정보 ****

        // 현재 플레이어를 보고 있는 적들
        private readonly HashSet<DimensionMember> _seeingEnemies = new();

        // 플레이어가 지금 보이는지 여부
        public bool PlayerCurrentlyVisible => _seeingEnemies.Count > 0;
        // 플레이어가 마지막까지 있었던 위치
        public Vector3 LastPlayerPosition { get; private set; }
        /// 플레이어가 마지막으로 보인 뒤 지난 시간
        public float TimeSinceLastSeen { get; private set; } = Mathf.Infinity;

        // --- Sound facts (for the InvestigateSound goal) ---
        // HasSound is true while there's an un-investigated sound to check out.
        // LastSoundPosition is where it came from. A chaser walks there (MoveToSound),
        // searches (SearchSoundArea), then calls ClearSound() so it isn't chased
        // forever. Generator sounds and footsteps both land here; the difference
        // (footsteps stay in one dimension, generator sound crosses) is enforced
        // by WHO calls ReportSound — see the detection system, not here.

        // **** 소리에 대한 정보 ****
        // 아직 조사하지 않은 소리가 있는지 여부
        public bool HasSound { get; private set; }
        // 그 소리의 위치
        public Vector3 LastSoundPosition { get; private set; }
        // 지금 조사 중인 (플레이어가 아닌 대상의) 소리의 출처
        public GameObject SoundSource { get; private set; }
        // 지금 조사 중인 소리가 난 차원
        public Dimension SoundDimension { get; private set; }
        // 지금 조사 중인 소리가 차원을 관통하는 소리인가
        public bool SoundCanCrossDimension { get; private set; }
        // 조사가 완료된 오브젝트의 모음. 이곳에 담긴 오브젝트가 내는 소리는 무시한다.
        private readonly HashSet<GameObject> _investigateCompleted = new();

        // 다중 추격자 전용 //
        // 추격, 매복 등등의 역할을 나누고, 각 추격자들이 일제히 같은 행동을 하지 않도록 조율
        // <(역할), (추격자 번호)> 타입의 Dictionary로 역할을 현재 수행 중인 추격자를 기록
        private readonly Dictionary<string, int> _roleClaims = new();

        private void Awake()
        {
            // 기존에 있던 BlackBoard Instance 제거
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _investigateCompleted.Clear();
        }

        void Update()
        {
            if (!PlayerCurrentlyVisible)
                TimeSinceLastSeen += Time.deltaTime;
        }

        /// 플레이어가 시야에 들어왔을 때
        /// 플레이어의 정보가 업데이트되고, 경계 상태는 무조건 Alerted로 격상된다
        public void ReportSighting(DimensionMember enemy, Vector3 playerPosition)
        {
            // 이 적이 플레이어를 보기 시작했다
            _seeingEnemies.Add(enemy);
            TimeSinceLastSeen = 0f;
            LastPlayerPosition = playerPosition;
        }

        /// 플레이어가 시야에서 사라졌을 때
        /// Visible 정보를 false로 설정한다.
        /// LastPlayerPosition은 지우지 않고 남겨두어
        /// 시야에서 사라져도 잠시동안은 그 주변을 수색하도록 한다.
        public void ReportLostSight(DimensionMember enemy)
        {
            // 이 적이 플레이어를 더 이상 보지 않는다
            _seeingEnemies.Remove(enemy);
        }

        /// <summary>
        /// Report a heard sound (footstep, generator, etc.) — softer than a
        /// sighting. Sets HasSound so an idle chaser will pick up the
        /// InvestigateSound goal and walk over to check it out.
        /// The caller (detection system) decides whether a given chaser can hear
        /// this sound at all — e.g. a generator sound is reported to chasers in
        /// both dimensions, a footstep only to chasers in the player's dimension.
        /// </summary>
        
        /// 소리가 유발하는 경계 상태를 먼저 본 뒤
        /// 현재 상태보다 높다면 그에 맞게 격상시키고
        /// 소리의 정보가 업데이트된다
        public void ReportSound(Vector3 soundPosition, Sound sound, Dimension dimension, GameObject source = null)
        {
            // 이미 조사한 것의 소리라면 아무것도 하지 않는다
            if (source != null && _investigateCompleted.Contains(source))
                return;
            
            HasSound = true;
            LastSoundPosition = soundPosition;
            SoundSource = source;
            SoundDimension = dimension;
            SoundCanCrossDimension = sound.CanCrossDimension;

            /// 벽 너머의 플레이어가 내는 소리, 발전기 소리 등등
            /// 소리는 들리지만 플레이어가 보이지 않는 상황일 때
            /// 적은 플레이어가 방금까지 그곳에 있었으리라 추측한다.
            if (!PlayerCurrentlyVisible)
                LastPlayerPosition = soundPosition;
        }

        // 조사를 끝낸 오브젝트를 조사 완료 목록에 추가
        // (도달할 수 없는 곳에서 나는 소리에 대해서도 적용)
        public void AddInvestigateCompleted(GameObject source)
        {
            if (source != null)
                _investigateCompleted.Add(source);
        }

        /// 소리에 대한 조사를 끝냈을 때
        public void ClearSound()
        {
            HasSound = false;
            SoundSource = null;
        }

        /// 다중 추격자 전용 ///
        public bool TryClaimRole(string role, int chaserId)
        {
            // 역할을 선점한 다른 추격자가 이미 존재
            if (_roleClaims.TryGetValue(role, out int owner) && owner != chaserId)
                return false;
            // 이 추격자에게 역할 부여
            _roleClaims[role] = chaserId;
            return true;
        }

        public void ReleaseRole(string role, int chaserId)
        {
            if (_roleClaims.TryGetValue(role, out int owner) && owner == chaserId)
                _roleClaims.Remove(role);
        }
    }
}
