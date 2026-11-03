using System.Collections.Generic;
using Astar3D;
using UnityEngine;
using UnityEngine.Rendering;

namespace Squad
{
    /// <summary>
    /// 디버그·시연용. F6을 누르면 길찾기 격자의 두 겹(Real 층, Fake 층)을 게임 화면에 그린다. 다시 누르면 끈다.
    ///   파랑    Real에서만 막힌 칸 (봉인된 관문, 모닥불의 덤불 덮개 등)
    ///   자홍    Fake에서만 막힌 칸 (오염 웅덩이 등)
    ///   회색    두 차원 모두 막힌 칸 (닫힌 구역, 구역 벽 등)
    /// 나무 기둥처럼 씬이 시작될 때 구운 장애물은 너무 많아 그리지 않는다.
    ///
    /// 그리고 플레이어에서 목적지까지, 같은 출발점과 도착점 사이의 경로를 차원마다 하나씩 그린다.
    ///   파란 선  Real 적이 갈 길 / 자홍 선  Fake 적이 갈 길
    /// 목적지는 처음에는 플레이어에게서 가장 먼 모닥불이고, 마우스 오른쪽 버튼으로 땅을 누르면 그 자리로 바뀐다.
    /// 길이를 화면에 함께 띄워, 같은 두 점인데 차원마다 길이 다르다는 것을 숫자로도 보여 준다.
    ///
    /// 막힌 칸은 장벽이 생기거나 사라질 때(PathGrid.Version이 바뀔 때)만 다시 모은다.
    ///
    /// 씬이나 프리팹에 붙일 필요가 없다. 게임이 시작될 때 스스로 만들어진다.
    /// </summary>
    public class PathGridDisplay : MonoBehaviour
    {
        private const KeyCode ToggleKey = KeyCode.F6;
        // 경로를 다시 찾는 주기(초)
        private const float RepathInterval = 0.25f;

        private static readonly Color RealOnly = new Color(0.25f, 0.55f, 1f, 0.55f);
        private static readonly Color FakeOnly = new Color(1f, 0.2f, 0.85f, 0.55f);
        private static readonly Color Both = new Color(0.35f, 0.35f, 0.35f, 0.45f);

        /// 지금 격자를 그리고 있는가. (시연 씬이 처음부터 켜 둘 때 쓴다)
        public static bool Showing { get; set; }

        private Pathfinder _pathfinder;
        private Mesh _mesh;
        private Material _material;
        private int _builtVersion = -1;
        private int _realCells, _fakeCells, _bothCells;

        private LineRenderer _realLine;
        private LineRenderer _fakeLine;
        private Vector3? _target;
        private float _repathTimer;
        private float _realLength, _fakeLength;
        private GUIStyle _style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            Showing = false;
            var displayObject = new GameObject("Path Grid Display");
            displayObject.AddComponent<PathGridDisplay>();
            DontDestroyOnLoad(displayObject);
        }

        private void Awake()
        {
            _material = new Material(Shader.Find("Sprites/Default"));
            _realLine = NewLine("Real Path", new Color(0.25f, 0.55f, 1f));
            _fakeLine = NewLine("Fake Path", new Color(1f, 0.2f, 0.85f));
        }

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey))
                Showing = !Showing;

            // 라운드가 바뀌면 격자와 목적지가 달라진다.
            if (_pathfinder == null)
            {
                _pathfinder = FindObjectOfType<Pathfinder>();
                _builtVersion = -1;
                _target = null;
            }

            Player player = DimensionController.Instance != null ? DimensionController.Instance.player : null;
            bool show = Showing && _pathfinder != null && _pathfinder.Grid != null && player != null;
            _realLine.enabled = show;
            _fakeLine.enabled = show;
            if (!show)
                return;

            if (_builtVersion != PathGrid.Version || _mesh == null)
                BuildMesh(_pathfinder.Grid);
            Graphics.DrawMesh(_mesh, Matrix4x4.identity, _material, 0, null, 0, null, ShadowCastingMode.Off, false);

            if (Input.GetMouseButtonDown(1))
                PickTarget();

            _repathTimer -= Time.unscaledDeltaTime;
            if (_repathTimer <= 0f)
            {
                _repathTimer = RepathInterval;
                _target ??= FarthestCampfire(player.transform.position);
                if (_target.HasValue)
                {
                    _realLength = DrawPath(_realLine, player.transform.position, _target.Value, Dimension.Real, 0.7f);
                    _fakeLength = DrawPath(_fakeLine, player.transform.position, _target.Value, Dimension.Fake, 1.1f);
                }
            }
        }

        private void OnGUI()
        {
            if (!Showing || _mesh == null)
                return;

            _style ??= new GUIStyle(GUI.skin.box) { fontSize = 15, richText = true, alignment = TextAnchor.UpperLeft };
            string Length(float length) => length > 0f ? $"{length:0}m" : "길 없음";
            GUI.Label(new Rect(12f, 120f, 520f, 98f),
                "<color=yellow>두 겹의 길찾기 격자 (F6로 끄기, 마우스 오른쪽: 목적지)</color>\n" +
                $"<color=#5090ff>■ Real에서만 막힘 {_realCells}칸</color>   <color=#ff40d8>■ Fake에서만 막힘 {_fakeCells}칸</color>   <color=#a0a0a0>■ 둘 다 {_bothCells}칸</color>\n" +
                $"<color=#5090ff>Real 경로 {Length(_realLength)}</color>   <color=#ff40d8>Fake 경로 {Length(_fakeLength)}</color>",
                _style);
        }

        // 차원마다 막힌 칸을 사각형 하나씩으로 모아 메시 하나로 그린다.
        private void BuildMesh(PathGrid grid)
        {
            _builtVersion = PathGrid.Version;
            _realCells = _fakeCells = _bothCells = 0;
            Terrain terrain = Terrain.activeTerrain;
            float half = grid.NodeDiameter * 0.45f;

            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            for (int x = 0; x < grid.SizeX; x++)
            {
                for (int y = 0; y < grid.SizeY; y++)
                {
                    Node node = grid.NodeAt(x, y);
                    if (!node.Walkable)
                        continue;

                    bool real = node.RealBlocks > 0;
                    bool fake = node.FakeBlocks > 0;
                    if (!real && !fake)
                        continue;

                    Color color = real && fake ? Both : real ? RealOnly : FakeOnly;
                    if (real && fake) _bothCells++;
                    else if (real) _realCells++;
                    else _fakeCells++;

                    Vector3 center = Grounded(node.WorldPosition, terrain, 0.3f);
                    int start = vertices.Count;
                    vertices.Add(center + new Vector3(-half, 0f, -half));
                    vertices.Add(center + new Vector3(-half, 0f, half));
                    vertices.Add(center + new Vector3(half, 0f, half));
                    vertices.Add(center + new Vector3(half, 0f, -half));
                    for (int i = 0; i < 4; i++)
                        colors.Add(color);
                    triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                }
            }

            if (_mesh == null)
                _mesh = new Mesh { name = "Blocked Cells", indexFormat = IndexFormat.UInt32 };
            _mesh.Clear();
            _mesh.SetVertices(vertices);
            _mesh.SetColors(colors);
            _mesh.SetTriangles(triangles, 0);
            _mesh.RecalculateBounds();
        }

        // 경로를 선으로 그리고 길이를 돌려준다. 길이 없으면 선을 비우고 0을 돌려준다.
        private float DrawPath(LineRenderer line, Vector3 from, Vector3 to, Dimension dimension, float lift)
        {
            List<Vector3> path = _pathfinder.FindPath(from, to, dimension);
            if (path == null || path.Count == 0)
            {
                line.positionCount = 0;
                return 0f;
            }

            Terrain terrain = Terrain.activeTerrain;
            line.positionCount = path.Count + 1;
            line.SetPosition(0, Grounded(from, terrain, lift));
            float length = 0f;
            Vector3 previous = from;
            for (int i = 0; i < path.Count; i++)
            {
                line.SetPosition(i + 1, Grounded(path[i], terrain, lift));
                length += Vector2.Distance(new Vector2(previous.x, previous.z), new Vector2(path[i].x, path[i].z));
                previous = path[i];
            }
            return length;
        }

        // 마우스 오른쪽 버튼으로 누른 땅을 목적지로 삼는다.
        private void PickTarget()
        {
            Camera cam = Camera.main;
            Terrain terrain = Terrain.activeTerrain;
            if (cam == null || terrain == null)
                return;

            TerrainCollider ground = terrain.GetComponent<TerrainCollider>();
            if (ground != null && ground.Raycast(cam.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, 5000f))
            {
                _target = hit.point;
                _repathTimer = 0f;
            }
        }

        private static Vector3? FarthestCampfire(Vector3 from)
        {
            Vector3? best = null;
            float bestDistance = -1f;
            foreach (Campfire campfire in Campfire.All)
            {
                if (campfire == null || !campfire.gameObject.activeInHierarchy)
                    continue;
                float distance = (campfire.transform.position - from).sqrMagnitude;
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    best = campfire.transform.position;
                }
            }
            return best;
        }

        private static Vector3 Grounded(Vector3 position, Terrain terrain, float lift)
        {
            if (terrain != null)
                position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
            return position + Vector3.up * lift;
        }

        private LineRenderer NewLine(string lineName, Color color)
        {
            var lineObject = new GameObject(lineName);
            lineObject.transform.SetParent(transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = _material;
            line.startColor = line.endColor = color;
            line.startWidth = line.endWidth = 0.6f;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.positionCount = 0;
            line.enabled = false;
            return line;
        }
    }
}
