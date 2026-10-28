using System.Collections.Generic;
using Astar3D;
using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 라운드가 시작될 때(Stage가 생성될 때) Stage 안의 오브젝트들을 지형 위에 배치하고, 그 라운드의 장벽을 세운다.
    /// 맵(지형과 숲)은 모든 라운드가 같다. 라운드마다 달라지는 것은 GameTuning의 라운드별 구성(stage.rounds)이 정한다.
    ///
    /// 배치 순서 (뒤의 것이 앞의 것을 피해야 하므로 이 순서를 지킨다):
    ///   1. 출구와 플레이어  항상 같은 자리 (출구는 남쪽, 플레이어는 그 앞)
    ///      랜드마크        항상 같은 자리, 같은 모양 (봉우리 꼭대기와 구역마다 하나. LandmarkBuilder)
    ///   2. 구역            봉우리 둘레 4구역 중 열린 구역만 쓴다. 닫힌 구역과의 경계에 두꺼운 덤불 벽을 세우고,
    ///                      닫힌 구역의 칸은 두 차원 모두에서 막는다 (ZoneMap)
    ///   3. 봉인된 모닥불    열린 구역에 서로 떨어뜨려 놓는다. 모두 꺼진 채, Real에서만 막히는 덤불 고리 안에 있다
    ///   4. 오염 웅덩이      Fake에서만 막는다. 절반은 모닥불 고리 바깥 가까이에 두어 Fake로 들어가는 길을 고르게 한다
    ///   5. 뿌리            Fake에서만 보인다. 꺼진 모닥불마다 Fake의 길을 따라 출구까지 뻗는다
    ///   6. 적              열린 구역에서, 플레이어와 떨어진 곳. 고리 안이나 웅덩이 위에는 놓지 않는다
    ///                      무리 지어 다니는 차원(Real)의 적은 packSize마리씩 무리를 짜서 우두머리 곁에 함께 놓는다
    ///   7. 돌              열린 구역 곳곳
    ///
    /// 무작위 자리는 "뽑아 보고, 조건에 안 맞으면 다시 뽑는" 방식으로 정한다.
    /// 모든 무작위 자리에 공통으로 적용되는 조건 (IsUsable):
    ///   - 지형의 가장자리에서 떨어져 있다
    ///   - 열린 구역에 있다
    ///   - 땅이 너무 가파르지 않다
    ///   - 길찾기 격자에서 두 차원 모두 걸을 수 있는 칸이다 (나무 기둥 안이 아니다)
    ///   - 덤불 고리 안이나 웅덩이 위가 아니다
    ///
    /// 높이는 프리팹에 놓인 높이를 읽지 않고, 종류마다 정해 둔 값만큼 그 자리의 땅 위에 올린다.
    ///
    /// 적과 모닥불은 Stage 프리팹에 하나씩만 넣어 두면, 그것을 견본으로 삼아 라운드 구성의 수만큼 복제한다.
    /// 적은 차원마다 따로 센다. 구성의 수가 0이면 그 차원의 견본도 꺼 둔다. (1라운드에는 Fake 적이 없다)
    ///
    /// 모닥불 주변의 나무와 풀은 라운드가 시작될 때 치워서, 덤불 고리와 겹치지 않게 한다.
    /// 숲은 Stage가 아니라 씬에 있으므로 지우지 않고 꺼 두었다가, 다음 라운드가 시작될 때 되돌린다.
    /// 씬에 지형(Terrain)이 없으면 아무것도 하지 않으므로, 프리팹의 배치가 그대로 쓰인다.
    /// 라운드의 밝기와 안개도 여기서 정한다. (RoundAtmosphere)
    ///
    /// 다른 스크립트의 Awake보다 먼저 실행된다.
    /// 적의 ChaserLocomotion이 Awake에서 "지금 서 있는 자리"의 땅 높이로 올라서기 때문에,
    /// 그 전에 자리를 옮겨 두어야 한다.
    ///
    /// 씬 구성:
    ///   Stage 프리팹의 가장 위 오브젝트에 붙인다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class StageLayout : MonoBehaviour
    {
        // 지형 — Forest Map Tool에 넣은 값과 같게
        private float hillRadius => GameTuning.Instance.stage.hillRadius;

        // 높이 — 오브젝트의 기준점(pivot)이 땅보다 얼마나 위에 있을지(m)
        private float exitHeight => GameTuning.Instance.stage.exitHeight;
        private float playerHeight => GameTuning.Instance.stage.playerHeight;
        private Vector2 exitPosition => GameTuning.Instance.stage.exitPosition;
        private float playerDistanceFromExit => GameTuning.Instance.stage.playerDistanceFromExit;

        // 무작위 배치
        private float edgeMargin => GameTuning.Instance.stage.edgeMargin;
        private float maxSlope => GameTuning.Instance.stage.maxSlope;
        private float objectSpacing => GameTuning.Instance.stage.objectSpacing;
        private float campfireSpacing => GameTuning.Instance.stage.campfireSpacing;
        private static MapTuning Map => GameTuning.Instance.map;

        [Header("○ 돌 (미끼)")]
        [Tooltip("돌로 쓸 모델(프리팹). 여러 개를 넣으면 놓을 때마다 그중 하나를 고른다. 비워 두면 돌을 놓지 않는다")]
        [SerializeField] private GameObject[] stoneModels;
        private int stoneCount => GameTuning.Instance.stone.count;
        private float stoneSize => GameTuning.Instance.stone.size;
        private float stoneSpacing => GameTuning.Instance.stone.spacing;

        // 조건에 맞는 자리가 나올 때까지 다시 뽑는 최대 횟수
        private const int MaxTries = 300;
        // Forest Map Tool이 나무와 풀을 심어 두는 씬 오브젝트의 이름
        private const string ForestPropsName = "Forest Props";
        // 출구와 플레이어 시작 자리 둘레에서 장애물을 비워 두는 거리(m)
        private const float StartClearance = 15f;

        // 지난 라운드에 모닥불 주변에서 치워 둔 나무와 풀.
        // Stage는 라운드마다 새로 만들어지지만 숲은 씬에 그대로 남으므로, 라운드를 넘어 기억해야 한다.
        private static readonly List<GameObject> _clearedProps = new();

        private Terrain _terrain;
        private Pathfinder _pathfinder;
        private RoundSetup _round;
        // 지형 한가운데의 월드 좌표 (높이는 쓰지 않는다)
        private Vector3 _center;
        // 가운데에서 가장자리 여백을 뺀 곳까지의 거리
        private float _halfExtent;
        private Vector3 _exitSpot;
        private Vector3 _playerSpot;
        // 이미 오브젝트를 놓은 자리들
        private readonly List<Vector3> _placed = new();
        // 다른 것을 놓으면 안 되는 원: 덤불 고리 안쪽과 웅덩이 (x, z, 반지름)
        private readonly List<Vector3> _keepOut = new();
        private readonly List<Vector3> _campfireSpots = new();
        // 닫힌 구역이라 막아 둔 칸. Stage가 사라질 때 풀어 준다.
        private List<Node> _closedZoneNodes;

        private void Awake()
        {
            RestoreClearedProps();

            _terrain = Terrain.activeTerrain;
            if (_terrain == null)
                return;

            _pathfinder = FindObjectOfType<Pathfinder>();
            int round = GameManager.Instance != null ? GameManager.Instance.CurrentRound : 1;
            _round = GameTuning.Instance.stage.Round(round);

            Vector3 size = _terrain.terrainData.size;
            _center = _terrain.transform.position + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
            _halfExtent = Mathf.Min(size.x, size.z) * 0.5f - edgeMargin;

            RoundAtmosphere.Apply(_round);
            _playerSpot = PlaceExitAndPlayer();
            PlaceLandmarks();
            BuildZones();
            List<Campfire> campfires = PlaceCampfires();
            PlacePools();
            BuildRoots(campfires);
            PlaceEnemies(_playerSpot);
            PlaceStones();
        }

        private void OnDestroy()
        {
            PathGrid.AddBlock(_closedZoneNodes, Dimension.None, -1);
        }

        // ── 고정 배치 ────────────────────────────────────────────────

        /// <summary>
        /// terrain 위에서 출구와 플레이어가 놓일 자리(높이는 0)를 계산한다.
        /// 나무를 심는 에디터 도구도 이 자리를 비워 두어야 하므로, 계산을 한 곳에 모아 밖에서도 쓸 수 있게 했다.
        /// </summary>
        public void GetFixedSpots(Terrain terrain, out Vector3 exitSpot, out Vector3 playerSpot)
        {
            Vector3 size = terrain.terrainData.size;
            Vector3 center = terrain.transform.position + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);

            exitSpot = center + new Vector3(exitPosition.x, 0f, exitPosition.y);

            // 출구에서 지형 한가운데를 바라보는 방향으로 조금 나온 곳이 "출구 앞"이다.
            Vector3 toCenter = center - exitSpot;
            if (toCenter.sqrMagnitude < 0.0001f)
                toCenter = Vector3.forward;
            playerSpot = exitSpot + toCenter.normalized * playerDistanceFromExit;
        }

        // 출구와 플레이어를 놓고, 플레이어의 자리를 돌려준다.
        private Vector3 PlaceExitAndPlayer()
        {
            GetFixedSpots(_terrain, out Vector3 exitSpot, out Vector3 playerSpot);
            _exitSpot = exitSpot;

            Exit exit = GetComponentInChildren<Exit>();
            if (exit != null)
                PlaceOnGround(exit.transform, exitSpot, exitHeight);

            Player player = GetComponentInChildren<Player>();
            if (player != null)
            {
                // CharacterController는 켜져 있는 동안 위치를 직접 바꾸면 무시할 수 있어서, 잠깐 끄고 옮긴다.
                CharacterController controller = player.GetComponent<CharacterController>();
                if (controller != null)
                    controller.enabled = false;

                PlaceOnGround(player.transform, playerSpot, playerHeight);

                if (controller != null)
                    controller.enabled = true;

                // 모닥불을 켜는 일은 플레이어 프리팹에 따로 붙이지 않아도 되도록 여기서 붙인다.
                if (player.GetComponent<CampfireLighter>() == null)
                    player.gameObject.AddComponent<CampfireLighter>();
            }

            return playerSpot;
        }

        // ── 랜드마크 ─────────────────────────────────────────────────

        // 랜드마크를 세우고, 그 둘레의 나무를 치우고, 다른 것이 겹쳐 놓이지 않게 비워 둔다.
        private void PlaceLandmarks()
        {
            List<Vector3> spots = LandmarkBuilder.BuildAll(transform, _center, stoneModels);
            float radius = LandmarkBuilder.FootprintRadius * Map.landmarkScale;
            foreach (Vector3 spot in spots)
                _keepOut.Add(new Vector3(spot.x, spot.z, radius));
            ClearPropsAround(spots, radius);
        }

        // ── 구역 ─────────────────────────────────────────────────────

        /// <summary>
        /// 닫힌 구역의 칸을 두 차원 모두에서 막고, 열린 구역과 닫힌 구역 사이의 경계에 두꺼운 덤불 벽을 세운다.
        /// 벽은 봉우리 꼭대기에서 지형의 모서리를 넘어서까지 뻗는다. (가장자리의 경계 벽과 빈틈없이 만나도록)
        /// </summary>
        private void BuildZones()
        {
            int open = Mathf.Clamp(_round.openZones, 1, ZoneMap.Count);
            if (open >= ZoneMap.Count)
                return;

            if (_pathfinder != null && _pathfinder.Grid != null)
            {
                _closedZoneNodes = _pathfinder.Grid.NodesWhere(p => !ZoneMap.IsOpen(p, _center, open));
                PathGrid.AddBlock(_closedZoneNodes, Dimension.None, 1);
            }

            float reach = (_halfExtent + edgeMargin) * 1.5f;
            foreach ((float angle, int a, int b) in ZoneMap.Boundaries)
            {
                if ((a < open) == (b < open))
                    continue;

                Vector3 end = _center + ZoneMap.Direction(angle) * reach;
                BarrierBuilder.BuildWall(transform, $"Zone Wall ({ZoneMap.Names[a]}|{ZoneMap.Names[b]})",
                                         _center, end, Dimension.None, Map.zoneWallTint, Map.zoneWallTrees);
            }
        }

        // ── 무작위 배치 ──────────────────────────────────────────────

        /// <summary>
        /// Stage에 들어 있는 T를 count개가 되도록 맞춰서 돌려준다.
        /// 정해진 개수보다 적으면, 첫 번째 것을 견본으로 삼아 모자란 만큼 복제하고,
        /// 많으면 남는 것을 꺼 둔다. (견본이 하나도 없으면 만들 수 없다)
        ///
        /// 복제본은 견본의 설정을 그대로 물려받으므로, 값을 바꿀 때는 견본 하나만 고치면 된다.
        /// </summary>
        private List<T> GetCopies<T>(List<T> group, int count) where T : Component
        {
            var copies = new List<T>();
            if (group.Count == 0)
                return copies;

            T sample = group[0];
            for (int i = 0; i < group.Count; i++)
            {
                if (i < count)
                    copies.Add(group[i]);
                else
                    group[i].gameObject.SetActive(false);
            }

            while (copies.Count < count)
            {
                T copy = Instantiate(sample, sample.transform.parent);
                copy.name = sample.name + " " + (copies.Count + 1);
                copy.gameObject.SetActive(true);
                copies.Add(copy);
            }

            return copies;
        }

        private void PlaceEnemies(Vector3 playerSpot)
        {
            // 규칙: 적은 플레이어에게서 적어도 봉우리의 지름만큼 떨어진 곳에서 시작한다.
            // 열린 구역이 좁은 라운드에서는 자리가 없을 수 있으므로, 못 찾으면 거리를 줄여 다시 찾는다.
            float minDistance = hillRadius * 2f;

            // 적의 높이는 여기서 정하지 않는다. 이 뒤에 실행되는 ChaserLocomotion의 Awake가
            // 자기 설정값(heightAboveGround)으로 땅 위에 올라서므로, 여기서는 땅에 놓기만 한다.
            // 무리 지어 다니는 차원의 적은 packSize마리씩 무리를 짜고, 우두머리를 먼저 놓은 뒤 졸개를 그 곁에 놓는다.
            var packs = new Dictionary<Dimension, (ChaserPack pack, Transform leader, int count)>();
            foreach (HorrorChaserAgent enemy in GetEnemyCopies())
            {
                DimensionMember member = enemy.GetComponent<DimensionMember>();
                Dimension dimension = member != null ? member.Dimension : Dimension.None;
                EnemyTuning tuning = GameTuning.Instance.EnemyFor(member);

                if (tuning.packSize <= 1)
                {
                    PlaceAwayFromPlayer(enemy.transform, playerSpot, minDistance, 0f);
                    continue;
                }

                // 지금 무리가 없거나 다 찼으면 이 적이 새 무리의 우두머리가 된다.
                if (!packs.TryGetValue(dimension, out var current) || current.count >= tuning.packSize)
                {
                    current = (new ChaserPack(), enemy.transform, 0);
                    PlaceAwayFromPlayer(enemy.transform, playerSpot, minDistance, 0f);
                }
                else
                {
                    PlaceNear(enemy.transform, current.leader.position, tuning.packSpread, playerSpot, minDistance * 0.5f);
                }

                current.pack.Add(enemy);
                enemy.Pack = current.pack;
                packs[dimension] = (current.pack, current.leader, current.count + 1);
            }
        }

        // anchor(우두머리) 곁, spread 안팎의 자리에 놓는다. 자리가 없으면 혼자 놓는다.
        private void PlaceNear(Transform target, Vector3 anchor, float spread, Vector3 playerSpot, float minPlayerDistance)
        {
            for (int i = 0; i < MaxTries; i++)
            {
                Vector3 spot = anchor + ZoneMap.Direction(Random.Range(0f, 360f)) * Random.Range(spread * 0.5f, spread * 1.5f);
                if (IsUsable(spot) && FlatDistance(spot, playerSpot) >= minPlayerDistance)
                {
                    PlaceOnGround(target, spot, 0f);
                    return;
                }
            }
            PlaceAwayFromPlayer(target, playerSpot, minPlayerDistance, 0f);
        }

        /// <summary>
        /// 차원마다 적이 라운드 구성의 수(realEnemies / fakeEnemies)가 되도록 맞춰서, 놓을 적을 모두 돌려준다.
        /// 차원마다 첫 번째 적을 견본으로 삼아 복제한다.
        /// (Real 적은 Real 적끼리, Fake 적은 Fake 적끼리 복제하므로 모습과 레이어가 그대로 따라간다)
        /// </summary>
        private List<HorrorChaserAgent> GetEnemyCopies()
        {
            var byDimension = new Dictionary<Dimension, List<HorrorChaserAgent>>();
            foreach (HorrorChaserAgent enemy in GetComponentsInChildren<HorrorChaserAgent>(true))
            {
                Dimension dimension = enemy.TryGetComponent(out DimensionMember member) ? member.Dimension : Dimension.None;
                if (!byDimension.TryGetValue(dimension, out List<HorrorChaserAgent> group))
                    byDimension[dimension] = group = new List<HorrorChaserAgent>();
                group.Add(enemy);
            }

            var all = new List<HorrorChaserAgent>();
            foreach (KeyValuePair<Dimension, List<HorrorChaserAgent>> pair in byDimension)
            {
                int count = pair.Key == Dimension.Fake ? _round.fakeEnemies : _round.realEnemies;
                all.AddRange(GetCopies(pair.Value, Mathf.Max(0, count)));
            }
            return all;
        }

        /// <summary>
        /// 꺼진 모닥불을 열린 구역에 서로 떨어뜨려 놓고, 하나하나를 Real에서만 막히는 덤불 고리로 둘러싼다.
        /// 고리가 닫힌 구역이나 지형 밖으로 걸치지 않고, 출구와 플레이어 시작 자리에서도 떨어진 자리만 고른다.
        /// </summary>
        private List<Campfire> PlaceCampfires()
        {
            var placed = new List<Campfire>();
            var group = new List<Campfire>(GetComponentsInChildren<Campfire>(true));
            float ringOuter = Map.ringRadius + Map.ringThickness;
            int open = Mathf.Clamp(_round.openZones, 1, ZoneMap.Count);

            foreach (Campfire campfire in GetCopies(group, Mathf.Max(0, _round.campfires)))
            {
                Vector3 spot = _center;
                bool found = false;
                for (int i = 0; i < MaxTries && !found; i++)
                {
                    spot = RandomSpot();
                    found = IsUsable(spot)
                            && RingFits(spot, ringOuter + 2f, open)
                            && FlatDistance(spot, _playerSpot) >= ringOuter + StartClearance
                            && FlatDistance(spot, _exitSpot) >= ringOuter + StartClearance;
                    foreach (Vector3 other in _campfireSpots)
                        if (FlatDistance(spot, other) < campfireSpacing)
                            found = false;
                }

                // 자리가 없으면 억지로 놓지 않고 이번 것은 꺼 둔다.
                if (!found)
                {
                    campfire.gameObject.SetActive(false);
                    continue;
                }

                _campfireSpots.Add(spot);
                // 모닥불은 기준점이 땅에 닿는 지점이 되도록 만들어져 있으므로 띄우지 않고 그대로 땅에 놓는다.
                PlaceOnGround(campfire.transform, spot, 0f);
                DimensionObstacle ring = BarrierBuilder.BuildRing(
                    transform, "Campfire Ring", spot, Map.ringRadius, Dimension.Real, Map.ringTint);
                campfire.Seal(ring);
                _keepOut.Add(new Vector3(spot.x, spot.z, ringOuter + 1f));
                placed.Add(campfire);
            }

            ClearPropsAround(_campfireSpots, ringOuter + 1f);
            if (GameManager.Instance != null)
                GameManager.Instance.SetPlacedCampfires(placed.Count);
            return placed;
        }

        /// <summary>
        /// Fake에서만 막는 오염 웅덩이를 놓는다.
        /// 절반은 모닥불 고리 바로 바깥에 두어, Fake로 넘어가 고리 안으로 들어갈 방향을 고르게 한다.
        /// (고리 쪽 길이 다 막히지 않도록 고리에 닿지는 않게 둔다) 나머지는 열린 구역 곳곳에 흩는다.
        /// </summary>
        private void PlacePools()
        {
            int count = Mathf.Max(0, _round.contaminationPools);
            var poolSpots = new List<Vector3>();
            float ringOuter = Map.ringRadius + Map.ringThickness;

            for (int n = 0; n < count; n++)
            {
                float radius = Random.Range(Map.poolRadiusMin, Map.poolRadiusMax);
                bool nearFire = n % 2 == 0 && _campfireSpots.Count > 0;
                Vector3 fire = nearFire ? _campfireSpots[(n / 2) % _campfireSpots.Count] : Vector3.zero;

                Vector3 spot = _center;
                bool found = false;
                for (int i = 0; i < MaxTries && !found; i++)
                {
                    if (nearFire)
                    {
                        float distance = ringOuter + radius + Random.Range(2f, 12f);
                        spot = fire + ZoneMap.Direction(Random.Range(0f, 360f)) * distance;
                    }
                    else
                    {
                        spot = RandomSpot();
                    }

                    found = IsUsable(spot, radius + 1f)
                            && FlatDistance(spot, _playerSpot) >= radius + StartClearance
                            && FlatDistance(spot, _exitSpot) >= radius + StartClearance;
                    foreach (Vector3 other in poolSpots)
                        if (FlatDistance(spot, other) < radius + Map.poolRadiusMax + 2f)
                            found = false;
                }
                if (!found)
                    continue;

                BarrierBuilder.BuildPool(transform, spot, radius);
                poolSpots.Add(spot);
                _keepOut.Add(new Vector3(spot.x, spot.z, radius + 1f));
            }
        }

        /// <summary>
        /// 꺼진 모닥불마다 출구까지 이어진 뿌리를 만든다. Fake의 길찾기 격자로 길을 내므로 웅덩이를 피해 돌아간다.
        /// (덤불 고리는 Real에서만 막으므로 뿌리는 고리를 가로질러 불까지 닿는다) 길이 없으면 곧게 잇는다.
        /// </summary>
        private void BuildRoots(List<Campfire> campfires)
        {
            foreach (Campfire campfire in campfires)
            {
                Vector3 fire = campfire.transform.position;
                List<Vector3> path = _pathfinder != null ? _pathfinder.FindPath(fire, _exitSpot, Dimension.Fake) : null;

                var points = new List<Vector3> { fire };
                if (path != null)
                    points.AddRange(path);
                else
                    for (float t = 0.05f; t < 1f; t += 0.05f)
                        points.Add(Vector3.Lerp(fire, _exitSpot, t));
                points.Add(_exitSpot);

                RootLine root = RootLine.Build(transform, points);
                campfire.ConnectRoot(root);
            }
        }

        /// <summary>
        /// 미끼로 쓸 돌을 맵 곳곳에 놓는다. 돌 모델을 복제해서 Stage의 자식으로 두므로 라운드가 끝나면 함께 사라진다.
        /// 모델마다 크기가 달라도 가장 긴 변이 stoneSize가 되도록 맞추고, 하나하나 방향과 크기를 조금씩 바꿔 단조롭지 않게 한다.
        /// </summary>
        private void PlaceStones()
        {
            if (stoneModels == null || stoneModels.Length == 0)
                return;

            var stoneSpots = new List<Vector3>();
            for (int n = 0; n < stoneCount; n++)
            {
                GameObject model = stoneModels[Random.Range(0, stoneModels.Length)];
                if (model == null)
                    continue;

                Vector3 spot = _center;
                bool found = false;
                for (int i = 0; i < MaxTries && !found; i++)
                {
                    spot = RandomSpot();
                    found = IsUsable(spot) && IsFarFromPlaced(spot, objectSpacing * 0.5f);
                    foreach (Vector3 other in stoneSpots)
                        if (FlatDistance(spot, other) < stoneSpacing)
                            found = false;
                }
                if (!found)
                    continue;

                GameObject stoneObject = Instantiate(model, transform);
                stoneObject.name = "Stone";
                stoneObject.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                // 가장 긴 변이 stoneSize가 되도록 맞춘다.
                Renderer body = stoneObject.GetComponentInChildren<Renderer>();
                float longest = body != null ? Mathf.Max(body.bounds.size.x, body.bounds.size.y, body.bounds.size.z) : 1f;
                if (longest > 0.0001f)
                    stoneObject.transform.localScale *= stoneSize / longest * Random.Range(0.8f, 1.2f);

                // 돌이 땅에 놓였을 때의 높이는 Stone이 크기를 재서 정한다.
                Stone stone = stoneObject.AddComponent<Stone>();
                PlaceOnGround(stoneObject.transform, spot, 0f);
                stone.SnapToGround();
                stoneSpots.Add(spot);
            }
        }

        // spots 주변의 나무와 풀을 꺼서 치운다. 꺼 둔 것은 기억해 두었다가 다음 라운드에 되돌린다.
        // 길찾기 격자는 씬이 시작될 때 숲 전체를 기준으로 구워 둔 그대로 쓴다.
        // (치운 나무 자리의 칸은 계속 막힌 칸으로 남지만, 추격자가 그 한두 칸을 돌아갈 뿐이다)
        private void ClearPropsAround(List<Vector3> spots, float radius)
        {
            if (radius <= 0f || spots.Count == 0)
                return;

            GameObject props = GameObject.Find(ForestPropsName);
            if (props == null)
                return;

            // 숲은 "Forest Props > Trees / Plants > 나무와 풀 하나하나"로 되어 있다.
            foreach (Transform group in props.transform)
            {
                foreach (Transform prop in group)
                {
                    foreach (Vector3 spot in spots)
                    {
                        if (FlatDistance(prop.position, spot) >= radius)
                            continue;

                        prop.gameObject.SetActive(false);
                        _clearedProps.Add(prop.gameObject);
                        break;
                    }
                }
            }
        }

        // 지난 라운드에 치워 둔 나무와 풀을 다시 켠다.
        private static void RestoreClearedProps()
        {
            foreach (GameObject prop in _clearedProps)
                if (prop != null)
                    prop.SetActive(true);

            _clearedProps.Clear();
        }

        // 열린 구역에서 고르게 뽑되, 플레이어에게서 minPlayerDistance 이상 떨어진 자리에 놓는다.
        // 자리가 없으면 거리를 절반씩 줄여 가며 다시 찾는다.
        private void PlaceAwayFromPlayer(
            Transform target, Vector3 playerSpot, float minPlayerDistance, float heightAboveGround)
        {
            Vector3 spot = _center;
            bool found = false;

            for (int attempt = 0; attempt < 3 && !found; attempt++)
            {
                for (int i = 0; i < MaxTries && !found; i++)
                {
                    spot = RandomSpot();
                    found = IsUsable(spot)
                            && FlatDistance(spot, playerSpot) >= minPlayerDistance
                            && IsFarFromPlaced(spot, objectSpacing);
                }
                minPlayerDistance *= 0.5f;
            }

            PlaceRandom(target, spot, found, heightAboveGround);
        }

        // 무작위로 뽑은 자리에 놓는다. 끝내 조건에 맞는 자리를 못 찾았으면 마지막 후보에 놓고 알린다.
        private void PlaceRandom(Transform target, Vector3 spot, bool found, float heightAboveGround)
        {
            if (!found)
                Debug.LogWarning(
                    $"[StageLayout] {target.name}: 조건에 맞는 자리를 찾지 못해 마지막 후보에 놓았습니다. " +
                    "거리 조건이 지형 크기에 비해 너무 크지 않은지 확인하세요.", target);

            PlaceOnGround(target, spot, heightAboveGround);
        }

        // ── 자리 검사 ────────────────────────────────────────────────

        private Vector3 RandomSpot()
            => _center + new Vector3(Random.Range(-_halfExtent, _halfExtent), 0f, Random.Range(-_halfExtent, _halfExtent));

        // 무작위 자리가 공통 조건에 맞는가. margin만큼 넉넉하게 장애물을 피한다.
        private bool IsUsable(Vector3 spot, float margin = 0f)
        {
            Vector3 fromCenter = spot - _center;
            if (Mathf.Abs(fromCenter.x) > _halfExtent || Mathf.Abs(fromCenter.z) > _halfExtent)
                return false;

            if (!ZoneMap.IsOpen(spot, _center, Mathf.Clamp(_round.openZones, 1, ZoneMap.Count)))
                return false;

            foreach (Vector3 circle in _keepOut)
                if (Vector2.Distance(new Vector2(spot.x, spot.z), new Vector2(circle.x, circle.y)) < circle.z + margin)
                    return false;

            // GetSteepness는 지형의 한쪽 끝을 0, 반대쪽 끝을 1로 본 비율 좌표를 받는다.
            Vector3 size = _terrain.terrainData.size;
            Vector3 local = spot - _terrain.transform.position;
            if (_terrain.terrainData.GetSteepness(local.x / size.x, local.z / size.z) > maxSlope)
                return false;

            return _pathfinder == null || _pathfinder.IsWalkable(spot);
        }

        // center 둘레 radius의 원이 지형 안, 열린 구역 안에 다 들어오는가. (덤불 고리가 벽이나 가장자리에 걸치지 않게)
        private bool RingFits(Vector3 center, float radius, int openZones)
        {
            const int Samples = 16;
            for (int i = 0; i < Samples; i++)
            {
                Vector3 point = center + ZoneMap.Direction(i * 360f / Samples) * radius;
                Vector3 fromCenter = point - _center;
                if (Mathf.Abs(fromCenter.x) > _halfExtent || Mathf.Abs(fromCenter.z) > _halfExtent)
                    return false;
                if (!ZoneMap.IsOpen(point, _center, openZones))
                    return false;
            }
            return true;
        }

        private bool IsFarFromPlaced(Vector3 spot, float minDistance)
        {
            foreach (Vector3 placed in _placed)
                if (FlatDistance(spot, placed) < minDistance)
                    return false;

            return true;
        }

        // 높이를 뺀, 위에서 내려다본 거리
        private static float FlatDistance(Vector3 a, Vector3 b)
            => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        // ── 놓기 ─────────────────────────────────────────────────────

        // target을 spot의 xz 자리로 옮기고, 그 자리의 땅에서 heightAboveGround만큼 위에 올린다.
        private void PlaceOnGround(Transform target, Vector3 spot, float heightAboveGround)
        {
            // SampleHeight는 지형 오브젝트의 위치를 뺀 높이를 주므로 다시 더한다.
            spot.y = _terrain.SampleHeight(spot) + _terrain.transform.position.y + heightAboveGround;
            target.position = spot;

            _placed.Add(spot);
        }
    }
}
