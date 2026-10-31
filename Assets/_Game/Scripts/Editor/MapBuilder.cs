using System.Collections.Generic;
using System.Linq;
using Astar3D;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Squad
{
    /// <summary>
    /// 문자 그리드로 적은 맵 배치를 읽어 씬에 벽을 만들고,
    /// Stage 프리팹 안의 플레이어/적/포탈/출구를 표시된 칸으로 옮긴다.
    ///
    /// 사용법: 상단 메뉴 Tools > Build Map
    ///
    /// 벽을 Stage 프리팹이 아니라 씬(WorldSpace 아래)에 두는 이유:
    ///   PathGrid는 씬이 시작될 때(Awake) 한 번만 그리드를 굽는다.
    ///   Stage는 그보다 늦게 생성되므로, 프리팹 안의 벽은 그리드에 반영되지 않는다.
    ///
    /// 배치를 바꾸고 싶으면 Layout의 문자만 고치고 다시 실행하면 된다.
    /// 이전에 만든 벽은 지우고 새로 만든다.
    /// </summary>
    public static class MapBuilder
    {
        private const string StagePrefabPath = "Assets/_Game/Prefabs/Stage.prefab";
        private const string MapRootName = "GeneratedMap";

        /// <summary>
        /// 한 칸의 크기(m). 복도는 한 칸 폭이므로 곧 복도 폭이기도 하다.
        /// 맵 전체 크기는 Layout의 칸 수 × 이 값으로 정해진다.
        /// 맵을 키우거나 줄이려면 이 값이 아니라 Layout에 줄/칸을 더하거나 빼면 된다.
        /// </summary>
        private const float CellSize = 4.35f;
        /// 벽 높이(m). 쿼터뷰 카메라에서 벽 뒤가 너무 많이 가려지지 않을 정도.
        private const float WallHeight = 4f;

        /// <summary>
        /// 맵 배치. 위쪽이 북쪽(+Z, 카메라에서 먼 쪽)이다.
        ///   # 벽   . 바닥
        ///   S 플레이어 시작   P 포탈   E 출구
        ///   R Real 적 시작    F Fake 적 시작
        ///
        /// 바깥 테두리 벽은 맵 크기에 맞춰 자동으로 세워지므로 여기에는 적지 않는다.
        /// 가장자리까지 닿은 # 줄은 바깥 벽에 그대로 붙는다.
        ///
        /// 3x3 방 구조. 바깥쪽 8개 방이 한 바퀴 순환로를 이루고,
        /// 가운데 홀(포탈)은 서/동/남쪽 방과 연결된다.
        /// 출구가 있는 북쪽 방은 가운데 홀과 바로 이어지지 않아서
        /// 북서/북동 방을 돌아서 들어가야 한다.
        /// </summary>
        private static readonly string[] Layout =
        {
            ".......#...E...#.......",
            ".......#.......#.......",
            ".......#.#...#.#.......",
            "...................R...",
            ".......#.#...#.#.......",
            ".......#.......#.......",
            ".......#.......#.......",
            "###.###############.###",
            ".......#.......#.......",
            ".......#.#...#.#.......",
            "..##...#.......#...##..",
            "...........P...........",
            "..##...#.......#...##..",
            ".......#.#...#.#.......",
            ".......#.......#.......",
            "###.#######.#######.###",
            ".......#.......#.......",
            ".......#.......#.......",
            ".......#.......#.......",
            "...................F...",
            ".......#...S...#.......",
            ".......#.......#.......",
            ".......#.......#.......",
        };

        private static int Rows => Layout.Length;
        private static int Cols => Layout[0].Length;
        private static float MapWidth => Cols * CellSize;
        private static float MapDepth => Rows * CellSize;

        [MenuItem("Tools/Build Map")]
        public static void BuildMap()
        {
            if (Layout.Any(row => row.Length != Cols))
            {
                Debug.LogError("[MapBuilder] Layout의 모든 줄은 길이가 같아야 합니다.");
                return;
            }

            int wallLayer = LayerMask.NameToLayer("Unwalkable");
            if (wallLayer < 0)
            {
                Debug.LogError("[MapBuilder] 'Unwalkable' 레이어가 없습니다.");
                return;
            }

            float floorTop = FindFloorTop();
            ResizeFloor();
            BuildWalls(wallLayer, floorTop);
            ConfigurePathGrid();
            PlaceStageObjects();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[MapBuilder] 맵 생성 완료. 씬을 저장하세요 (Ctrl+S).");
        }

        // ── 좌표 ────────────────────────────────────────────────────

        /// <summary>r행 c열 칸의 중심 좌표(XZ). 맵 전체의 중심이 원점이 되도록 배치한다.</summary>
        private static Vector3 CellCenter(int r, int c)
        {
            float x = (c - Cols / 2f + 0.5f) * CellSize;
            float z = (Rows / 2f - r - 0.5f) * CellSize;
            return new Vector3(x, 0f, z);
        }

        private static bool IsWall(int r, int c) => Layout[r][c] == '#';

        private static List<Vector3> FindCells(char symbol)
        {
            var cells = new List<Vector3>();
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                    if (Layout[r][c] == symbol)
                        cells.Add(CellCenter(r, c));
            return cells;
        }

        /// 벽을 바닥 위에 세우기 위해 바닥 윗면의 높이를 찾는다.
        private static float FindFloorTop()
        {
            GameObject floor = GameObject.Find("Floor");
            if (floor != null && floor.TryGetComponent(out Renderer renderer))
                return renderer.bounds.max.y;

            Debug.LogWarning("[MapBuilder] 'Floor'를 찾지 못해 바닥 높이를 0.5로 가정합니다.");
            return 0.5f;
        }

        /// 바닥을 맵 크기에 맞춘다. 높이(두께)는 그대로 둔다.
        private static void ResizeFloor()
        {
            GameObject floor = GameObject.Find("Floor");
            if (floor == null)
                return;

            Transform t = floor.transform;
            Undo.RecordObject(t, "Resize Floor");
            t.position = new Vector3(0f, t.position.y, 0f);
            t.localScale = new Vector3(MapWidth, t.localScale.y, MapDepth);
        }

        // ── 벽 ──────────────────────────────────────────────────────

        private static void BuildWalls(int wallLayer, float floorTop)
        {
            GameObject worldSpace = GameObject.Find("WorldSpace");
            Transform parent = worldSpace != null ? worldSpace.transform : null;

            // 이전에 생성한 맵 제거
            Transform oldMap = parent != null ? parent.Find(MapRootName) : null;
            if (oldMap == null && GameObject.Find(MapRootName) is GameObject found)
                oldMap = found.transform;
            if (oldMap != null)
                Undo.DestroyObjectImmediate(oldMap.gameObject);

            // 기존에 손으로 세워 둔 벽(Cube, Wall, Wall (1) ...)은 맵 크기가 바뀌면
            // 어긋나므로 끈다. 바깥 벽은 아래에서 맵 크기에 맞춰 새로 세운다. (지우지는 않음)
            if (parent != null)
            {
                foreach (Transform child in parent)
                {
                    bool isOldWall = child.name == "Cube" || child.name.StartsWith("Wall");
                    if (!isOldWall || !child.gameObject.activeSelf)
                        continue;

                    Undo.RecordObject(child.gameObject, "Disable old wall");
                    child.gameObject.SetActive(false);
                    Debug.Log($"[MapBuilder] 기존 벽 WorldSpace/{child.name}을(를) 비활성화했습니다.");
                }
            }

            var root = new GameObject(MapRootName) { layer = wallLayer };
            Undo.RegisterCreatedObjectUndo(root, "Build Map");
            if (parent != null)
                root.transform.SetParent(parent, false);

            // 벽 오브젝트 수를 줄이기 위해 이어진 칸을 하나의 큐브로 합친다.
            // 1) 가로로 2칸 이상 이어진 벽을 먼저 합치고
            // 2) 남은 칸은 세로로 이어서 합친다.
            var used = new bool[Rows, Cols];

            BuildOuterWalls(root.transform, wallLayer, floorTop);

            for (int r = 0; r < Rows; r++)
            {
                int c = 0;
                while (c < Cols)
                {
                    if (!IsWall(r, c)) { c++; continue; }

                    int start = c;
                    while (c < Cols && IsWall(r, c))
                        c++;

                    if (c - start >= 2)
                    {
                        CreateWall(root.transform, wallLayer, floorTop, r, start, 1, c - start);
                        for (int i = start; i < c; i++)
                            used[r, i] = true;
                    }
                }
            }

            for (int c = 0; c < Cols; c++)
            {
                int r = 0;
                while (r < Rows)
                {
                    if (!IsWall(r, c) || used[r, c]) { r++; continue; }

                    int start = r;
                    while (r < Rows && IsWall(r, c) && !used[r, c])
                        r++;

                    CreateWall(root.transform, wallLayer, floorTop, start, c, r - start, 1);
                }
            }
        }

        /// <summary>
        /// 맵 바깥을 한 칸 두께의 벽으로 두른다.
        /// 남북 벽을 모서리까지 길게 늘여서 네 모서리에 틈이 생기지 않게 한다.
        /// </summary>
        private static void BuildOuterWalls(Transform parent, int layer, float floorTop)
        {
            float y = floorTop + WallHeight * 0.5f;
            float t = CellSize;
            float halfW = MapWidth * 0.5f;
            float halfD = MapDepth * 0.5f;

            CreateBox(parent, layer, "OuterWall N", new Vector3(0f, y, halfD + t * 0.5f), new Vector3(MapWidth + t * 2f, WallHeight, t));
            CreateBox(parent, layer, "OuterWall S", new Vector3(0f, y, -halfD - t * 0.5f), new Vector3(MapWidth + t * 2f, WallHeight, t));
            CreateBox(parent, layer, "OuterWall E", new Vector3(halfW + t * 0.5f, y, 0f), new Vector3(t, WallHeight, MapDepth));
            CreateBox(parent, layer, "OuterWall W", new Vector3(-halfW - t * 0.5f, y, 0f), new Vector3(t, WallHeight, MapDepth));
        }

        /// <summary>row, col 칸부터 rowCount x colCount 칸을 덮는 벽 하나를 만든다.</summary>
        private static void CreateWall(Transform parent, int layer, float floorTop,
                                       int row, int col, int rowCount, int colCount)
        {
            Vector3 first = CellCenter(row, col);
            Vector3 center = new Vector3(
                first.x + (colCount - 1) * CellSize * 0.5f,
                floorTop + WallHeight * 0.5f,
                first.z - (rowCount - 1) * CellSize * 0.5f);

            CreateBox(parent, layer, "Wall", center, new Vector3(colCount * CellSize, WallHeight, rowCount * CellSize));
        }

        private static void CreateBox(Transform parent, int layer, string name, Vector3 center, Vector3 size)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.layer = layer;
            box.transform.SetParent(parent, false);
            box.transform.position = center;
            box.transform.localScale = size;
        }

        // ── 길찾기 그리드 ───────────────────────────────────────────

        /// <summary>
        /// PathGrid가 맵 전체를 덮도록 크기를 맞춘다.
        /// 노드 수는 면적에 비례하므로, 맵을 크게 키우면 길찾기 비용도 함께 커진다.
        /// </summary>
        private static void ConfigurePathGrid()
        {
            PathGrid grid = Object.FindObjectOfType<PathGrid>();
            if (grid == null)
            {
                Debug.LogWarning("[MapBuilder] 씬에서 PathGrid를 찾지 못했습니다.");
                return;
            }

            var so = new SerializedObject(grid);
            so.FindProperty("gridWorldSize").vector2Value = new Vector2(MapWidth, MapDepth);
            so.ApplyModifiedProperties();

            if (grid.transform.position != Vector3.zero)
                Debug.LogWarning("[MapBuilder] PathGrid 오브젝트가 원점에 있지 않습니다. 맵과 그리드가 어긋날 수 있습니다.");
        }

        // ── Stage 프리팹 안의 오브젝트 배치 ─────────────────────────

        private static void PlaceStageObjects()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(StagePrefabPath);
            try
            {
                PlacePlayer(root);
                PlaceEnemies(root);
                PlaceSingle(root.GetComponentInChildren<Exit>(true), 'E', "출구");

                PrefabUtility.SaveAsPrefabAsset(root, StagePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void PlacePlayer(GameObject root)
        {
            Player player = root.GetComponentInChildren<Player>(true);
            List<Vector3> cells = FindCells('S');
            if (player == null || cells.Count == 0)
            {
                Debug.LogWarning("[MapBuilder] 플레이어 또는 시작 위치(S)가 없습니다.");
                return;
            }

            MoveKeepingHeight(player.transform, cells[0]);
        }

        /// <summary>
        /// 적을 R/F 칸으로 옮기고, 루트 스케일을 1로 되돌린다.
        /// 스케일이 크면 콜라이더도 커져서 catchRadius 안으로 들어오지 못하고,
        /// 좁은 복도에 끼이기 때문이다. 발바닥 높이는 유지한다.
        /// </summary>
        private static void PlaceEnemies(GameObject root)
        {
            List<Vector3> realCells = FindCells('R');
            List<Vector3> fakeCells = FindCells('F');
            int realIndex = 0, fakeIndex = 0;

            foreach (HorrorChaserAgent agent in root.GetComponentsInChildren<HorrorChaserAgent>(true))
            {
                Transform t = agent.transform;
                ResetEnemyScale(t);

                DimensionMember member = agent.GetComponent<DimensionMember>();
                bool isFake = member != null && member.Dimension == Dimension.Fake;
                List<Vector3> cells = isFake ? fakeCells : realCells;
                int index = isFake ? fakeIndex++ : realIndex++;

                if (index < cells.Count)
                    MoveKeepingHeight(t, cells[index]);
                else
                    Debug.LogWarning($"[MapBuilder] {agent.name}을(를) 놓을 {(isFake ? 'F' : 'R')} 칸이 부족합니다.");
            }
        }

        private static void ResetEnemyScale(Transform t)
        {
            Vector3 scale = t.localScale;
            if (scale == Vector3.one)
                return;

            // 캡슐 콜라이더 기준으로 발바닥 높이를 구해, 스케일을 바꾼 뒤에도 바닥에 서 있게 한다.
            float halfHeight = t.TryGetComponent(out CapsuleCollider capsule) ? capsule.height * 0.5f : 1f;
            float bottom = t.position.y - halfHeight * scale.y;

            t.localScale = Vector3.one;
            t.position = new Vector3(t.position.x, bottom + halfHeight, t.position.z);
        }

        private static void PlaceSingle(Component target, char symbol, string label)
        {
            List<Vector3> cells = FindCells(symbol);
            if (target == null || cells.Count == 0)
            {
                Debug.LogWarning($"[MapBuilder] {label} 또는 위치({symbol})가 없습니다.");
                return;
            }

            MoveByBounds(target.transform, cells[0]);
        }

        /// 높이는 그대로 두고 XZ만 옮긴다.
        private static void MoveKeepingHeight(Transform t, Vector3 cell)
        {
            t.position = new Vector3(cell.x, t.position.y, cell.z);
        }

        /// <summary>
        /// 피벗이 모델 중심에 있지 않은 오브젝트(출구 모델 등)를 칸 중심으로 옮긴다. XZ만 옮긴다.
        ///
        /// 상호작용 범위(트리거 BoxCollider)가 있으면 그 중심을 칸 중심에 맞춘다.
        /// 플레이어가 실제로 닿아야 하는 곳이 트리거이기 때문이다.
        /// (Renderer bounds는 프리팹을 에디터에서 열었을 때 정확하지 않을 수 있어,
        ///  트리거 중심은 Transform으로 직접 계산한다)
        /// 트리거가 없으면 눈에 보이는 모습(Renderer bounds)의 중심을 맞춘다.
        /// </summary>
        private static void MoveByBounds(Transform t, Vector3 cell)
        {
            BoxCollider trigger = t.GetComponents<BoxCollider>().FirstOrDefault(b => b.isTrigger);
            if (trigger != null)
            {
                Vector3 triggerCenter = t.TransformPoint(trigger.center);
                Vector3 shift = cell - triggerCenter;
                shift.y = 0f;
                t.position += shift;
                return;
            }

            Renderer[] renderers = t.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                MoveKeepingHeight(t, cell);
                return;
            }

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers)
                bounds.Encapsulate(r.bounds);

            Vector3 offset = cell - bounds.center;
            offset.y = 0f;
            t.position += offset;
        }
    }
}
