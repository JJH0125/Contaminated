using System.Collections.Generic;
using Squad;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 숲 맵을 만드는 에디터 도구. 지형(Terrain), 경계 벽, 나무와 풀을 한 창에서 만든다.
/// 메뉴: Tools > Forest Map Tool
///
/// ■ 지형
/// 지형의 높이는 격자 모양으로 나열한 숫자(높이맵)로 저장된다.
/// 이 도구는 칸마다 "가운데에서 얼마나 떨어졌는가"로 높이를 계산해 그 숫자를 채운다.
///   봉우리   가운데가 가장 높고, 반경 끝에서 평지와 부드럽게 이어진다
///   굴곡     평지가 밋밋하지 않도록 전체에 얕은 굴곡을 더한다 (0이면 완전히 평평)
/// 지형의 한가운데가 월드의 원점(0, 0, 0)에 오도록 놓는다.
/// Stage가 원점에 생성되고 PathGrid도 자기 위치를 중심으로 격자를 깔기 때문이다.
///
/// 만든 뒤에는 Terrain의 Inspector에서 붓으로 자유롭게 고칠 수 있다.
/// 다만 지형을 다시 만들면 높이를 처음부터 다시 계산하므로, 붓으로 고친 높이는 사라진다.
/// (칠해 둔 바닥 텍스처는 그대로 남는다)
///
/// ■ 경계 벽
/// 지형의 네 가장자리에 보이지 않는 벽을 둘러서, 플레이어가 지형 밖으로 떨어지지 않게 한다.
///
/// ■ 숲
/// 기존 숲 에셋(ForestRoad)의 나무와 풀을 복제해서 지형 위에 흩뿌린다.
/// 원본에서 무엇이 나무이고 무엇이 풀인지는 Plant Collider Tool이 정리해 둔 결과로 구분한다.
///   나무      기둥 캡슐(CapsuleCollider)이 켜져 있다
///   풀·덤불   켜진 Collider가 없다
///   바닥·벽   MeshCollider가 켜져 있다 → 복제하지 않는다
/// 복제본에는 원본의 캡슐과 장애물 레이어가 그대로 따라오므로,
/// 새 지형에서도 추격자가 나무를 피해 다니고 나무 뒤에서는 시야가 가려진다.
///
/// 심는 자리는 "뽑아 보고, 조건에 안 맞으면 다시 뽑는" 방식으로 정한다.
///   - 땅이 너무 가파르지 않다
///   - 출구와 플레이어 시작 자리에서 떨어져 있다 (Stage 프리팹의 StageLayout에서 자리를 읽어 온다)
///   - (나무만) 다른 나무와 너무 가깝지 않다
///
/// 나무는 Stage 프리팹이 아니라 씬에 심는다.
/// PathGrid가 씬이 시작될 때 한 번만 격자를 굽기 때문에, 그보다 늦게 생성되는 Stage 안의 나무는 격자에 반영되지 않는다.
///
/// 같은 시드로는 항상 같은 숲이 나온다. 그래서 지형의 높이만 바꿔 다시 만들어도
/// 나무는 같은 자리에, 새 높이에 맞춰 다시 심어진다.
///
/// ■ 경계 숲
/// 경계 벽 바깥에 보기만 하는 땅(Outer Ground)과 숲(Border Forest)을 둘러서, 맵의 끝이 보이지 않고
/// 숲이 계속 이어지는 것처럼 보이게 한다. 바깥 땅은 바깥으로 갈수록 서서히 높아진다.
/// 나무와 풀은 안쪽과 같은 밀도로 심고, Collider는 모두 뺀다. 몸은 경계 벽이 막는다.
/// 지형을 다시 만들었으면 경계 숲도 다시 만들어야 가장자리 높이가 맞는다.
/// </summary>
public class ForestMapTool : EditorWindow
{
    private const string Folder = "Assets/_Game/Environment/Terrain";
    private const string DataPath = Folder + "/ForestTerrainData.asset";
    private const string LayerPath = Folder + "/ForestGround.terrainlayer";
    private const string TerrainName = "Forest Terrain";
    private const string WallsName = "Boundary Walls";
    private const string PropsName = "Forest Props";
    private const string OuterGroundName = "Outer Ground";
    private const string BorderForestName = "Border Forest";
    /// 만든 것들을 묶어 두는 오브젝트의 이름. 씬에 있으면 처음 만들 때 그 아래에 둔다.
    /// (지형과 안쪽 숲은 InnerMap, 바깥 땅과 경계 숲은 OuterMap)
    private const string InnerGroupName = "InnerMap";
    private const string OuterGroupName = "OuterMap";
    private const string OuterMeshPath = Folder + "/OuterGround.asset";
    private const string OuterMaterialPath = Folder + "/OuterGround.mat";

    /// 바깥 땅 메시의 칸 크기(m). 지형의 가로세로 길이와 바깥 땅의 너비를 이 값으로 나눠떨어지게 맞춘다.
    private const float OuterCellSize = 2f;
    /// 바깥 땅 중 지형 아래에 깔리는 부분을 지형보다 얼마나 낮출지(m). 지형과 겹쳐 깜빡이지 않게 한다.
    private const float OuterTuckDepth = 0.3f;

    /// 높이맵의 가로세로 칸 수. Unity 규칙상 (2의 거듭제곱 + 1)이어야 한다.
    /// 513이면 200m 지형에서 한 칸이 약 0.4m다.
    private const int Resolution = 513;

    /// 경계 벽의 두께(m)와, 지형의 가장 높은 곳보다 얼마나 더 높이 세울지(m).
    /// 플레이어가 봉우리 꼭대기에서 뛰어도 넘지 못할 만큼 넉넉히 잡는다.
    private const float WallThickness = 2f;
    private const float WallExtraHeight = 30f;

    /// 나무나 풀 하나를 심을 때, 조건에 맞는 자리가 나올 때까지 다시 뽑는 최대 횟수
    private const int MaxTries = 50;

    // 창에 넣은 값은 스크립트를 다시 컴파일해도 남도록 저장해 둔다.

    // 지형
    [SerializeField] private float _size = 200f;
    [SerializeField] private float _hillHeight = 12f;
    [SerializeField] private float _hillRadius = 40f;
    [SerializeField] private float _bumpHeight = 1f;
    [SerializeField] private float _bumpSize = 20f;
    [SerializeField] private Texture2D _groundTexture;
    [SerializeField] private Texture2D _groundNormal;
    [SerializeField] private float _textureTile = 20f;

    // 숲
    [SerializeField] private GameObject _source;
    [SerializeField] private GameObject _stagePrefab;
    [SerializeField] private int _treeCount = 126;
    [SerializeField] private int _plantCount = 294;
    [SerializeField] private float _treeSpacing = 6f;
    [SerializeField] private float _sizeScale = 1f;
    [SerializeField] private float _sizeVariation = 0.2f;
    [SerializeField] private float _maxSlope = 35f;
    [SerializeField] private float _exitClearRadius = 25f;
    [SerializeField] private float _playerClearRadius = 8f;
    [SerializeField] private int _seed = 1;

    // 경계 숲
    [SerializeField] private float _borderWidth = 60f;
    [SerializeField] private float _borderRise = 15f;

    private Vector2 _scroll;
    private string _message = "";

    [MenuItem("Tools/Forest Map Tool")]
    private static void Open() => GetWindow<ForestMapTool>("Forest Map");

    private void OnEnable()
    {
        // 원본 숲은 꺼 둔 상태일 수 있으므로, 꺼진 오브젝트까지 포함해서 찾는다.
        if (_source == null)
            foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == "ForestRoad")
                    _source = root;

        // GameManager에 연결된 Stage 프리팹을 그대로 가져온다.
        if (_stagePrefab == null)
        {
            GameManager manager = FindObjectOfType<GameManager>();
            if (manager != null)
                _stagePrefab = new SerializedObject(manager).FindProperty("stage").objectReferenceValue as GameObject;
        }
    }

    // ── 창 ───────────────────────────────────────────────────────

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawTerrainSettings();
        EditorGUILayout.Space();
        DrawForestSettings();
        EditorGUILayout.Space();
        DrawBorderSettings();
        EditorGUILayout.Space();
        DrawButtons();

        if (_message != "")
            EditorGUILayout.HelpBox(_message, MessageType.Info);

        EditorGUILayout.EndScrollView();
    }

    private void DrawTerrainSettings()
    {
        EditorGUILayout.LabelField("■ 지형", EditorStyles.boldLabel);
        _size = EditorGUILayout.FloatField(
            new GUIContent("가로세로 길이 (m)", "지형은 정사각형이다"), _size);

        _hillHeight = EditorGUILayout.Slider(
            new GUIContent("봉우리 높이 (m)", "봉우리 꼭대기의 높이. 0이면 봉우리가 없다"), _hillHeight, 0f, 200f);
        _hillRadius = EditorGUILayout.Slider(
            new GUIContent("봉우리 반경 (m)", "가운데에서 이 거리까지가 봉우리다. 클수록 완만해진다"),
            _hillRadius, 10f, _size * 0.5f);

        // 높이와 반경을 바꾸면 경사가 어떻게 되는지 바로 보여준다.
        float steepest = SteepestHillAngle();
        EditorGUILayout.HelpBox(
            $"봉우리에서 가장 가파른 곳의 기울기: {steepest:0.0}도\n" +
            "플레이어(CharacterController)는 Slope Limit(지금 45도)보다 가파른 곳을 오르지 못한다.",
            steepest > 45f ? MessageType.Warning : MessageType.None);

        _bumpHeight = EditorGUILayout.Slider(
            new GUIContent("굴곡 높이 (m)", "평지의 얕은 굴곡. 0이면 완전히 평평하다"), _bumpHeight, 0f, 10f);
        _bumpSize = EditorGUILayout.Slider(
            new GUIContent("굴곡 하나의 너비 (m)", "클수록 넓고 완만한 굴곡이 된다"), _bumpSize, 5f, 200f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("바닥 텍스처 (선택)", EditorStyles.boldLabel);
        _groundTexture = (Texture2D)EditorGUILayout.ObjectField(
            new GUIContent("색상", "반복해서 깔아도 이음매가 안 보이는 텍스처여야 한다"),
            _groundTexture, typeof(Texture2D), false);
        _groundNormal = (Texture2D)EditorGUILayout.ObjectField("노멀맵", _groundNormal, typeof(Texture2D), false);
        _textureTile = EditorGUILayout.FloatField(
            new GUIContent("한 장의 너비 (m)", "텍스처 한 장이 덮는 땅의 너비"), _textureTile);
    }

    private void DrawForestSettings()
    {
        EditorGUILayout.LabelField("■ 숲", EditorStyles.boldLabel);
        _source = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("원본 숲 (ForestRoad)", "나무와 풀을 복제해 올 오브젝트. 꺼져 있어도 된다"),
            _source, typeof(GameObject), true);
        _stagePrefab = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Stage 프리팹", "출구와 플레이어 자리를 읽어 와서 그 주변을 비워 둔다"),
            _stagePrefab, typeof(GameObject), false);

        _treeCount = EditorGUILayout.IntField("나무 개수", _treeCount);
        _plantCount = EditorGUILayout.IntField("풀·덤불 개수", _plantCount);
        _treeSpacing = EditorGUILayout.FloatField(
            new GUIContent("나무 사이 최소 거리 (m)", "기둥과 기둥 사이. 좁히면 빽빽해져서 추격자가 지나갈 틈이 줄어든다"),
            _treeSpacing);

        _sizeScale = EditorGUILayout.Slider(
            new GUIContent("크기 배율", "1이면 원본 숲에 놓여 있던 크기 그대로"), _sizeScale, 0.1f, 2f);
        _sizeVariation = EditorGUILayout.Slider(
            new GUIContent("크기 변화", "0.2면 하나하나를 0.8~1.2배 사이에서 무작위로 바꾼다"), _sizeVariation, 0f, 0.5f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("심지 않을 곳", EditorStyles.boldLabel);
        _maxSlope = EditorGUILayout.Slider(
            new GUIContent("최대 기울기 (도)", "이보다 가파른 땅에는 심지 않는다"), _maxSlope, 5f, 80f);
        _exitClearRadius = EditorGUILayout.FloatField(
            new GUIContent("출구 주변 (m)", "출구에서 이 거리 안에는 심지 않는다"), _exitClearRadius);
        _playerClearRadius = EditorGUILayout.FloatField(
            new GUIContent("플레이어 시작점 주변 (m)", "이 거리 안에는 심지 않는다"), _playerClearRadius);

        EditorGUILayout.BeginHorizontal();
        _seed = EditorGUILayout.IntField(
            new GUIContent("시드", "같은 숫자면 항상 같은 숲이 나온다"), _seed);
        if (GUILayout.Button("다른 숫자", GUILayout.Width(70f)))
            _seed = Random.Range(1, 100000);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawBorderSettings()
    {
        EditorGUILayout.LabelField("■ 경계 숲 (보기만 하는 바깥 숲)", EditorStyles.boldLabel);
        _borderWidth = EditorGUILayout.Slider(
            new GUIContent("바깥 땅의 너비 (m)", "경계 벽 바깥으로 이만큼 땅과 숲을 더 둘러서, 숲이 이어지는 것처럼 보이게 한다"),
            _borderWidth, 10f, 150f);
        _borderRise = EditorGUILayout.Slider(
            new GUIContent("바깥으로 갈수록 높아지는 정도 (m)", "바깥 땅의 끝이 경계보다 이만큼 높다. 높을수록 땅 끝이 덜 보인다"),
            _borderRise, 0f, 50f);
        EditorGUILayout.HelpBox(
            "나무와 풀은 안쪽 숲과 같은 밀도로 심는다(나무 개수, 풀·덤불 개수를 넓이에 비례해 늘림).\n" +
            "보기만 하는 숲이라 Collider를 모두 뺀다. 몸은 경계 벽이 막는다.", MessageType.None);
    }

    private void DrawButtons()
    {
        // 지형의 높이가 바뀌면 나무도 새 높이에 맞춰 다시 심어야 하므로, 보통은 이 버튼 하나면 된다.
        if (GUILayout.Button("전부 만들기 (지형 → 경계 벽 → 숲)", GUILayout.Height(30f)))
        {
            if (BuildTerrain())
                Scatter();
        }

        EditorGUILayout.LabelField("따로 하기", EditorStyles.boldLabel);

        // 지형과 높이는 그대로 두고 나무 배치만 바꾸고 싶을 때
        if (GUILayout.Button("숲만 다시 심기"))
            Scatter();

        // 경계 바깥에 보기만 하는 땅과 숲을 두른다. 지형을 다시 만들었으면 이것도 다시 눌러야 가장자리가 맞는다.
        if (GUILayout.Button("경계 숲 만들기 (바깥 땅 + 나무·풀)"))
            BuildBorder();

        // 붓으로 고친 높이를 지우지 않고 벽만 고치고 싶을 때
        if (GUILayout.Button("경계 벽만 다시 두르기"))
        {
            Terrain terrain = FindTerrain();
            if (terrain == null)
                _message = "씬에 이 도구로 만든 지형이 없습니다.";
            else
            {
                BuildWalls(terrain);
                EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
                _message = "경계 벽을 다시 둘렀습니다. 씬을 저장(Ctrl+S)해야 유지된다.";
            }
        }
    }

    // ── 지형 ─────────────────────────────────────────────────────

    /// <summary>
    /// 가운데에서 distance만큼 떨어진 곳의 봉우리 높이.
    /// 코사인 곡선을 쓰면 꼭대기와 가장자리 양쪽에서 기울기가 0이 되어,
    /// 꼭대기는 둥글고 평지와는 꺾이는 곳 없이 이어진다.
    /// </summary>
    private float HillHeightAt(float distance)
    {
        if (distance >= _hillRadius)
            return 0f;

        return _hillHeight * 0.5f * (1f + Mathf.Cos(Mathf.PI * distance / _hillRadius));
    }

    // 코사인 곡선은 반경의 절반 지점에서 가장 가파르고, 그 기울기는 (높이 × π) ÷ (반경 × 2)다.
    private float SteepestHillAngle()
        => Mathf.Atan(_hillHeight * Mathf.PI / (_hillRadius * 2f)) * Mathf.Rad2Deg;

    /// <summary>
    /// 새로 만들 오브젝트를 둘 부모. 전에 만든 것이 있으면 그것과 같은 부모에 두어서, 씬에서 묶어 둔 구조가 유지되게 한다.
    /// 처음 만드는 것이면 묶음 오브젝트(groupName)가 씬에 있을 때 그 아래에, 없으면 맨 바깥에 둔다.
    /// 묶음 오브젝트는 원점에 두고 회전과 크기를 바꾸지 않아야 지형과 숲의 자리가 어긋나지 않는다.
    /// </summary>
    private static Transform ParentFor(GameObject previous, string groupName)
    {
        if (previous != null)
            return previous.transform.parent;

        GameObject group = GameObject.Find(groupName);
        return group != null ? group.transform : null;
    }

    // path의 폴더가 없으면 한 단계씩 만든다. (예: Assets/_Game/Environment/Terrain)
    private static void EnsureFolder(string path)
    {
        string current = "Assets";
        foreach (string part in path.Split('/'))
        {
            if (part == "Assets")
                continue;
            string next = current + "/" + part;
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, part);
            current = next;
        }
    }

    // 이 도구로 만든 지형을 씬에서 찾는다. 없으면 null.
    private static Terrain FindTerrain()
    {
        GameObject terrainObject = GameObject.Find(TerrainName);
        return terrainObject != null ? terrainObject.GetComponent<Terrain>() : null;
    }

    // 지형과 경계 벽을 만든다. 다시 만들지 묻는 창에서 취소하면 아무것도 하지 않고 false를 돌려준다.
    private bool BuildTerrain()
    {
        EnsureFolder(Folder);

        TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(DataPath);
        if (data == null)
        {
            data = new TerrainData();
            AssetDatabase.CreateAsset(data, DataPath);
        }
        else if (!EditorUtility.DisplayDialog(
            "지형 다시 만들기",
            "높이를 처음부터 다시 계산합니다.\n붓으로 고친 높이는 사라집니다. 계속할까요?",
            "다시 만들기", "취소"))
        {
            return false;
        }
        else
            Undo.RegisterCompleteObjectUndo(data, "지형 다시 만들기");

        // 높이맵에는 0~1 사이의 비율만 들어가고, 실제 높이는 (비율 × 지형의 최대 높이)다.
        // 그래서 가장 높은 곳이 1을 넘지 않도록 최대 높이를 넉넉히 잡는다.
        float maxHeight = _hillHeight + _bumpHeight + 1f;

        // 칸 수를 바꾸면 크기가 초기화되므로 칸 수를 먼저 정한다.
        data.heightmapResolution = Resolution;
        data.size = new Vector3(_size, maxHeight, _size);

        var heights = new float[Resolution, Resolution];
        for (int z = 0; z < Resolution; z++)
        {
            for (int x = 0; x < Resolution; x++)
            {
                // 이 칸이 지형의 한가운데에서 얼마나 떨어져 있는가 (m)
                float worldX = (x / (Resolution - 1f) - 0.5f) * _size;
                float worldZ = (z / (Resolution - 1f) - 0.5f) * _size;
                float distance = Mathf.Sqrt(worldX * worldX + worldZ * worldZ);

                float height = HillHeightAt(distance);
                // PerlinNoise는 가까운 곳끼리 비슷한 값이 나오는 무작위 값(0~1)이라 자연스러운 굴곡이 된다.
                // 음수 좌표에서는 무늬가 대칭으로 나오므로 좌표를 양수 쪽으로 밀어 준다.
                height += Mathf.PerlinNoise(
                    (worldX + _size) / _bumpSize, (worldZ + _size) / _bumpSize) * _bumpHeight;

                // 높이맵은 [z, x] 순서다.
                heights[z, x] = height / maxHeight;
            }
        }
        data.SetHeights(0, 0, heights);

        // 이미 바닥 텍스처를 칠해 두었다면 건드리지 않는다.
        if (_groundTexture != null && data.terrainLayers.Length == 0)
            data.terrainLayers = new[] { GetGroundLayer() };

        GameObject terrainObject = GameObject.Find(TerrainName);
        if (terrainObject == null)
        {
            terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.name = TerrainName;
            terrainObject.transform.SetParent(ParentFor(null, InnerGroupName), false);
            Undo.RegisterCreatedObjectUndo(terrainObject, "지형 만들기");
        }
        // Terrain은 한쪽 모서리가 기준점이라, 절반만큼 당겨야 한가운데가 원점에 온다.
        terrainObject.transform.position = new Vector3(-_size * 0.5f, 0f, -_size * 0.5f);
        // 바닥은 Floor 레이어에 둔다. 적의 시야(VisionSensor)가 이 레이어를 통과하지 못하므로 언덕이 시야를 가린다.
        int floorLayer = LayerMask.NameToLayer("Floor");
        if (floorLayer >= 0)
            terrainObject.layer = floorLayer;

        BuildWalls(terrainObject.GetComponent<Terrain>());

        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(terrainObject.scene);
        Selection.activeGameObject = terrainObject;

        _message = "지형과 경계 벽을 만들었습니다. 씬을 저장(Ctrl+S)해야 유지된다.";
        return true;
    }

    // 바닥 전체에 깔 텍스처 한 겹(TerrainLayer)을 만들거나, 이미 있으면 값만 고쳐서 돌려준다.
    private TerrainLayer GetGroundLayer()
    {
        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(LayerPath);
        if (layer == null)
        {
            layer = new TerrainLayer();
            AssetDatabase.CreateAsset(layer, LayerPath);
        }

        layer.diffuseTexture = _groundTexture;
        layer.normalMapTexture = _groundNormal;
        layer.tileSize = new Vector2(_textureTile, _textureTile);
        EditorUtility.SetDirty(layer);
        return layer;
    }

    // ── 경계 벽 ──────────────────────────────────────────────────

    /// <summary>
    /// 지형의 네 가장자리에 보이지 않는 벽(BoxCollider)을 두른다. 전에 두른 벽은 지우고 새로 만든다.
    /// 벽은 지형 오브젝트의 자식으로 두므로, 지형을 옮기면 함께 움직인다.
    ///
    /// 벽에는 장애물 레이어를 주지 않는다.
    /// 길찾기 격자가 지형과 같은 크기라서 추격자는 애초에 지형 밖으로 나가는 경로를 만들지 않고,
    /// 장애물 레이어를 주면 가장자리 칸이 통째로 막힌 칸이 되어 버린다.
    /// </summary>
    private static void BuildWalls(Terrain terrain)
    {
        Transform old = terrain.transform.Find(WallsName);
        if (old != null)
            Undo.DestroyObjectImmediate(old.gameObject);

        var walls = new GameObject(WallsName);
        Undo.RegisterCreatedObjectUndo(walls, "경계 벽 두르기");
        walls.transform.SetParent(terrain.transform, false);

        // 지형 오브젝트 기준으로 지형은 (0, 0)에서 (size.x, size.z)까지 펼쳐져 있다.
        Vector3 size = terrain.terrainData.size;
        float height = size.y + WallExtraHeight;
        float centerY = height * 0.5f;
        float half = WallThickness * 0.5f;

        // 벽의 안쪽 면이 지형의 가장자리에 딱 맞도록, 두께의 절반만큼 바깥으로 내어 세운다.
        // 모서리에 틈이 생기지 않게 앞뒤 벽은 양옆 벽의 두께만큼 더 길게 만든다.
        AddWall(walls.transform, "West",
            new Vector3(-half, centerY, size.z * 0.5f), new Vector3(WallThickness, height, size.z));
        AddWall(walls.transform, "East",
            new Vector3(size.x + half, centerY, size.z * 0.5f), new Vector3(WallThickness, height, size.z));
        AddWall(walls.transform, "South",
            new Vector3(size.x * 0.5f, centerY, -half),
            new Vector3(size.x + WallThickness * 2f, height, WallThickness));
        AddWall(walls.transform, "North",
            new Vector3(size.x * 0.5f, centerY, size.z + half),
            new Vector3(size.x + WallThickness * 2f, height, WallThickness));
    }

    private static void AddWall(Transform parent, string name, Vector3 center, Vector3 size)
    {
        var wall = new GameObject(name);
        wall.transform.SetParent(parent, false);
        wall.transform.localPosition = center;
        wall.AddComponent<BoxCollider>().size = size;
    }

    // ── 숲 ───────────────────────────────────────────────────────

    // 전에 심은 것을 모두 지우고, 지형 위에 나무와 풀을 새로 심는다.
    private void Scatter()
    {
        Terrain terrain = FindTerrain();
        if (terrain == null)
        {
            _message = "씬에 이 도구로 만든 지형이 없습니다. 먼저 지형을 만드세요.";
            return;
        }
        if (_source == null)
        {
            _message = "원본 숲(ForestRoad)이 비어 있어서 숲은 심지 않았습니다.";
            return;
        }

        CollectSources(out List<GameObject> trees, out List<GameObject> plants);
        if (trees.Count == 0 && plants.Count == 0)
        {
            _message = "원본에서 나무와 풀을 찾지 못했습니다. Plant Collider Tool로 먼저 분류를 적용하세요.";
            return;
        }

        GameObject oldRoot = GameObject.Find(PropsName);
        Transform parent = ParentFor(oldRoot, InnerGroupName);
        if (oldRoot != null)
            Undo.DestroyObjectImmediate(oldRoot);

        var root = new GameObject(PropsName);
        Undo.RegisterCreatedObjectUndo(root, "숲 심기");
        root.transform.SetParent(parent, false);
        Transform treeRoot = new GameObject("Trees").transform;
        Transform plantRoot = new GameObject("Plants").transform;
        treeRoot.SetParent(root.transform);
        plantRoot.SetParent(root.transform);

        // 비워 둘 자리. Stage 프리팹이 없거나 StageLayout이 안 붙어 있으면 비워 두지 않는다.
        var clearSpots = new List<Vector3>();
        var clearRadii = new List<float>();
        StageLayout layout = _stagePrefab != null ? _stagePrefab.GetComponent<StageLayout>() : null;
        if (layout != null)
        {
            layout.GetFixedSpots(terrain, out Vector3 exitSpot, out Vector3 playerSpot);
            clearSpots.Add(exitSpot);
            clearRadii.Add(_exitClearRadius);
            clearSpots.Add(playerSpot);
            clearRadii.Add(_playerClearRadius);
        }

        // 시드를 정해 두면 Random이 매번 같은 순서로 숫자를 내놓아서 같은 숲이 만들어진다.
        // 다른 곳에서 쓰는 Random에 영향을 주지 않도록, 끝나면 원래 상태로 되돌린다.
        Random.State oldState = Random.state;
        Random.InitState(_seed);

        var treeSpots = new List<Vector3>();
        int plantedTrees = 0;
        for (int i = 0; i < _treeCount && trees.Count > 0; i++)
        {
            if (!TryPickSpot(terrain, clearSpots, clearRadii, treeSpots, _treeSpacing, out Vector3 spot))
                continue;

            treeSpots.Add(spot);
            Plant(trees[Random.Range(0, trees.Count)], spot, treeRoot);
            plantedTrees++;
        }

        int plantedPlants = 0;
        for (int i = 0; i < _plantCount && plants.Count > 0; i++)
        {
            // 풀은 통과할 수 있으므로 서로 겹쳐도, 나무 옆에 붙어도 괜찮다.
            if (!TryPickSpot(terrain, clearSpots, clearRadii, null, 0f, out Vector3 spot))
                continue;

            Plant(plants[Random.Range(0, plants.Count)], spot, plantRoot);
            plantedPlants++;
        }

        Random.state = oldState;

        EditorSceneManager.MarkSceneDirty(root.scene);
        _message = $"나무 {plantedTrees}그루, 풀·덤불 {plantedPlants}개를 심었습니다. 씬을 저장(Ctrl+S)해야 유지된다.";
        if (plantedTrees < _treeCount)
            _message += $"\n나무 {_treeCount - plantedTrees}그루는 자리가 없어 심지 못했습니다. 나무 사이 최소 거리를 줄이거나 개수를 줄이세요.";
    }

    // 원본의 자식들을 나무와 풀로 나눈다. (Plant Collider Tool이 적용해 둔 Collider 상태로 구분)
    private void CollectSources(out List<GameObject> trees, out List<GameObject> plants)
    {
        trees = new List<GameObject>();
        plants = new List<GameObject>();

        foreach (MeshFilter filter in _source.GetComponentsInChildren<MeshFilter>(true))
        {
            GameObject go = filter.gameObject;

            if (go.TryGetComponent(out CapsuleCollider capsule) && capsule.enabled)
                trees.Add(go);
            // 메시 모양 그대로 막는 것은 원본의 바닥이나 벽이다. 새 지형에는 필요 없다.
            else if (go.TryGetComponent(out MeshCollider meshCollider) && meshCollider.enabled)
                continue;
            else
                plants.Add(go);
        }
    }

    // 지형 위의 무작위 자리를 뽑는다. 조건에 맞는 자리를 끝내 못 찾으면 false.
    private bool TryPickSpot(
        Terrain terrain, List<Vector3> clearSpots, List<float> clearRadii,
        List<Vector3> others, float minDistanceToOthers, out Vector3 spot)
    {
        Vector3 size = terrain.terrainData.size;
        Vector3 origin = terrain.transform.position;

        for (int i = 0; i < MaxTries; i++)
        {
            // 지형의 한쪽 끝을 0, 반대쪽 끝을 1로 본 비율 좌표
            float u = Random.value;
            float v = Random.value;
            spot = origin + new Vector3(u * size.x, 0f, v * size.z);

            if (terrain.terrainData.GetSteepness(u, v) > _maxSlope)
                continue;
            if (IsNearAny(spot, clearSpots, clearRadii))
                continue;
            if (others != null && IsNearAny(spot, others, minDistanceToOthers))
                continue;

            spot.y = terrain.SampleHeight(spot) + origin.y;
            return true;
        }

        spot = Vector3.zero;
        return false;
    }

    private static bool IsNearAny(Vector3 spot, List<Vector3> points, List<float> radii)
    {
        for (int i = 0; i < points.Count; i++)
            if (FlatDistance(spot, points[i]) < radii[i])
                return true;

        return false;
    }

    private static bool IsNearAny(Vector3 spot, List<Vector3> points, float radius)
    {
        foreach (Vector3 point in points)
            if (FlatDistance(spot, point) < radius)
                return true;

        return false;
    }

    // 높이를 뺀, 위에서 내려다본 거리
    private static float FlatDistance(Vector3 a, Vector3 b)
        => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

    // 원본 하나를 복제해서 spot에 심고, 심은 복제본을 돌려준다.
    private GameObject Plant(GameObject original, Vector3 spot, Transform parent)
    {
        Transform source = original.transform;
        GameObject clone = Instantiate(original, parent);
        clone.name = original.name;

        // 원본은 크기를 키운 부모(ForestRoad) 아래에 있었으므로, 부모의 배율까지 곱한 실제 크기를 넣어야
        // 새 부모 아래에서도 같은 크기로 보인다.
        float sizeFactor = _sizeScale * Random.Range(1f - _sizeVariation, 1f + _sizeVariation);
        clone.transform.localScale = source.lossyScale * sizeFactor;

        // 원본이 세워져 있던 방향을 유지한 채, 위에서 봤을 때의 방향만 무작위로 돌린다.
        clone.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * source.rotation;
        clone.transform.position = spot;
        return clone;
    }

    // ── 경계 숲 ──────────────────────────────────────────────────

    /// <summary>
    /// 경계 벽 바깥에 보기만 하는 땅(메시)과 숲을 두른다. 전에 만든 것은 지우고 새로 만든다.
    /// 지형의 가장자리가 훤히 보이지 않고, 숲이 계속 이어지는 것처럼 보이게 하기 위한 것이다.
    ///
    /// 바깥 땅을 Terrain으로 하나 더 만들지 않는 이유:
    /// 여러 스크립트(적 이동, 시작 위치, 횃불 등)가 Terrain.activeTerrain으로 "씬의 지형"을 하나만 찾아 쓰는데,
    /// 지형이 둘이면 엉뚱한 쪽을 잡을 수 있다.
    /// </summary>
    private void BuildBorder()
    {
        Terrain terrain = FindTerrain();
        if (terrain == null)
        {
            _message = "씬에 이 도구로 만든 지형이 없습니다. 먼저 지형을 만드세요.";
            return;
        }

        BuildOuterGround(terrain);

        int plantedTrees = 0;
        int plantedPlants = 0;
        if (_source != null)
            PlantBorderForest(terrain, out plantedTrees, out plantedPlants);

        EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
        _message = $"바깥 땅을 두르고 나무 {plantedTrees}그루, 풀·덤불 {plantedPlants}개를 심었습니다. " +
                   "씬을 저장(Ctrl+S)해야 유지된다.";
        if (_source == null)
            _message += "\n원본 숲(ForestRoad)이 비어 있어서 나무와 풀은 심지 않았습니다.";
    }

    /// <summary>
    /// 바깥 땅의 높이. 지형 위(경계 포함)에서는 지형의 높이와 같고,
    /// 경계 밖에서는 가장 가까운 가장자리의 높이에서 시작해 바깥으로 갈수록 _borderRise만큼 서서히 높아진다.
    /// 경계에서 높이가 딱 맞으므로 지형과 바깥 땅 사이에 턱이 생기지 않는다.
    /// </summary>
    private float OuterHeightAt(Terrain terrain, float x, float z)
    {
        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        float edgeX = Mathf.Clamp(x, origin.x, origin.x + size.x);
        float edgeZ = Mathf.Clamp(z, origin.z, origin.z + size.z);
        float edgeHeight = terrain.SampleHeight(new Vector3(edgeX, 0f, edgeZ)) + origin.y;

        float outside = Vector2.Distance(new Vector2(x, z), new Vector2(edgeX, edgeZ));
        if (outside <= 0f)
            return edgeHeight;

        // 경계에서는 기울기 0으로 시작해서 바깥 끝에서 다시 평평해지는 곡선(smoothstep)
        float t = Mathf.Clamp01(outside / _borderWidth);
        float rise = _borderRise * t * t * (3f - 2f * t);
        // 안쪽처럼 얕은 굴곡을 준다. 경계 근처에서는 굴곡을 줄여 지형과 매끄럽게 이어지게 한다.
        float bumps = (Mathf.PerlinNoise((x + 1000f) / _bumpSize, (z + 1000f) / _bumpSize) - 0.5f)
                      * _bumpHeight * Mathf.Clamp01(outside / 10f);
        return edgeHeight + rise + bumps;
    }

    /// <summary>
    /// 지형을 액자처럼 둘러싸는 바깥 땅 메시를 만든다.
    /// 칸 크기 OuterCellSize의 격자로 만들되, 지형에 완전히 덮이는 안쪽 칸은 빼고
    /// 지형 가장자리 바로 아래의 한 줄만 남긴다(지형보다 조금 낮게). 그래서 이음매에 틈이 보이지 않는다.
    /// 메시와 재질은 Assets/_Game/Environment/Terrain에 저장해서 씬을 다시 열어도 남게 한다.
    /// </summary>
    private void BuildOuterGround(Terrain terrain)
    {
        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        int borderCells = Mathf.CeilToInt(_borderWidth / OuterCellSize);
        int innerCellsX = Mathf.RoundToInt(size.x / OuterCellSize);
        int innerCellsZ = Mathf.RoundToInt(size.z / OuterCellSize);
        int cellsX = innerCellsX + borderCells * 2;
        int cellsZ = innerCellsZ + borderCells * 2;
        float startX = origin.x - borderCells * OuterCellSize;
        float startZ = origin.z - borderCells * OuterCellSize;

        // 바닥 텍스처는 지형에 칠한 첫 번째 텍스처를 그대로 쓴다. 텍스처 한 장이 덮는 너비도 같게 맞춘다.
        TerrainLayer layer = terrain.terrainData.terrainLayers.Length > 0 ? terrain.terrainData.terrainLayers[0] : null;
        Vector2 tile = layer != null ? layer.tileSize : new Vector2(_textureTile, _textureTile);

        int rowLength = cellsX + 1;
        var vertices = new Vector3[rowLength * (cellsZ + 1)];
        var uvs = new Vector2[vertices.Length];
        for (int z = 0; z <= cellsZ; z++)
        {
            for (int x = 0; x <= cellsX; x++)
            {
                float worldX = startX + x * OuterCellSize;
                float worldZ = startZ + z * OuterCellSize;
                int innerX = x - borderCells;
                int innerZ = z - borderCells;
                bool underTerrain = innerX > 0 && innerX < innerCellsX && innerZ > 0 && innerZ < innerCellsZ;

                float height = underTerrain
                    ? terrain.SampleHeight(new Vector3(worldX, 0f, worldZ)) + origin.y - OuterTuckDepth
                    : OuterHeightAt(terrain, worldX, worldZ);

                int index = z * rowLength + x;
                vertices[index] = new Vector3(worldX, height, worldZ);
                uvs[index] = new Vector2(worldX / tile.x, worldZ / tile.y);
            }
        }

        var triangles = new List<int>();
        for (int z = 0; z < cellsZ; z++)
        {
            for (int x = 0; x < cellsX; x++)
            {
                // 지형에 완전히 덮이는 칸은 뺀다. (가장자리에 닿는 칸은 남긴다)
                int innerX = x - borderCells;
                int innerZ = z - borderCells;
                bool hidden = innerX >= 1 && innerX + 1 <= innerCellsX - 1 &&
                              innerZ >= 1 && innerZ + 1 <= innerCellsZ - 1;
                if (hidden)
                    continue;

                int v00 = z * rowLength + x;
                int v10 = v00 + 1;
                int v01 = v00 + rowLength;
                int v11 = v01 + 1;
                // 위에서 봤을 때 시계 방향이어야 윗면이 보인다.
                triangles.Add(v00); triangles.Add(v01); triangles.Add(v11);
                triangles.Add(v00); triangles.Add(v11); triangles.Add(v10);
            }
        }

        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(OuterMeshPath);
        if (mesh == null)
        {
            mesh = new Mesh { name = "OuterGround" };
            AssetDatabase.CreateAsset(mesh, OuterMeshPath);
        }
        else
            mesh.Clear();

        // 바깥 땅을 넓게 잡으면 꼭짓점이 65,535개를 넘을 수 있다.
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);

        Material material = GetOuterMaterial(layer);
        AssetDatabase.SaveAssets();

        GameObject old = GameObject.Find(OuterGroundName);
        Transform parent = ParentFor(old, OuterGroupName);
        if (old != null)
            Undo.DestroyObjectImmediate(old);

        // 꼭짓점이 월드 좌표로 들어 있으므로 원점에 둔다. 몸은 경계 벽이 막으므로 Collider는 붙이지 않는다.
        var ground = new GameObject(OuterGroundName);
        Undo.RegisterCreatedObjectUndo(ground, "바깥 땅 만들기");
        ground.transform.SetParent(parent, false);
        ground.AddComponent<MeshFilter>().sharedMesh = mesh;
        var groundRenderer = ground.AddComponent<MeshRenderer>();
        groundRenderer.sharedMaterial = material;
        groundRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // 바깥 땅에 쓸 재질(Standard)을 만들거나, 이미 있으면 텍스처만 다시 맞춰서 돌려준다.
    private Material GetOuterMaterial(TerrainLayer layer)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(OuterMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, OuterMaterialPath);
        }

        Texture2D diffuse = layer != null ? layer.diffuseTexture : _groundTexture;
        Texture2D normal = layer != null ? layer.normalMapTexture : _groundNormal;
        material.mainTexture = diffuse;
        if (normal != null)
        {
            material.SetTexture("_BumpMap", normal);
            material.EnableKeyword("_NORMALMAP");
        }
        // 지형처럼 번들거리지 않게 한다.
        material.SetFloat("_Glossiness", 0f);
        EditorUtility.SetDirty(material);
        return material;
    }

    /// <summary>
    /// 바깥 땅에 나무와 풀을 안쪽 숲과 같은 밀도로 심는다.
    /// (안쪽의 나무 개수와 풀·덤불 개수를, 바깥 땅의 넓이가 지형의 몇 배인지에 맞춰 늘린다)
    /// 보기만 하는 숲이므로 복제본의 Collider를 모두 뺀다. 그래서 물리 계산 부담이 없다.
    /// </summary>
    private void PlantBorderForest(Terrain terrain, out int plantedTrees, out int plantedPlants)
    {
        plantedTrees = 0;
        plantedPlants = 0;

        CollectSources(out List<GameObject> trees, out List<GameObject> plants);

        GameObject oldRoot = GameObject.Find(BorderForestName);
        Transform parent = ParentFor(oldRoot, OuterGroupName);
        if (oldRoot != null)
            Undo.DestroyObjectImmediate(oldRoot);

        var root = new GameObject(BorderForestName);
        Undo.RegisterCreatedObjectUndo(root, "경계 숲 심기");
        root.transform.SetParent(parent, false);
        Transform treeRoot = new GameObject("Trees").transform;
        Transform plantRoot = new GameObject("Plants").transform;
        treeRoot.SetParent(root.transform);
        plantRoot.SetParent(root.transform);

        Vector3 size = terrain.terrainData.size;
        float innerArea = size.x * size.z;
        float outerArea = (size.x + _borderWidth * 2f) * (size.z + _borderWidth * 2f) - innerArea;
        int treeCount = Mathf.RoundToInt(_treeCount * outerArea / innerArea);
        int plantCount = Mathf.RoundToInt(_plantCount * outerArea / innerArea);

        // 안쪽 숲과 다른 무늬가 나오도록 시드를 조금 바꾼다.
        Random.State oldState = Random.state;
        Random.InitState(_seed + 7919);

        var treeSpots = new List<Vector3>();
        for (int i = 0; i < treeCount && trees.Count > 0; i++)
        {
            if (!TryPickBorderSpot(terrain, treeSpots, _treeSpacing, out Vector3 spot))
                continue;

            treeSpots.Add(spot);
            RemoveColliders(Plant(trees[Random.Range(0, trees.Count)], spot, treeRoot));
            plantedTrees++;
        }

        for (int i = 0; i < plantCount && plants.Count > 0; i++)
        {
            if (!TryPickBorderSpot(terrain, null, 0f, out Vector3 spot))
                continue;

            RemoveColliders(Plant(plants[Random.Range(0, plants.Count)], spot, plantRoot));
            plantedPlants++;
        }

        Random.state = oldState;
    }

    // 바깥 땅 위의 무작위 자리를 뽑는다. 경계 안쪽(플레이하는 곳)과 경계 바로 바깥 1m는 비운다.
    private bool TryPickBorderSpot(Terrain terrain, List<Vector3> others, float minDistanceToOthers, out Vector3 spot)
    {
        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        const float gap = 1f;

        for (int i = 0; i < MaxTries; i++)
        {
            float x = Random.Range(origin.x - _borderWidth, origin.x + size.x + _borderWidth);
            float z = Random.Range(origin.z - _borderWidth, origin.z + size.z + _borderWidth);
            bool insideBoundary = x > origin.x - gap && x < origin.x + size.x + gap &&
                                  z > origin.z - gap && z < origin.z + size.z + gap;
            if (insideBoundary)
                continue;

            spot = new Vector3(x, 0f, z);
            if (others != null && IsNearAny(spot, others, minDistanceToOthers))
                continue;

            spot.y = OuterHeightAt(terrain, x, z);
            return true;
        }

        spot = Vector3.zero;
        return false;
    }

    private static void RemoveColliders(GameObject clone)
    {
        foreach (Collider collider in clone.GetComponentsInChildren<Collider>(true))
            DestroyImmediate(collider);
    }
}
