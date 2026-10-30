using System.Collections.Generic;
using UnityEngine;

namespace Astar3D
{
    /// <summary>
    /// Builds and owns the 3D node grid, laid out on the XZ plane (the floor).
    ///
    /// NAMED "PathGrid", NOT "Grid": Unity has a built-in UnityEngine.Grid
    /// component (used by Tilemap). Naming your class "Grid" makes the editor
    /// try to attach the built-in GridEditor and throw
    /// SerializedObjectNotCreatableException. Avoid built-in names.
    ///
    /// Walkability is sampled with Physics.CheckBox against an "unwalkable"
    /// layer mask, so any 3D collider (walls, props, level geometry) blocks a
    /// cell — no Tilemap required.
    /// </summary>
    public class PathGrid : MonoBehaviour
    {
        [Header("○ 튜닝 값 — 자유롭게 조절")]
        [Tooltip("전체 월드의 크기")]
        [SerializeField] private Vector2 gridWorldSize = new Vector2(40, 40);

        [Tooltip("셀 하나의 반경. 셀의 크기는 이 값의 2배")]
        [SerializeField] private float nodeRadius = 0.5f;

        [Tooltip("장애물로 감지하는 높이")]
        [SerializeField] private float obstacleCheckHeight = 10000f;

        [Tooltip("지정한 레이어의 물체들을 장애물로 인식")]
        [SerializeField] private LayerMask unwalkableMask;

        [Tooltip("대각선 이동 허용 여부. True이면 8방향, False면 4방향")]
        [SerializeField] private bool allowDiagonal = true;
        [Tooltip("Scene 화면에 격자 타일(흰색: 이동 가능, 빨간색: 장애물)을 그린다")]
        [SerializeField] private bool drawGridGizmos = true;
        [Tooltip("Scene 화면에 적의 경로(시안색)를 그린다")]
        [SerializeField] private bool drawPathGizmos = true;

        private Node[,] _grid;
        private float _nodeDiameter;
        private int _gridSizeX;
        private int _gridSizeY;   // count along Z

        public int MaxSize => _gridSizeX * _gridSizeY;
        public bool AllowDiagonal => allowDiagonal;

        private void Awake() => BuildGrid();

        /// <summary>
        /// 게임이 시작되면 호출되는 함수.
        /// 월드 상에서 도달 가능한 모든 지역을 Node로 그려낸다.
        /// </summary>
        public void BuildGrid()
        {
            _nodeDiameter = nodeRadius * 2;
            _gridSizeX = Mathf.RoundToInt(gridWorldSize.x / _nodeDiameter);
            _gridSizeY = Mathf.RoundToInt(gridWorldSize.y / _nodeDiameter);
            _grid = new Node[_gridSizeX, _gridSizeY];

            // Bottom-left corner of the grid on the XZ plane.
            Vector3 worldBottomLeft = transform.position
                                      + Vector3.left * gridWorldSize.x / 2
                                      + Vector3.back * gridWorldSize.y / 2;

            // 각 칸에 벽이 있는지 검사하는 데 쓰이는
            // 검사 박스의 radius 값을 담는 변수.
            // x, z는 노드의 radius 값에 0.9를 곱하여
            // 1.0으로 설정했을 때 바로 옆칸의 벽까지
            // 판정되는 일이 없도록 함.
            Vector3 boxHalf = new Vector3(nodeRadius * 0.9f,
                                        obstacleCheckHeight * 0.5f,
                                        nodeRadius * 0.9f);

            for (int x = 0; x < _gridSizeX; x++)
            {
                for (int y = 0; y < _gridSizeY; y++)
                {
                    // x, y번째 노드의 중심 좌표 (xz 평면 상에서)
                    Vector3 worldPoint = worldBottomLeft
                        + Vector3.right * (x * _nodeDiameter + nodeRadius)
                        + Vector3.forward * (y * _nodeDiameter + nodeRadius);

                    // x, y번째 노드의 중심 좌표 (장애물 높이 감지 위해 연직 위 좌표 추가))
                    Vector3 boxCenter = worldPoint + Vector3.up * (obstacleCheckHeight * 0.5f);
                    
                    // center로부터 half 반경의 공간으로 이루어진
                    // 눈에 보이지 않는 충돌 판정 측정 박스가
                    // unwalkableMask에 지정된 레이어의 오브젝트와 겹치는지 판정.
                    bool walkable = !Physics.CheckBox(
                        boxCenter, boxHalf, Quaternion.identity, unwalkableMask);

                    _grid[x, y] = new Node(walkable, worldPoint, x, y);
                }
            }
        }

        /// <summary>
        /// 실제 유니티 상의 좌표를 받아서, 그 좌표가 있는 노드의 번호를 반환하는 함수.
        /// </summary>
        public Node NodeFromWorldPoint(Vector3 worldPosition)
        {
            // 맵의 왼쪽 아래 끝 기준으로 벡터값을 변환
            Vector3 local = worldPosition - transform.position
                            + new Vector3(gridWorldSize.x / 2, 0, gridWorldSize.y / 2);

            // 노드 한 칸의 지름으로 나눈 몫이 곧 몇 번째 칸인지다.
            // (이전의 "비율 × (칸 수 - 1)을 반올림" 방식은 가장자리로 갈수록
            //  최대 반 칸씩 어긋나서, 적이 벽 옆 칸에 서 있으면 벽 칸으로 읽혀
            //  길찾기가 실패하고 그 자리에 멈추는 문제가 있었다)
            int x = Mathf.Clamp(Mathf.FloorToInt(local.x / _nodeDiameter), 0, _gridSizeX - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(local.z / _nodeDiameter), 0, _gridSizeY - 1);  // Z maps to grid Y
            return _grid[x, y];
        }

        /// <summary>
        /// 좌표가 있는 노드가 벽이면, 주변에서 가장 가까운 walkable 노드를 찾아 반환한다.
        /// 적이나 플레이어가 벽에 바짝 붙어 서면 그 칸이 벽으로 판정될 수 있는데,
        /// 그대로 두면 길찾기가 실패해서 적이 멈춰 버리기 때문에 쓰는 방어 코드.
        /// maxRing 칸 안에 walkable 노드가 없으면 null.
        /// dimension: 어느 차원에서 걸을 수 있는 칸을 찾는가 (None이면 두 차원 모두에서 걸을 수 있는 칸)
        /// </summary>
        public Node ClosestWalkableNode(Vector3 worldPosition, int maxRing = 2, Dimension dimension = Dimension.None)
        {
            Node origin = NodeFromWorldPoint(worldPosition);
            if (origin.IsWalkableIn(dimension))
                return origin;

            Node best = null;
            float bestDist = float.MaxValue;

            for (int dx = -maxRing; dx <= maxRing; dx++)
            {
                for (int dy = -maxRing; dy <= maxRing; dy++)
                {
                    int x = origin.GridX + dx;
                    int y = origin.GridY + dy;
                    if (x < 0 || x >= _gridSizeX || y < 0 || y >= _gridSizeY)
                        continue;

                    Node node = _grid[x, y];
                    if (!node.IsWalkableIn(dimension))
                        continue;

                    float dist = (node.WorldPosition - worldPosition).sqrMagnitude;
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        best = node;
                    }
                }
            }
            return best;
        }

        public IEnumerable<Node> GetNeighbors(Node node, Dimension dimension = Dimension.None)
        {
            // 노드 자신과 그 주변의 8개, 총 9개의 노드 중
            // 곧장 도달할 수 있는 노드를 검사.
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    // 제자리일 경우 continue
                    if (dx == 0 && dy == 0)
                        continue;
                    // 대각선이 허용되지 않았는데 대각선일 경우도
                    // continue
                    if (!allowDiagonal && dx != 0 && dy != 0)
                        continue;

                    // 해당 노드의 번호
                    int checkX = node.GridX + dx;
                    int checkY = node.GridY + dy;
                    
                    // 경계 out 보호
                    if (checkX >= 0 && checkX < _gridSizeX && checkY >= 0 && checkY < _gridSizeY)
                    {
                        // 대각선의 노드일 경우, 
                        // A D
                        // S B (S에서 D로 가는 상황을 검사)
                        // 같은 상황일 때 A와 B 둘 중 하나라도 벽이라면,
                        // S에서 D로 대각선으로 곧장 이동할 수 없기에
                        // 그런 노드는 continue.
                        if (allowDiagonal && dx != 0 && dy != 0)
                        {
                            if (!_grid[node.GridX + dx, node.GridY].IsWalkableIn(dimension) ||
                                !_grid[node.GridX, node.GridY + dy].IsWalkableIn(dimension))
                                continue;
                        }
                        yield return _grid[checkX, checkY];
                    }
                }
            }
        }

        // 적이 나를 향해 오는 경로의 노드를 담는 List
        public List<Node> DebugPath;

        // 유효한 목표 지점만 뽑도록 하는 방어 코드. (None이면 두 차원 모두에서 걸을 수 있는가)
        public bool IsWalkable(Vector3 worldPos, Dimension dimension = Dimension.None)
            => NodeFromWorldPoint(worldPos).IsWalkableIn(dimension);

        // ── 차원별 장애물 ────────────────────────────────────────────
        // 격자는 씬이 시작될 때 한 번 굽지만, 장벽처럼 라운드 중에 생기고 사라지는 장애물은
        // 굽지 않고 칸마다 "막고 있는 장애물의 수"를 차원별로 더하고 뺀다. (Node.RealBlocks / FakeBlocks)
        // 수로 세므로 장애물 두 개가 겹친 칸도, 하나가 사라지면 다른 하나 때문에 계속 막혀 있다.

        /// <summary>collider 안에 중심이 들어가는 칸들. (칸의 반경만큼 넉넉하게 잡는다)</summary>
        public List<Node> NodesInside(Collider collider)
        {
            var nodes = new List<Node>();
            if (_grid == null || collider == null)
                return nodes;

            Bounds bounds = collider.bounds;
            Node min = NodeFromWorldPoint(bounds.min);
            Node max = NodeFromWorldPoint(bounds.max);
            float reach = nodeRadius * nodeRadius;
            for (int x = min.GridX; x <= max.GridX; x++)
            {
                for (int y = min.GridY; y <= max.GridY; y++)
                {
                    Node node = _grid[x, y];
                    // 칸의 중심을 장애물의 높이로 올려서 잰다. (격자의 높이는 땅과 다를 수 있다)
                    Vector3 point = node.WorldPosition;
                    point.y = bounds.center.y;
                    if ((collider.ClosestPoint(point) - point).sqrMagnitude <= reach)
                        nodes.Add(node);
                }
            }
            return nodes;
        }

        /// <summary>조건에 맞는 자리의 칸들. (예: 닫힌 구역 전체)</summary>
        public List<Node> NodesWhere(System.Func<Vector3, bool> condition)
        {
            var nodes = new List<Node>();
            if (_grid == null)
                return nodes;

            foreach (Node node in _grid)
                if (condition(node.WorldPosition))
                    nodes.Add(node);
            return nodes;
        }

        /// <summary>
        /// 칸들을 그 차원에서 막는다(amount = 1) 또는 막은 것을 푼다(amount = -1).
        /// dimension이 None이면 두 차원 모두에 적용한다.
        /// </summary>
        public static void AddBlock(List<Node> nodes, Dimension dimension, int amount)
        {
            if (nodes == null)
                return;

            foreach (Node node in nodes)
            {
                if (dimension != Dimension.Fake)
                    node.RealBlocks = Mathf.Max(0, node.RealBlocks + amount);
                if (dimension != Dimension.Real)
                    node.FakeBlocks = Mathf.Max(0, node.FakeBlocks + amount);
            }
            Version++;
        }

        /// <summary>
        /// 막힌 칸이 바뀔 때마다 1씩 오른다. (장벽이 세워지거나 덤불 고리가 타서 열릴 때)
        /// 적은 경로를 찾을 때의 값을 기억해 두었다가, 값이 바뀌면 경로를 다시 찾는다.
        /// </summary>
        public static int Version { get; private set; }

        /// <summary>
        /// position이 그 차원에서 장애물에 막힌 칸이면, 가장 가까운 걸을 수 있는 칸의 자리를 돌려준다.
        /// 막히지 않았으면 position을 그대로 돌려준다. maxRing 칸 안에도 없으면 null.
        /// 장벽만 따진다: 원래 나무 기둥 칸(Walkable이 false)에 서 있는 것은 옮길 까닭이 없으므로 그대로 둔다.
        /// </summary>
        public Vector3? EscapeBlocked(Vector3 position, Dimension dimension, int maxRing)
        {
            if (_grid == null)
                return position;

            Node origin = NodeFromWorldPoint(position);
            bool blocked = dimension == Dimension.Fake ? origin.FakeBlocks > 0 : origin.RealBlocks > 0;
            if (!blocked)
                return position;

            Node best = ClosestWalkableNode(position, maxRing, dimension);
            return best != null ? best.WorldPosition : (Vector3?)null;
        }

        /// <summary>
        /// A*가 제대로 동작하는지를,
        /// Scene 화면에서 노드가 색칠되는 것을 통해
        /// 시각적으로 알 수 있게 하는 함수
        /// </summary>
        private void OnDrawGizmos()
        {
            // 월드의 경계를 흰선으로 나타냄
            Gizmos.DrawWireCube(transform.position,
                new Vector3(gridWorldSize.x, 0.1f, gridWorldSize.y));
            
            // grid가 아직 생성되지 않았다면
            // 즉 게임이 실행되지 않았다면
            // 아무것도 하지 않음
            if (_grid == null)
                return;

            if (drawGridGizmos)
            {
                foreach (Node n in _grid)
                {
                    /// Walkable 하면 흰색, 그렇지 않으면 빨간색
                    /// 4번째
                    // 차원별 장애물: Real에서만 막힘 = 주황, Fake에서만 막힘 = 보라, 둘 다 = 빨강
                    Gizmos.color = !n.Walkable ? new Color(1, 0, 0, 0.8f)
                                 : n.RealBlocks > 0 && n.FakeBlocks > 0 ? new Color(1, 0, 0, 0.6f)
                                 : n.RealBlocks > 0 ? new Color(1, 0.55f, 0, 0.6f)
                                 : n.FakeBlocks > 0 ? new Color(0.6f, 0.2f, 1, 0.6f)
                                 : new Color(1, 1, 1, 0.5f);
                    // 큐브의 한가운데 좌표와 큐브의 full size를 받아
                    // 바닥의 격자 무늬를 그림
                    Gizmos.DrawCube(n.WorldPosition,
                    new Vector3(_nodeDiameter * 0.9f, 0.05f, _nodeDiameter * 0.9f));
                }
            }

            if (drawPathGizmos && DebugPath != null)
            {
                Gizmos.color = Color.cyan;
                // 적이 오는 경로에 해당하는 노드들은
                // 격자무늬의 사각형 안에 더 작은 시안색의 사각형으로 표시
                foreach (Node n in DebugPath)
                    Gizmos.DrawCube(n.WorldPosition, new Vector3(
                        _nodeDiameter * 0.5f, 0.1f, _nodeDiameter * 0.5f));
            }
        }
    }
}
