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

        // 플레이어가 지금 적에게 보이는지 여부
        public bool PlayerCurrentlyVisible => _seeingEnemies.Count > 0;
        // 최신 사건 번호 (몇 번째로 플레이어를 새로 포착했는지)
        // 아무도 플레이어를 보지 않다가 누군가 다시 보게 된 순간 1 오른다. 같은 추격 중에 여러 적이 봐도 그대로다.
        public int spotCount { get; private set; } = 0;
        // 최신 사건이 일어난 차원. 다른 차원의 사건에는 합류한 적이 반응하지 않게 한다.
        public Dimension SpotDimension { get; private set; } = Dimension.None;
        // 마지막으로 보인(또는 소리로 짐작한) 플레이어의 위치
        public Vector3 LastPlayerPosition { get; private set; }
        // 적이 눈으로 직접 본 플레이어의 마지막 위치. 소리로는 바뀌지 않는다.
        // 발견 신호를 받고 합류한 적은 이 위치로 간다. (LastPlayerPosition은 다른 차원의 소리로도 덮어써진다)
        public Vector3 LastSightingPosition { get; private set; }
        // 마지막으로 보인 플레이어가 이동하던 방향 (평면, 길이 1). 멈춰 있으면 마지막으로 움직이던 방향이 남는다.
        public Vector3 LastPlayerDirection { get; private set; } = Vector3.zero;
        // 마지막으로 보인 플레이어의 이동 속력(m/s, 평면). 앞질러 갈 위치를 예측하는 데 쓴다.
        public float LastPlayerSpeed { get; private set; }

        // 플레이어가 이 속도(m/s)보다 빠를 때만 이동 방향을 새로 적는다. (멈췄을 때 방향이 0이 되지 않도록)
        private const float DirectionMinSpeed = 0.5f;

        // **** 수색 지도 (발견 신호를 받고 합류한 적들이 함께 쓴다) ****

        /// <summary>수색 지점 하나. 누가 맡았는지와 확인했는지를 함께 기록한다.</summary>
        public class SearchPoint
        {
            public Vector3 Position;
            // 맡은 적의 번호 (0이면 아무도 맡지 않음)
            public int Owner;
            // 맡은 시각. 맡은 적이 반납하지 못하고 사라져도 일정 시간이 지나면 다른 적이 가져갈 수 있다.
            public float ClaimTime;
            public bool Checked;
        }

        // 지금 수색 지도가 어느 사건의 것인가 (0이면 아직 없음)
        public int SearchPlanSpot { get; private set; }
        private readonly List<SearchPoint> _searchPoints = new();
        public IReadOnlyList<SearchPoint> SearchPoints => _searchPoints;

        // 수색 지도
        private int searchPointCount => GameTuning.Instance.search.pointCount;
        private float[] searchRings => GameTuning.Instance.search.rings;
        private float searchPointSpacing => GameTuning.Instance.search.pointSpacing;
        [Tooltip("숨을 만한 곳(발견 지점에서 가려 안 보이는 곳)인지 판단할 때 시선을 가리는 레이어 (나무 기둥, 지형)")]
        [SerializeField] private LayerMask searchCoverMask = (1 << 3) | (1 << 10);   // Unwalkable + Floor
        private float searchClaimTimeout => GameTuning.Instance.search.claimTimeout;

        // 둘레 하나에서 후보를 뽑는 방향의 수
        private const int SearchDirections = 12;
        // 가려졌는지 볼 때 시선의 높이(m)
        private const float SearchEyeHeight = 2f;
        private Astar3D.Pathfinder _pathfinder;
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
            _pathfinder = FindObjectOfType<Astar3D.Pathfinder>();
        }

        /// <summary>
        /// 이 사건(spot)의 수색 지도가 아직 없으면 만든다. 그 사건에서 발견 지점에 처음 도착한 적이 부른다.
        /// 발견 지점(center) 둘레에서 갈 수 있는 곳을 후보로 뽑고, 점수가 높은 순으로 서로 떨어진 지점을 고른다.
        ///   - 발견 지점에서 나무나 언덕에 가려 안 보이는 곳 (숨을 만한 곳) +1
        ///   - 플레이어가 달아나던 방향 쪽일수록 최대 +1
        ///   - 같은 점수면 가까운 곳을 먼저 (조금씩 흩뜨려서 매번 같은 모양이 되지 않게)
        /// </summary>
        public void EnsureSearchPlan(int spot, Vector3 center, Vector3 fleeDirection)
        {
            if (SearchPlanSpot == spot)
                return;

            SearchPlanSpot = spot;
            _searchPoints.Clear();

            Terrain terrain = Terrain.activeTerrain;
            Vector3 eye = Grounded(center, terrain) + Vector3.up * SearchEyeHeight;
            var candidates = new List<(Vector3 position, float score)>();
            float angleOffset = Random.Range(0f, 360f);

            foreach (float ring in searchRings)
            {
                for (int i = 0; i < SearchDirections; i++)
                {
                    float angle = (angleOffset + i * 360f / SearchDirections) * Mathf.Deg2Rad;
                    Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    Vector3 position = Grounded(center + direction * ring, terrain);
                    if (_pathfinder != null && !_pathfinder.IsWalkable(position, SpotDimension))
                        continue;

                    float score = 0f;
                    Vector3 look = position + Vector3.up * SearchEyeHeight;
                    if (Physics.Linecast(eye, look, searchCoverMask, QueryTriggerInteraction.Ignore))
                        score += 1f;
                    if (fleeDirection.sqrMagnitude > 0.0001f)
                        score += Mathf.Max(0f, Vector3.Dot(direction, fleeDirection));
                    score -= ring * 0.01f;
                    score += Random.Range(0f, 0.1f);
                    candidates.Add((position, score));
                }
            }

            candidates.Sort((a, b) => b.score.CompareTo(a.score));
            foreach ((Vector3 position, float _) in candidates)
            {
                if (_searchPoints.Count >= searchPointCount)
                    break;

                bool tooClose = false;
                foreach (SearchPoint point in _searchPoints)
                    if (FlatDistance(point.Position, position) < searchPointSpacing)
                        tooClose = true;
                if (!tooClose)
                    _searchPoints.Add(new SearchPoint { Position = position });
            }
        }

        /// <summary>
        /// 아직 아무도 맡지 않았고 확인하지도 않은 지점 중 from에서 가장 가까운 것을 agentId가 맡는다. 없으면 null.
        /// (맡은 지 오래된 지점은 맡은 적이 사라진 것으로 보고 다시 내준다)
        /// </summary>
        public SearchPoint ClaimSearchPoint(int agentId, Vector3 from)
        {
            SearchPoint best = null;
            float bestDistance = float.PositiveInfinity;
            foreach (SearchPoint point in _searchPoints)
            {
                if (point.Checked)
                    continue;
                bool free = point.Owner == 0 || point.Owner == agentId ||
                            Time.time - point.ClaimTime > searchClaimTimeout;
                if (!free)
                    continue;

                float distance = FlatDistance(point.Position, from);
                if (distance < bestDistance)
                {
                    best = point;
                    bestDistance = distance;
                }
            }

            if (best != null)
            {
                best.Owner = agentId;
                best.ClaimTime = Time.time;
            }
            return best;
        }

        /// <summary>맡은 지점을 확인 완료로 표시하고 반납한다.</summary>
        public void CompleteSearchPoint(SearchPoint point)
        {
            if (point == null)
                return;
            point.Checked = true;
            point.Owner = 0;
        }

        /// <summary>agentId가 맡고 있던 지점을 모두 반납한다. (수색을 끝내거나 합류를 풀 때)</summary>
        public void ReleaseSearchPoints(int agentId)
        {
            foreach (SearchPoint point in _searchPoints)
                if (point.Owner == agentId)
                    point.Owner = 0;
        }

        /// <summary>
        /// position에서 radius 안에 있는 지점은 지나가며 본 것으로 치고 확인 완료로 표시한다.
        /// (다른 적이 맡은 지점은 그 적에게 맡긴다)
        /// </summary>
        public void MarkSearchPointsSeen(Vector3 position, float radius, int agentId)
        {
            foreach (SearchPoint point in _searchPoints)
            {
                if (point.Checked || (point.Owner != 0 && point.Owner != agentId))
                    continue;
                if (FlatDistance(point.Position, position) <= radius)
                    point.Checked = true;
            }
        }

        // position의 높이를 땅 높이로 맞춘다. 지형이 없으면 그대로 둔다.
        private static Vector3 Grounded(Vector3 position, Terrain terrain)
        {
            if (terrain != null)
                position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
            return position;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
            => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        void Update()
        {
            if (!PlayerCurrentlyVisible)
                TimeSinceLastSeen += Time.deltaTime;
        }

        /// <summary>
        /// 플레이어가 시야에 들어왔을 때 (보고 있는 동안 매번) 부른다.
        /// 플레이어의 위치와 이동 방향을 갱신하고, 아무도 보지 않던 플레이어를 이 적이 새로 봤다면
        /// 사건 번호를 올리고 true를 돌려준다. 부른 쪽(VisionCensor)은 true일 때 주변 적에게 신호(포효)를 보낸다.
        /// (경계 단계는 여기서 다루지 않는다. 본 적의 ChaserAlert가 따로 올린다)
        /// </summary>
        /// <param name="playerVelocity">플레이어의 이동 속도. 이동 방향을 구하는 데 쓴다</param>
        public bool ReportSighting(DimensionMember enemy, Vector3 playerPosition, Vector3 playerVelocity)
        {
            // 아무도 보지 않다가 이 적이 처음으로 봤다면 새 사건이다.
            bool newSpot = _seeingEnemies.Count == 0;
            if (newSpot)
            {
                spotCount++;
                SpotDimension = enemy != null ? enemy.Dimension : Dimension.None;
            }

            _seeingEnemies.Add(enemy);
            TimeSinceLastSeen = 0f;
            LastPlayerPosition = playerPosition;
            LastSightingPosition = playerPosition;

            // 방향은 위치 차이가 아니라 실제 이동 속도로 구한다.
            // 위치 차이로 구하면 새 사건의 첫 보고(이전 사건의 위치와 비교)나 같은 프레임의 두 번째 보고(차이 0)에서 틀어진다.
            Vector3 flat = new Vector3(playerVelocity.x, 0f, playerVelocity.z);
            LastPlayerSpeed = flat.magnitude;
            if (flat.magnitude > DirectionMinSpeed)
                LastPlayerDirection = flat.normalized;

            return newSpot;
        }

        /// <summary>
        /// 지금 플레이어를 보고 있는 적들 중에서 enemy가 플레이어와 몇 번째로 가까운가.
        /// 0이면 가장 가까운 적(곧장 쫓는 역할), 1 이상이면 앞질러 가는 역할이다. 보고 있지 않으면 -1.
        /// 매번 거리로 다시 정하므로, 곧장 쫓던 적이 뒤처지면 더 가까운 적이 그 역할을 넘겨받는다.
        /// </summary>
        public int ChaseRank(DimensionMember enemy, Vector3 playerPosition)
        {
            if (enemy == null || !_seeingEnemies.Contains(enemy))
                return -1;

            float myDistance = FlatDistance(enemy.transform.position, playerPosition);
            int myId = enemy.GetInstanceID();
            int rank = 0;
            foreach (DimensionMember other in _seeingEnemies)
            {
                if (other == null || other == enemy || !other.isActiveAndEnabled)
                    continue;

                float distance = FlatDistance(other.transform.position, playerPosition);
                // 거리가 같으면 번호로 순서를 정해, 두 적이 서로를 앞이라고 여기는 일이 없게 한다.
                if (distance < myDistance || (Mathf.Approximately(distance, myDistance) && other.GetInstanceID() < myId))
                    rank++;
            }
            return rank;
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

        /// 이 적이 플레이어를 보고 있는가?
        public bool IsSeeingPlayerNow(DimensionMember enemy) => _seeingEnemies.Contains(enemy);

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
