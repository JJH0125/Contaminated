using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 외부 에셋 맵(숲 등)의 Collider를 용도에 맞게 정리하는 에디터 도구.
/// 메뉴: Tools > Plant Collider Tool
///
/// 오브젝트 이름으로는 무엇인지 알 수 없으므로, 재질과 메시의 모양을 보고 넷으로 나눈다.
///
///   바닥      불투명 재질만 쓰고, 면이 대부분 위를 향한다 (걸을 수 있는 기울기)
///             → MeshCollider 켬
///   벽        불투명 재질만 쓰고, 면이 대부분 옆을 향한다
///             → MeshCollider 켬 + 장애물 레이어
///   나무      잎(투명하게 잘라 그리는 재질)이 있고, 폭보다 키가 크다
///             → MeshCollider 끔 + 기둥 자리에만 CapsuleCollider + 장애물 레이어
///   풀·덤불   잎 재질만 쓰고, 키가 폭과 비슷하거나 더 작다
///             → Collider 없음
///
/// 나무를 MeshCollider로 두지 않는 이유:
///   이 숲의 나무는 잎 그림을 붙인 판을 엇갈려 세운 모양이라, MeshCollider를 켜면
///   투명한 부분까지 벽이 된다. 그래서 잎은 통과시키고 기둥 자리에만 캡슐을 세운다.
///   - 기둥이 따로 만들어진 나무(불투명 재질 부분이 있음)는 그 부분의 크기에 맞춘다
///   - 판으로만 된 나무는 기둥이 그림으로만 있으므로, 한가운데에 폭의 일정 비율로 세운다
///
/// 장애물 레이어를 주는 이유:
///   PathGrid(추격자 길찾기)와 VisionSensor(시야 차폐)가 그 레이어의 Collider만 장애물로 본다.
///   바닥까지 그 레이어에 들어가면 맵 전체가 막힌 칸이 되므로, 바닥은 반드시 빼야 한다.
///
/// Collider를 지우지 않고 켜고 끄기만 하므로 언제든 되돌릴 수 있다. (Ctrl+Z도 가능)
/// </summary>
public class PlantColliderTool : EditorWindow
{
    private enum Kind { Ground, Wall, Tree, Plant }

    private GameObject _root;
    private string _message = "";

    // 자동 분류
    private float _maxWalkableSlope = 50f;
    private float _minTreeAspect = 1.4f;
    private float _trunkWidthRatio = 0.12f;
    private bool _setObstacleLayer = true;
    private int _obstacleLayer;
    private Dictionary<Kind, List<MeshCollider>> _classified;

    // 직접 고르기
    private bool _showManual;
    private float _maxSize = 3f;
    private string _nameFilter = "";

    [MenuItem("Tools/Plant Collider Tool")]
    private static void Open() => GetWindow<PlantColliderTool>("Plant Colliders");

    private void OnEnable()
    {
        // PathGrid와 VisionSensor가 쓰는 장애물 레이어. 없으면 레이어 지정을 끈 채로 시작한다.
        _obstacleLayer = LayerMask.NameToLayer("Unwalkable");
        if (_obstacleLayer < 0)
        {
            _obstacleLayer = 0;
            _setObstacleLayer = false;
        }
    }

    private void OnSelectionChange() => Repaint();

    private void OnGUI()
    {
        _root = (GameObject)EditorGUILayout.ObjectField(
            "대상 (ForestRoad)", _root, typeof(GameObject), true);

        using (new EditorGUI.DisabledScope(_root == null))
        {
            DrawAutoSection();
            EditorGUILayout.Space();
            DrawManualSection();
        }

        if (_message != "")
            EditorGUILayout.HelpBox(_message, MessageType.Info);
    }

    // ── 자동 분류 ────────────────────────────────────────────────

    private void DrawAutoSection()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("자동 분류", EditorStyles.boldLabel);

        _maxWalkableSlope = EditorGUILayout.Slider(
            new GUIContent("바닥으로 볼 최대 기울기", "불투명한 메시의 면이 이 각도(도)보다 완만하면 걸을 수 있는 면으로 센다"),
            _maxWalkableSlope, 10f, 80f);
        _minTreeAspect = EditorGUILayout.Slider(
            new GUIContent("나무로 볼 최소 키/폭 비율", "잎 재질만 쓰는 오브젝트가 이 비율보다 길쭉하면 나무, 아니면 풀·덤불로 본다"),
            _minTreeAspect, 0.5f, 3f);
        _trunkWidthRatio = EditorGUILayout.Slider(
            new GUIContent("판 나무의 기둥 굵기", "판으로만 된 나무에 세울 기둥의 지름. 나무 폭에 대한 비율"),
            _trunkWidthRatio, 0.02f, 0.5f);

        _setObstacleLayer = EditorGUILayout.Toggle(
            new GUIContent("벽·나무에 장애물 레이어 지정", "추격자가 피해 다니고, 시야를 가리게 된다"),
            _setObstacleLayer);
        using (new EditorGUI.DisabledScope(!_setObstacleLayer))
            _obstacleLayer = EditorGUILayout.LayerField("장애물 레이어", _obstacleLayer);

        if (GUILayout.Button("1. 분류하기 (아직 아무것도 바꾸지 않는다)"))
            Classify();

        if (_classified == null)
            return;

        DrawKindRow("바닥", Kind.Ground);
        DrawKindRow("벽", Kind.Wall);
        DrawKindRow("나무 (기둥만 막힘)", Kind.Tree);
        DrawKindRow("풀·덤불 (통과)", Kind.Plant);

        if (GUILayout.Button("2. 분류대로 적용"))
            ApplyClassification();
    }

    // 분류 결과 한 줄. "선택"을 누르면 Scene에서 어떤 오브젝트가 그 종류로 묶였는지 볼 수 있다.
    private void DrawKindRow(string label, Kind kind)
    {
        List<MeshCollider> colliders = _classified[kind];

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"{label}: {colliders.Count}개");
        if (GUILayout.Button("선택", GUILayout.Width(50f)))
            Select(colliders);
        EditorGUILayout.EndHorizontal();
    }

    private void Classify()
    {
        _classified = new Dictionary<Kind, List<MeshCollider>>
        {
            { Kind.Ground, new List<MeshCollider>() },
            { Kind.Wall, new List<MeshCollider>() },
            { Kind.Tree, new List<MeshCollider>() },
            { Kind.Plant, new List<MeshCollider>() },
        };

        foreach (MeshCollider collider in _root.GetComponentsInChildren<MeshCollider>(true))
            if (collider.sharedMesh != null)
                _classified[KindOf(collider)].Add(collider);

        _message = "분류 완료. 각 줄의 \"선택\"으로 Scene에서 확인한 뒤 적용할 것.\n" +
                   "나무와 풀이 섞여 있으면 키/폭 비율을 조절해 다시 분류한다.";
    }

    private Kind KindOf(MeshCollider collider)
    {
        CountMaterials(collider, out int opaqueCount, out int foliageCount);

        // 잎 재질이 전혀 없으면 지형이나 구조물이다. 기울기로 바닥과 벽을 나눈다.
        if (foliageCount == 0)
            return WalkableRatio(collider) >= 0.5f ? Kind.Ground : Kind.Wall;

        // 잎 재질과 불투명 재질(나무껍질)을 함께 쓰면 기둥이 따로 만들어진 나무다.
        if (opaqueCount > 0)
            return Kind.Tree;

        // 잎 재질만 쓰는 판 묶음은 길쭉하면 나무, 아니면 풀이나 덤불이다.
        GetWorldSize(collider, out float height, out float width, out _);
        return height >= width * _minTreeAspect ? Kind.Tree : Kind.Plant;
    }

    // 투명하게 잘라 그리는 재질(Cutout)이나 반투명 재질은 잎과 풀에만 쓴다.
    private static bool IsFoliage(Material material)
        => material != null && material.renderQueue >= (int)RenderQueue.AlphaTest;

    // 메시의 부분(서브메시)마다 재질이 하나씩 붙는다. 불투명 재질과 잎 재질이 각각 몇 부분인지 센다.
    private static void CountMaterials(MeshCollider collider, out int opaqueCount, out int foliageCount)
    {
        opaqueCount = 0;
        foliageCount = 0;

        Renderer renderer = collider.GetComponent<Renderer>();
        if (renderer == null)
        {
            opaqueCount = 1;
            return;
        }

        foreach (Material material in renderer.sharedMaterials)
        {
            if (IsFoliage(material))
                foliageCount++;
            else
                opaqueCount++;
        }
    }

    /// <summary>
    /// 메시의 삼각형을 하나씩 보고, 전체 넓이 중 걸을 수 있을 만큼 완만한 면이 차지하는 비율을 구한다.
    /// 큰 삼각형일수록 더 많이 반영되도록 넓이로 가중한다.
    /// </summary>
    private float WalkableRatio(MeshCollider collider)
    {
        Mesh mesh = collider.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        // 오브젝트가 회전되어 있을 수 있으므로 월드 좌표로 옮긴 뒤 계산한다.
        Transform t = collider.transform;
        for (int i = 0; i < vertices.Length; i++)
            vertices[i] = t.TransformPoint(vertices[i]);

        // 면이 위를 향하는 정도(normal.y)가 이 값 이상이면 걸을 수 있는 면이다.
        float minWalkableNormalY = Mathf.Cos(_maxWalkableSlope * Mathf.Deg2Rad);

        float totalArea = 0f;
        float walkableArea = 0f;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 a = vertices[triangles[i]];
            Vector3 b = vertices[triangles[i + 1]];
            Vector3 c = vertices[triangles[i + 2]];

            // 외적의 방향이 면이 향하는 쪽, 길이가 넓이의 2배다.
            Vector3 cross = Vector3.Cross(b - a, c - a);
            float area = cross.magnitude;
            totalArea += area;
            // 뒤집힌 면도 같은 기울기로 보도록 절댓값을 쓴다.
            if (area > 0f && Mathf.Abs(cross.y) / area >= minWalkableNormalY)
                walkableArea += area;
        }

        return totalArea > 0f ? walkableArea / totalArea : 0f;
    }

    /// <summary>
    /// 오브젝트의 실제 키와 폭을 구한다. upAxis는 오브젝트의 세 축(0: x, 1: y, 2: z) 중 위를 향하는 축.
    /// 외부 모델은 눕힌 채로 회전시켜 세워 둔 경우가 많아서, 어느 축이 위인지부터 찾아야 한다.
    /// 월드 기준 상자(Renderer.bounds)는 오브젝트가 돌아가 있으면 폭이 부풀려지므로 쓰지 않는다.
    /// </summary>
    private static void GetWorldSize(MeshCollider collider, out float height, out float width, out int upAxis)
    {
        Transform t = collider.transform;

        Vector3 up = t.InverseTransformDirection(Vector3.up);
        up = new Vector3(Mathf.Abs(up.x), Mathf.Abs(up.y), Mathf.Abs(up.z));
        if (up.x > up.y && up.x > up.z)
            upAxis = 0;
        else
            upAxis = up.y > up.z ? 1 : 2;

        Vector3 size = Vector3.Scale(collider.sharedMesh.bounds.size, Abs(t.lossyScale));
        height = size[upAxis];
        width = Mathf.Max(size[(upAxis + 1) % 3], size[(upAxis + 2) % 3]);
    }

    /// <summary>
    /// 나무의 기둥이 차지하는 공간을 오브젝트 기준 좌표의 상자로 구한다.
    ///   기둥이 따로 만들어진 나무  불투명 재질을 쓰는 부분을 감싸는 상자
    ///   판으로만 된 나무           한가운데에, 폭의 일정 비율만큼 가늘게 세운 상자
    /// </summary>
    private Bounds GetTrunkBounds(MeshCollider collider)
    {
        Mesh mesh = collider.sharedMesh;
        Renderer renderer = collider.GetComponent<Renderer>();
        Material[] materials = renderer != null ? renderer.sharedMaterials : new Material[0];

        Vector3[] vertices = mesh.vertices;
        bool hasTrunk = false;
        Bounds trunk = new Bounds();

        for (int sub = 0; sub < mesh.subMeshCount && sub < materials.Length; sub++)
        {
            if (IsFoliage(materials[sub]))
                continue;

            foreach (int index in mesh.GetTriangles(sub))
            {
                if (!hasTrunk)
                {
                    trunk = new Bounds(vertices[index], Vector3.zero);
                    hasTrunk = true;
                }
                else
                    trunk.Encapsulate(vertices[index]);
            }
        }

        if (hasTrunk)
            return trunk;

        GetWorldSize(collider, out _, out float width, out int upAxis);
        Vector3 scale = Abs(collider.transform.lossyScale);

        // 오브젝트의 크기 배율이 축마다 다를 수 있으므로, 실제 굵기를 각 축의 배율로 나눠 넣는다.
        Vector3 size = mesh.bounds.size;
        for (int axis = 0; axis < 3; axis++)
            if (axis != upAxis && scale[axis] > 0f)
                size[axis] = width * _trunkWidthRatio / scale[axis];

        return new Bounds(mesh.bounds.center, size);
    }

    private void ApplyClassification()
    {
        var changed = new List<Object>();
        foreach (List<MeshCollider> colliders in _classified.Values)
            foreach (MeshCollider collider in colliders)
            {
                changed.Add(collider);
                changed.Add(collider.gameObject);
            }

        // Ctrl+Z로 되돌릴 수 있도록 기록한다.
        Undo.RecordObjects(changed.ToArray(), "Collider 분류 적용");

        foreach (KeyValuePair<Kind, List<MeshCollider>> pair in _classified)
        {
            Kind kind = pair.Key;
            foreach (MeshCollider collider in pair.Value)
            {
                GameObject go = collider.gameObject;

                // 바닥과 벽만 메시 모양 그대로 막는다.
                collider.enabled = kind == Kind.Ground || kind == Kind.Wall;

                if (kind == Kind.Tree)
                    FitTrunkCapsule(collider);
                // 전에 나무로 분류되어 캡슐이 붙었던 오브젝트가 다른 종류로 바뀌었으면 캡슐을 끈다.
                else if (go.TryGetComponent(out CapsuleCollider oldCapsule))
                {
                    Undo.RecordObject(oldCapsule, "Collider 분류 적용");
                    oldCapsule.enabled = false;
                }

                if (!_setObstacleLayer)
                    continue;

                if (kind == Kind.Wall || kind == Kind.Tree)
                    go.layer = _obstacleLayer;
                // 바닥이나 풀이 장애물 레이어에 남아 있으면 길찾기 격자가 통째로 막히므로 빼낸다.
                else if (go.layer == _obstacleLayer)
                    go.layer = 0;
            }
        }

        _message = $"적용 완료. 바닥 {_classified[Kind.Ground].Count}개, 벽 {_classified[Kind.Wall].Count}개, " +
                   $"나무 {_classified[Kind.Tree].Count}개, 풀·덤불 {_classified[Kind.Plant].Count}개.\n" +
                   "씬을 저장(Ctrl+S)해야 유지된다.";
    }

    // 나무의 기둥 자리에 CapsuleCollider를 맞춘다. 이미 있으면 새로 붙이지 않고 값만 고친다.
    private void FitTrunkCapsule(MeshCollider collider)
    {
        GameObject go = collider.gameObject;
        if (go.TryGetComponent(out CapsuleCollider capsule))
            Undo.RecordObject(capsule, "Collider 분류 적용");
        else
            capsule = Undo.AddComponent<CapsuleCollider>(go);

        Bounds trunk = GetTrunkBounds(collider);
        GetWorldSize(collider, out _, out _, out int upAxis);

        capsule.enabled = true;
        capsule.direction = upAxis;
        capsule.center = trunk.center;
        capsule.height = trunk.size[upAxis];
        capsule.radius = Mathf.Max(trunk.size[(upAxis + 1) % 3], trunk.size[(upAxis + 2) % 3]) * 0.5f;
    }

    // ── 직접 고르기 ──────────────────────────────────────────────

    // 자동 분류가 틀린 오브젝트를 이름이나 크기로 골라 MeshCollider를 직접 켜고 끌 때 쓴다.
    private void DrawManualSection()
    {
        _showManual = EditorGUILayout.Foldout(_showManual, "직접 고르기 (이름·크기)", true);
        if (!_showManual)
            return;

        _maxSize = EditorGUILayout.FloatField(
            new GUIContent("기준 크기", "가장 긴 변이 이 값보다 작은 오브젝트만 고른다 (월드 단위)"),
            _maxSize);
        _nameFilter = EditorGUILayout.TextField(
            new GUIContent("이름 필터", "비워두면 이름과 상관없이 크기로만 고른다"),
            _nameFilter);

        // 기준값을 정할 수 있도록, Scene에서 클릭한 오브젝트의 크기를 보여준다.
        GameObject selected = Selection.activeGameObject;
        Renderer selectedRenderer = selected != null ? selected.GetComponent<Renderer>() : null;
        if (selectedRenderer != null)
            EditorGUILayout.HelpBox(
                $"선택한 오브젝트: {selected.name}\n가장 긴 변: {LongestSide(selectedRenderer):0.00}",
                MessageType.None);
        else
            EditorGUILayout.HelpBox(
                "Scene에서 풀이나 나무를 클릭하면 크기가 여기 표시된다.\n" +
                "(모델의 자식을 고르려면 같은 곳을 한 번 더 클릭)",
                MessageType.None);

        if (GUILayout.Button("조건에 맞는 오브젝트 선택 (미리보기)"))
        {
            List<MeshCollider> small = FindSmallColliders();
            Select(small);
            _message = $"{small.Count}개 선택됨.";
        }

        if (GUILayout.Button("조건에 맞는 MeshCollider 끄기"))
            SetColliders(FindSmallColliders(), false, "끔");

        if (GUILayout.Button("조건에 맞는 MeshCollider 켜기"))
            SetColliders(FindSmallColliders(), true, "켬");
    }

    // 대상 아래의 MeshCollider 중, 이름 필터에 맞고
    // 같은 오브젝트의 Renderer 크기가 기준보다 작은 것만 모은다.
    private List<MeshCollider> FindSmallColliders()
    {
        var result = new List<MeshCollider>();
        foreach (MeshCollider collider in _root.GetComponentsInChildren<MeshCollider>(true))
        {
            if (_nameFilter != "" && !collider.name.Contains(_nameFilter))
                continue;

            Renderer renderer = collider.GetComponent<Renderer>();
            if (renderer != null && LongestSide(renderer) < _maxSize)
                result.Add(collider);
        }
        return result;
    }

    private void SetColliders(List<MeshCollider> colliders, bool enabled, string verb)
    {
        // Ctrl+Z로 되돌릴 수 있도록 기록한다.
        Undo.RecordObjects(colliders.ToArray(), $"Collider {verb}");
        foreach (MeshCollider collider in colliders)
            collider.enabled = enabled;
        _message = $"MeshCollider {colliders.Count}개를 {verb}. 씬을 저장(Ctrl+S)해야 유지된다.";
    }

    private static void Select(List<MeshCollider> colliders)
    {
        var objects = new List<Object>();
        foreach (MeshCollider collider in colliders)
            objects.Add(collider.gameObject);
        Selection.objects = objects.ToArray();
    }

    private static float LongestSide(Renderer renderer)
    {
        Vector3 size = renderer.bounds.size;
        return Mathf.Max(size.x, Mathf.Max(size.y, size.z));
    }

    private static Vector3 Abs(Vector3 v)
        => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
}
