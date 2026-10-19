using System.Collections.Generic;
using Astar3D;
using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 라운드가 시작될 때(Stage가 생성될 때) Stage 안의 오브젝트들을 지형 위에 배치한다.
    ///
    /// 고정 배치:
    ///   출구      항상 같은 자리
    ///   플레이어  출구 앞 (출구에서 지형 한가운데 쪽으로 조금 나온 자리)
    ///
    /// 무작위 배치 (라운드마다 달라진다):
    ///   발전기  대체로 봉우리 너머, 플레이어의 반대쪽에 서로 떨어뜨려 흩뿌린다
    ///   적      플레이어에게서 적어도 봉우리의 지름만큼 떨어진 곳
    ///   모닥불  맵 곳곳에, 서로 떨어뜨려 놓는다 (길잡이 역할)
    ///
    /// 무작위 자리는 "뽑아 보고, 조건에 안 맞으면 다시 뽑는" 방식으로 정한다.
    /// 모든 무작위 자리에 공통으로 적용되는 조건:
    ///   - 지형의 가장자리에서 떨어져 있다
    ///   - 땅이 너무 가파르지 않다
    ///   - 길찾기 격자에서 걸을 수 있는 칸이다 (나무 기둥 안이 아니다)
    ///   - 먼저 놓인 오브젝트와 너무 가깝지 않다
    ///
    /// 높이는 프리팹에 놓인 높이를 읽지 않고, 종류마다 정해 둔 값(○ 높이)만큼 그 자리의 땅 위에 올린다.
    /// 그래서 프리팹 안에서는 오브젝트를 어느 위치, 어느 높이에 두든 결과가 같다.
    /// (모델의 크기를 바꿔서 땅에 묻히거나 뜨면 프리팹의 높이가 아니라 ○ 높이의 값을 고친다)
    ///
    /// 적의 개수는 정하지 않는다. Stage 프리팹에 들어 있는 것을 모두 찾아서 옮긴다.
    /// 발전기와 모닥불은 개수를 숫자로 정한다. (여러 개를 프리팹에 일일이 넣지 않도록)
    /// Stage 프리팹에 하나씩만 넣어 두면, 그것을 견본으로 삼아 모자란 만큼 복제한다.
    ///
    /// 모닥불 주변의 나무와 풀은 라운드가 시작될 때 치워서, 모닥불이 숲에 가려지지 않게 한다.
    /// 숲은 Stage가 아니라 씬에 있으므로 지우지 않고 꺼 두었다가, 다음 라운드가 시작될 때 되돌린다.
    /// 씬에 지형(Terrain)이 없으면 아무것도 하지 않으므로, 프리팹의 배치가 그대로 쓰인다.
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
        [Header("○ 지형 — Forest Map Tool에 넣은 값과 같게")]
        [Tooltip("봉우리의 반경(m). 적은 플레이어에게서 이 값의 2배(지름) 이상 떨어져 생성된다")]
        [SerializeField] private float hillRadius = 40f;

        [Header("○ 높이 — 오브젝트의 기준점(pivot)이 땅보다 얼마나 위에 있을지(m)")]
        [Tooltip("출구")]
        [SerializeField] private float exitHeight = 13.63f;
        [Tooltip("플레이어. 기준점이 몸 한가운데(발에서 1m 위)이므로 1보다 조금 크게 둔다. 값이 크면 그만큼 공중에서 떨어지며 시작한다")]
        [SerializeField] private float playerHeight = 1.2f;
        [Tooltip("발전기")]
        [SerializeField] private float generatorHeight = 4.11f;

        [Header("○ 고정 배치")]
        [Tooltip("출구의 자리. 지형 한가운데를 (0, 0)으로 본 (x, z)")]
        [SerializeField] private Vector2 exitPosition = new Vector2(0f, -84f);
        [Tooltip("플레이어가 출구에서 지형 한가운데 쪽으로 얼마나 나와서 시작할지(m)")]
        [SerializeField] private float playerDistanceFromExit = 25f;

        [Header("○ 무작위 배치")]
        [Tooltip("지형 가장자리에서 이만큼(m)은 비워 둔다")]
        [SerializeField] private float edgeMargin = 10f;
        [Tooltip("땅의 기울기가 이 각도(도)보다 가파른 곳에는 놓지 않는다")]
        [SerializeField] private float maxSlope = 25f;
        [Tooltip("라운드마다 차원별로 놓을 적의 수. Stage 프리팹에는 차원마다 적을 하나씩만 넣어 두면 되고, " +
                 "모자란 만큼 같은 차원의 적을 복제한다 (3이면 Real 3마리, Fake 3마리)")]
        [SerializeField] private int enemiesPerDimension = 3;
        [Tooltip("적과 모닥불이 먼저 놓인 오브젝트에서 적어도 이만큼(m)은 떨어지게 한다")]
        [SerializeField] private float objectSpacing = 10f;

        [Header("○ 모닥불")]
        [Tooltip("라운드마다 놓을 모닥불의 개수. Stage 프리팹에는 모닥불을 하나만 넣어 두면 되고, 모자란 만큼 복제한다")]
        [SerializeField] private int campfireCount = 6;
        [Tooltip("모닥불끼리 적어도 이만큼(m)은 떨어지게 한다")]
        [SerializeField] private float campfireSpacing = 40f;
        [Tooltip("모닥불에서 이 거리(m) 안에 있는 나무와 풀을 치워서 모닥불이 잘 보이게 한다. 0이면 치우지 않는다")]
        [SerializeField] private float campfireClearRadius = 12f;

        [Header("○ 돌 (미끼)")]
        [Tooltip("돌로 쓸 모델(프리팹). 여러 개를 넣으면 놓을 때마다 그중 하나를 고른다. 비워 두면 돌을 놓지 않는다")]
        [SerializeField] private GameObject[] stoneModels;
        [Tooltip("라운드마다 맵 곳곳에 놓을 돌의 개수")]
        [SerializeField] private int stoneCount = 20;
        [Tooltip("돌의 크기(m). 모델의 가장 긴 변이 이 길이가 되도록 맞춘다")]
        [SerializeField] private float stoneSize = 1f;
        [Tooltip("돌끼리 적어도 이만큼(m)은 떨어지게 한다")]
        [SerializeField] private float stoneSpacing = 12f;

        // 조건에 맞는 자리가 나올 때까지 다시 뽑는 최대 횟수
        private const int MaxTries = 300;
        // Forest Map Tool이 나무와 풀을 심어 두는 씬 오브젝트의 이름
        private const string ForestPropsName = "Forest Props";

        // 지난 라운드에 모닥불 주변에서 치워 둔 나무와 풀.
        // Stage는 라운드마다 새로 만들어지지만 숲은 씬에 그대로 남으므로, 라운드를 넘어 기억해야 한다.
        private static readonly List<GameObject> _clearedProps = new();

        private Terrain _terrain;
        private Pathfinder _pathfinder;
        // 지형 한가운데의 월드 좌표 (높이는 쓰지 않는다)
        private Vector3 _center;
        // 가운데에서 가장자리 여백을 뺀 곳까지의 거리
        private float _halfExtent;
        // 이미 오브젝트를 놓은 자리들
        private readonly List<Vector3> _placed = new();

        private void Awake()
        {
            RestoreClearedProps();

            _terrain = Terrain.activeTerrain;
            if (_terrain == null)
                return;

            _pathfinder = FindObjectOfType<Pathfinder>();

            Vector3 size = _terrain.terrainData.size;
            _center = _terrain.transform.position + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
            _halfExtent = Mathf.Min(size.x, size.z) * 0.5f - edgeMargin;

            Vector3 playerSpot = PlaceExitAndPlayer();
            PlaceEnemies(playerSpot);
            // 모닥불은 자리가 조금 달라져도 상관없으므로, 중요한 것들을 먼저 놓은 뒤 남은 자리에 놓는다.
            PlaceCampfires();
            PlaceStones();
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
            }

            return playerSpot;
        }

        // ── 무작위 배치 ──────────────────────────────────────────────

        /// <summary>
        /// Stage에 들어 있는 T(발전기, 모닥불)를 count개가 되도록 맞춰서 돌려준다.
        /// 정해진 개수보다 적으면, 첫 번째 것을 견본으로 삼아 모자란 만큼 복제한다.
        /// (견본이 하나도 없으면 만들 수 없고, 이미 개수보다 많으면 지우지 않고 그대로 쓴다)
        ///
        /// 복제본은 견본의 설정을 그대로 물려받으므로, 값을 바꿀 때는 견본 하나만 고치면 된다.
        /// </summary>
        private List<T> GetCopies<T>(int count) where T : Component
        {
            var copies = new List<T>(GetComponentsInChildren<T>());
            if (copies.Count == 0)
                return copies;

            T sample = copies[0];
            while (copies.Count < count)
            {
                T copy = Instantiate(sample, sample.transform.parent);
                copy.name = sample.name;
                copies.Add(copy);
            }

            return copies;
        }

        private void PlaceEnemies(Vector3 playerSpot)
        {
            // 규칙: 적은 플레이어에게서 적어도 봉우리의 지름만큼 떨어진 곳에서 시작한다.
            float minDistance = hillRadius * 2f;

            // 적의 높이는 여기서 정하지 않는다. 이 뒤에 실행되는 ChaserLocomotion의 Awake가
            // 자기 설정값(heightAboveGround)으로 땅 위에 올라서므로, 여기서는 땅에 놓기만 한다.
            foreach (HorrorChaserAgent enemy in GetEnemyCopies())
                PlaceAwayFromPlayer(enemy.transform, playerSpot, minDistance, 0f);
        }

        /// <summary>
        /// 차원마다 적이 enemiesPerDimension마리가 되도록 맞춰서 모두 돌려준다.
        /// 발전기나 모닥불과 같은 방식으로, 차원마다 첫 번째 적을 견본으로 삼아 모자란 만큼 복제한다.
        /// (Real 적은 Real 적끼리, Fake 적은 Fake 적끼리 복제하므로 모습과 레이어가 그대로 따라간다)
        /// </summary>
        private List<HorrorChaserAgent> GetEnemyCopies()
        {
            var all = new List<HorrorChaserAgent>(GetComponentsInChildren<HorrorChaserAgent>());
            var byDimension = new Dictionary<Dimension, List<HorrorChaserAgent>>();
            foreach (HorrorChaserAgent enemy in all)
            {
                Dimension dimension = enemy.TryGetComponent(out DimensionMember member) ? member.Dimension : Dimension.None;
                if (!byDimension.TryGetValue(dimension, out List<HorrorChaserAgent> group))
                    byDimension[dimension] = group = new List<HorrorChaserAgent>();
                group.Add(enemy);
            }

            foreach (List<HorrorChaserAgent> group in byDimension.Values)
            {
                HorrorChaserAgent sample = group[0];
                for (int i = group.Count; i < enemiesPerDimension; i++)
                {
                    HorrorChaserAgent copy = Instantiate(sample, sample.transform.parent);
                    copy.name = sample.name + " " + (i + 1);
                    all.Add(copy);
                }
            }

            return all;
        }

        // 모닥불을 지형 전체에 서로 떨어뜨려 놓고, 그 주변의 나무와 풀을 치운다.
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
                    spot = _center + new Vector3(
                        Random.Range(-_halfExtent, _halfExtent), 0f, Random.Range(-_halfExtent, _halfExtent));
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

        private void PlaceCampfires()
        {
            var campfireSpots = new List<Vector3>();

            foreach (Campfire campfire in GetCopies<Campfire>(campfireCount))
            {
                Vector3 spot = _center;
                bool found = false;

                for (int i = 0; i < MaxTries && !found; i++)
                {
                    spot = _center + new Vector3(
                        Random.Range(-_halfExtent, _halfExtent), 0f, Random.Range(-_halfExtent, _halfExtent));

                    found = IsUsable(spot) && IsFarFromPlaced(spot, objectSpacing);
                    foreach (Vector3 other in campfireSpots)
                        if (FlatDistance(spot, other) < campfireSpacing)
                            found = false;
                }

                // 자리가 없으면 억지로 놓지 않고 이번 것은 꺼 둔다. 모닥불은 하나쯤 적어도 게임이 진행된다.
                if (!found)
                {
                    campfire.gameObject.SetActive(false);
                    continue;
                }

                campfireSpots.Add(spot);
                // 모닥불은 기준점이 땅에 닿는 지점이 되도록 만들어져 있으므로 띄우지 않고 그대로 땅에 놓는다.
                PlaceOnGround(campfire.transform, spot, 0f);
            }

            ClearPropsAround(campfireSpots);
        }

        // spots 주변의 나무와 풀을 꺼서 치운다. 꺼 둔 것은 기억해 두었다가 다음 라운드에 되돌린다.
        // 길찾기 격자는 씬이 시작될 때 숲 전체를 기준으로 구워 둔 그대로 쓴다.
        // (치운 나무 자리의 칸은 계속 막힌 칸으로 남지만, 추격자가 그 한두 칸을 돌아갈 뿐이다)
        private void ClearPropsAround(List<Vector3> spots)
        {
            if (campfireClearRadius <= 0f)
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
                        if (FlatDistance(prop.position, spot) >= campfireClearRadius)
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

        // 지형 전체에서 고르게 뽑되, 플레이어에게서 minPlayerDistance 이상 떨어진 자리에 놓는다.
        private void PlaceAwayFromPlayer(
            Transform target, Vector3 playerSpot, float minPlayerDistance, float heightAboveGround)
        {
            Vector3 spot = _center;
            bool found = false;

            for (int i = 0; i < MaxTries && !found; i++)
            {
                spot = _center + new Vector3(
                    Random.Range(-_halfExtent, _halfExtent), 0f, Random.Range(-_halfExtent, _halfExtent));

                found = IsUsable(spot)
                        && FlatDistance(spot, playerSpot) >= minPlayerDistance
                        && IsFarFromPlaced(spot, objectSpacing);
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

        // 무작위 자리가 공통 조건(가장자리, 기울기, 걸을 수 있는 칸)에 맞는가
        private bool IsUsable(Vector3 spot)
        {
            Vector3 fromCenter = spot - _center;
            if (Mathf.Abs(fromCenter.x) > _halfExtent || Mathf.Abs(fromCenter.z) > _halfExtent)
                return false;

            // GetSteepness는 지형의 한쪽 끝을 0, 반대쪽 끝을 1로 본 비율 좌표를 받는다.
            Vector3 size = _terrain.terrainData.size;
            Vector3 local = spot - _terrain.transform.position;
            if (_terrain.terrainData.GetSteepness(local.x / size.x, local.z / size.z) > maxSlope)
                return false;

            return _pathfinder == null || _pathfinder.IsWalkable(spot);
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
