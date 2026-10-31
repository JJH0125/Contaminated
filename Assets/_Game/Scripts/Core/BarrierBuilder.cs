using System.Collections.Generic;
using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 라운드마다 놓는 장벽과 오염의 모습을 코드로 만든다. StageLayout이 부른다.
    ///   나무 벽     구역의 경계를 영원히 막는다. 높은 나무 줄이고, 줄기 사이로 건너편이 보인다. 두 차원 모두에서 몸과 길을 막는다.
    ///               플레이어를 가려도 투명해지지 않는다.
    ///               열린 구역끼리의 벽에는 관문 자리가 비어 있다
    ///   봉인된 관문 Real에서만 막는 짧은 오염 덤불 벽. 검게 엉겨 있어 나무 벽과 구분된다. 이어진 모닥불을 켜면 타서 열린다
    ///   덤불 고리   꺼진 모닥불을 둘러싼 오염 덤불. Real에서만 막는다
    ///   오염 웅덩이 Fake에서만 막는다
    ///   뿌리        꺼진 모닥불에서 출구와 관문까지 이어진다. Fake에서만 보인다 (RootLine)
    ///
    /// 덤불은 씬의 숲(Forest Props)에 심어 둔 나무와 풀을 복제해 늘어놓는다. 숲과 같은 모습이라 맵에 어울리고,
    /// 따로 에셋을 연결하지 않아도 된다. 복제본의 Collider는 지우고, 몸과 길은 "Colliders" 아래의 상자가 막는다.
    /// 오염 덤불은 복제본의 재질에 오염 텍스처를 덧입힌다 (ApplyGrime, Resources/Textures/Contamination).
    /// 만든 것은 모두 Stage의 자식으로 두므로 라운드가 끝나면 함께 사라진다.
    /// </summary>
    public static class BarrierBuilder
    {
        // Forest Map Tool이 나무와 풀을 심어 두는 씬 오브젝트의 이름
        private const string ForestPropsName = "Forest Props";
        // 장벽 상자의 높이(m). 봉우리의 비탈에서도 몸이 넘어가지 않을 만큼 높게 둔다.
        private const float WallHeight = 40f;
        // 덤불 벽의 상자 하나가 맡는 길이(m). 짧을수록 비탈을 잘 따라간다.
        private const float SegmentLength = 8f;

        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private const string GrimeTextureFolder = "Textures/Contamination/";
        private const string BushTexture = "moss_wood";
        private const string GrimeShaderName = "Hidden/Custom/ContaminatedSurface";

        private static readonly int GrimeTexId = Shader.PropertyToID("_GrimeTex");
        private static readonly int GrimeSizeId = Shader.PropertyToID("_GrimeSize");
        private static readonly int GrimeAmountId = Shader.PropertyToID("_GrimeAmount");
        private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");

        // 원래 재질과 텍스처마다 오염 재질을 하나씩만 만들어 함께 쓴다. (덤불마다 복제하면 수백 개가 되어 무거워진다)
        private static readonly Dictionary<(Material, string), Material> _grimeMaterials = new();

        private static MapTuning Tuning => GameTuning.Instance.map;

        /// 적의 시선을 가리는 레이어. 나무 기둥과 같은 장애물 레이어다.
        public static int SightBlockingLayer
        {
            get
            {
                int layer = LayerMask.NameToLayer("Unwalkable");
                return layer >= 0 ? layer : 3;
            }
        }

        // ── 덤불 벽과 고리 ───────────────────────────────────────────

        /// <summary>
        /// from에서 to까지 구역 벽을 세운다. 높은 나무 줄이고, 줄기 사이로 건너편이 보이지만 지나갈 수는 없다.
        /// 두 차원 모두에서 몸과 길을 막는다. 불로 걷히지 않고, 플레이어를 가려도 투명해지지 않는다.
        /// </summary>
        public static DimensionObstacle BuildTreeWall(Transform parent, string name, Vector3 from, Vector3 to)
        {
            GameObject wall = new GameObject(name);
            wall.transform.SetParent(parent, false);
            Transform colliders = NewChild(wall.transform, "Colliders");
            AddWallBoxes(colliders, from, to, out float length, out Vector3 direction, blocksSight: Tuning.zoneWallBlocksSight);

            // 숲에서 나무를 찾지 못하면 덤불로 대신한다.
            List<GameObject> trees = TallestTrees();
            if (trees.Count == 0)
                trees = Templates(trees: false);

            Vector3 side = Vector3.Cross(Vector3.up, direction);
            float spacing = Mathf.Max(Tuning.treeWallSpacing, 1f);
            Color tint = Tuning.zoneWallTint;
            for (float along = 0f; along <= length; along += spacing)
            {
                Vector3 tree = from + direction * (along + Random.Range(-0.5f, 0.5f)) + side * Random.Range(-0.5f, 0.5f);
                GameObject trunk = PlaceBush(wall.transform, trees, tree, tint, Tuning.treeWallScaleMin, Tuning.treeWallScaleMax, fades: false);
                // 키는 그대로 두고 옆으로만 키워 굵은 줄기로 만든다.
                Vector3 girth = trunk.transform.localScale;
                girth.x *= Tuning.treeWallGirth;
                girth.z *= Tuning.treeWallGirth;
                trunk.transform.localScale = girth;
            }

            return Finish(wall, colliders, Dimension.None);
        }

        /// <summary>
        /// from에서 to까지 봉인된 관문을 세운다. 검게 엉긴 오염 덤불로, Real에서만 막고 적의 시선은 가리지 않는다.
        /// 구역 벽의 나무 줄과 모양이 달라서 "여기는 나중에 열린다"는 것을 알 수 있다.
        /// </summary>
        public static DimensionObstacle BuildSealedGate(Transform parent, string name, Vector3 from, Vector3 to)
        {
            GameObject gate = new GameObject(name);
            gate.transform.SetParent(parent, false);
            Transform colliders = NewChild(gate.transform, "Colliders");
            AddWallBoxes(colliders, from, to, out float length, out Vector3 direction, blocksSight: false);

            List<GameObject> templates = Templates(trees: false);
            Vector3 side = Vector3.Cross(Vector3.up, direction);
            float thickness = Tuning.zoneWallThickness;
            int rows = Mathf.Max(1, Tuning.sealedGateBushRows);
            for (float along = 0f; along <= length; along += Mathf.Max(Tuning.sealedGateBushSpacing, 0.5f))
            {
                for (int row = 0; row < rows; row++)
                {
                    float across = rows > 1 ? Mathf.Lerp(-thickness * 0.4f, thickness * 0.4f, row / (rows - 1f)) : 0f;
                    Vector3 spot = from + direction * (along + Random.Range(-0.4f, 0.4f)) + side * (across + Random.Range(-0.4f, 0.4f));
                    PlaceContaminatedBush(gate.transform, templates, spot);
                }
            }

            return Finish(gate, colliders, Dimension.Real);
        }

        /// <summary>
        /// 출구 덤불 아치의 앞면을 세운다. 검게 엉긴 오염 덤불로 두 차원 모두에서 막고, 가운데가 높이 솟는다.
        /// 나중에 하나씩 태울 수 있게 덤불들을 돌려준다. (ExitArch)
        /// </summary>
        public static DimensionObstacle BuildExitFront(Transform parent, Vector3 from, Vector3 to, out List<GameObject> bushes)
        {
            GameObject front = new GameObject("Exit Arch");
            front.transform.SetParent(parent, false);
            Transform colliders = NewChild(front.transform, "Colliders");
            AddWallBoxes(colliders, from, to, out float length, out Vector3 direction, blocksSight: false);

            bushes = new List<GameObject>();
            List<GameObject> templates = Templates(trees: false);
            Vector3 side = Vector3.Cross(Vector3.up, direction);
            float thickness = Tuning.zoneWallThickness;
            int rows = Mathf.Max(1, Tuning.exitArchBushRows);
            for (float along = 0f; along <= length; along += Mathf.Max(Tuning.exitArchBushSpacing, 0.5f))
            {
                float peak = Mathf.Lerp(1f, Tuning.exitArchPeak, Mathf.Sin(along / Mathf.Max(length, 0.01f) * Mathf.PI));
                for (int row = 0; row < rows; row++)
                {
                    float across = rows > 1 ? Mathf.Lerp(-thickness * 0.4f, thickness * 0.4f, row / (rows - 1f)) : 0f;
                    Vector3 spot = from + direction * (along + Random.Range(-0.3f, 0.3f)) + side * (across + Random.Range(-0.4f, 0.4f));
                    bushes.Add(PlaceContaminatedBush(front.transform, templates, spot, peak));
                }
            }

            return Finish(front, colliders, Dimension.None);
        }

        /// <summary>center를 둘러싼 덤불 고리를 세운다. dimension에서만 막는다.</summary>
        public static DimensionObstacle BuildRing(Transform parent, string name, Vector3 center, float radius,
                                                  Dimension dimension)
        {
            GameObject ring = new GameObject(name);
            ring.transform.SetParent(parent, false);
            Transform colliders = NewChild(ring.transform, "Colliders");
            float thickness = Tuning.ringThickness;

            int segments = Mathf.Max(8, Mathf.CeilToInt(2f * Mathf.PI * radius / 5f));
            float segmentLength = 2f * Mathf.PI * radius / segments * 1.15f;
            for (int i = 0; i < segments; i++)
            {
                float angle = (i + 0.5f) * 360f / segments;
                Vector3 spot = Ground(center + ZoneMap.Direction(angle) * radius);
                Vector3 tangent = ZoneMap.Direction(angle + 90f);
                AddBox(colliders, spot, tangent, thickness, segmentLength);
            }

            List<GameObject> templates = Templates(trees: false);
            int rows = Mathf.Max(1, Tuning.bushRows);
            int count = Mathf.CeilToInt(2f * Mathf.PI * radius / Tuning.bushSpacing);
            for (int i = 0; i < count; i++)
            {
                for (int row = 0; row < rows; row++)
                {
                    float angle = (i + Random.Range(-0.3f, 0.3f)) * 360f / count;
                    float across = rows > 1 ? Mathf.Lerp(-thickness * 0.4f, thickness * 0.4f, row / (rows - 1f)) : 0f;
                    Vector3 spot = center + ZoneMap.Direction(angle) * (radius + across + Random.Range(-0.3f, 0.3f));
                    PlaceContaminatedBush(ring.transform, templates, spot);
                }
            }

            return Finish(ring, colliders, dimension);
        }

        // ── 오염 웅덩이 ──────────────────────────────────────────────

        /// <summary>
        /// center에 오염 웅덩이를 만든다. Fake에서만 막고 Fake에서만 보인다.
        /// 땅의 굴곡을 따라 휘는 원판이라, 비탈에서도 땅에 붙어 있다. 가장자리는 들쭉날쭉하고 희미하게 빛난다.
        /// </summary>
        public static DimensionObstacle BuildPool(Transform parent, Vector3 center, float radius)
        {
            GameObject pool = new GameObject("Contamination Pool");
            pool.transform.SetParent(parent, false);
            Transform colliders = NewChild(pool.transform, "Colliders");

            var mesh = new Mesh { name = "Contamination Pool" };
            const int Spokes = 28;
            const int Rings = 4;
            var vertices = new List<Vector3> { Ground(center) + Vector3.up * 0.08f };
            var colors = new List<Color> { Tuning.poolColor };
            float seed = Random.value * 100f;
            for (int ring = 1; ring <= Rings; ring++)
            {
                float t = ring / (float)Rings;
                for (int spoke = 0; spoke < Spokes; spoke++)
                {
                    float angle = spoke * 360f / Spokes;
                    // 가장자리를 들쭉날쭉하게 한다.
                    float edge = radius * (0.8f + 0.35f * Mathf.PerlinNoise(seed + spoke * 0.35f, seed));
                    Vector3 spot = Ground(center + ZoneMap.Direction(angle) * edge * t) + Vector3.up * 0.08f;
                    vertices.Add(spot);
                    Color color = Color.Lerp(Tuning.poolColor, Tuning.poolRimColor, t * t);
                    color.a = ring == Rings ? 0f : 0.95f;
                    colors.Add(color);
                }
            }

            var triangles = new List<int>();
            for (int spoke = 0; spoke < Spokes; spoke++)
            {
                int next = (spoke + 1) % Spokes;
                triangles.AddRange(new[] { 0, 1 + next, 1 + spoke });
                for (int ring = 1; ring < Rings; ring++)
                {
                    int inner = 1 + (ring - 1) * Spokes;
                    int outer = 1 + ring * Spokes;
                    triangles.AddRange(new[] { inner + spoke, inner + next, outer + spoke });
                    triangles.AddRange(new[] { inner + next, outer + next, outer + spoke });
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            pool.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = pool.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = UnlitMaterial();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // 몸과 길은 원기둥이 막는다. 가장자리의 흐린 부분은 밟을 수 있게 조금 작게 둔다.
            var body = new GameObject("Pool Collider").AddComponent<CapsuleCollider>();
            body.transform.SetParent(colliders, false);
            body.transform.position = Ground(center);
            body.radius = radius * 0.8f;
            body.height = WallHeight;
            body.direction = 1;

            return Finish(pool, colliders, Dimension.Fake);
        }

        // ── 공통 ─────────────────────────────────────────────────────

        /// <summary>뿌리, 웅덩이처럼 빛을 받지 않고 정점 색으로 그리는 재질. (빌드에도 늘 들어가는 셰이더를 쓴다)</summary>
        public static Material UnlitMaterial()
        {
            if (_unlit == null)
                _unlit = new Material(Shader.Find("Sprites/Default")) { name = "Contamination (Unlit)" };
            return _unlit;
        }
        private static Material _unlit;

        /// <summary>그 자리의 땅 높이로 올린 자리.</summary>
        public static Vector3 Ground(Vector3 spot)
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain != null)
                spot.y = terrain.SampleHeight(spot) + terrain.transform.position.y;
            return spot;
        }

        /// <summary>root에 이름표와 차원별 장애물을 달아 마무리한다. colliders 아래의 Collider가 몸과 길을 막는다.</summary>
        public static DimensionObstacle Finish(GameObject root, Transform colliders, Dimension dimension)
        {
            root.AddComponent<DimensionMember>().Setup(dimension);
            DimensionObstacle obstacle = root.AddComponent<DimensionObstacle>();
            obstacle.Setup(colliders);
            return obstacle;
        }

        private static Transform NewChild(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static void AddBox(Transform parent, Vector3 groundSpot, Vector3 direction, float thickness, float length)
            => MakeBox(parent, groundSpot, direction, thickness, length);

        private static BoxCollider MakeBox(Transform parent, Vector3 groundSpot, Vector3 direction, float thickness, float length)
        {
            var box = new GameObject("Segment").AddComponent<BoxCollider>();
            box.transform.SetParent(parent, false);
            box.transform.SetPositionAndRotation(groundSpot, Quaternion.LookRotation(direction, Vector3.up));
            box.size = new Vector3(thickness, WallHeight, length);
            return box;
        }

        // from에서 to까지 비탈을 따라 상자를 잇는다.
        // blocksSight면 같은 자리에 장애물 레이어의 상자를 하나 더 둔다. 적의 시선만 이 레이어를 보고,
        // 던진 돌은 원래 레이어의 상자에 부딪히므로 벽을 넘어가지 않는다.
        private static void AddWallBoxes(Transform colliders, Vector3 from, Vector3 to,
                                         out float length, out Vector3 direction, bool blocksSight)
        {
            Vector3 flat = to - from;
            flat.y = 0f;
            length = flat.magnitude;
            direction = length > 0.001f ? flat / length : Vector3.forward;
            float thickness = Tuning.zoneWallThickness;

            int segments = Mathf.Max(1, Mathf.CeilToInt(length / SegmentLength));
            for (int i = 0; i < segments; i++)
            {
                float start = i * length / segments;
                float end = (i + 1) * length / segments;
                Vector3 middle = Ground(from + direction * ((start + end) * 0.5f));
                AddBox(colliders, middle, direction, thickness, end - start + 0.5f);
                if (blocksSight)
                    MakeBox(colliders, middle, direction, thickness, end - start + 0.5f).gameObject.layer = SightBlockingLayer;
            }
        }

        /// <summary>
        /// target의 재질에 오염 텍스처를 덧입힌다. 원래 재질을 복사해 셰이더만 바꾸므로 모양과 잎의 오림은 그대로다.
        /// opaque: 바위처럼 오려 낼 것이 없는 모델이면 true.
        /// </summary>
        public static void ApplyGrime(GameObject target, string textureName, float amount, Color tint, bool opaque)
        {
            foreach (Renderer part in target.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = part.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = GrimeMaterial(materials[i], textureName, amount, tint, opaque);
                part.sharedMaterials = materials;
            }
        }

        private static Material GrimeMaterial(Material original, string textureName, float amount, Color tint, bool opaque)
        {
            Shader shader = Shader.Find(GrimeShaderName);
            Texture2D texture = Resources.Load<Texture2D>(GrimeTextureFolder + textureName);
            if (original == null || shader == null || texture == null)
                return original;

            if (!_grimeMaterials.TryGetValue((original, textureName), out Material material) || material == null)
            {
                material = new Material(original) { shader = shader, name = original.name + " (Contaminated)" };
                _grimeMaterials[(original, textureName)] = material;
            }

            material.SetTexture(GrimeTexId, texture);
            material.SetFloat(GrimeSizeId, Tuning.grimeSize);
            material.SetFloat(GrimeAmountId, amount);
            material.SetColor("_GrimeTint", tint);
            if (opaque)
                material.SetFloat(CutoffId, 0f);
            return material;
        }

        // 숲에서 복제해 올 나무 또는 풀. 숲은 "Forest Props > 무리 > 나무와 풀 하나하나"로 되어 있다.
        // 나무는 기둥 Collider가 있고, 풀과 덤불은 없다.
        private static List<GameObject> Templates(bool trees)
        {
            var templates = new List<GameObject>();
            GameObject props = GameObject.Find(ForestPropsName);
            if (props == null)
                return templates;

            foreach (Transform group in props.transform)
            {
                int taken = 0;
                foreach (Transform prop in group)
                {
                    bool isTree = prop.GetComponentInChildren<Collider>(true) != null;
                    if (isTree != trees)
                        continue;
                    templates.Add(prop.gameObject);
                    // 무리마다 앞쪽 몇 개만 견본으로 쓴다. (모두 모으면 수천 개라 느리다)
                    if (++taken >= 24)
                        break;
                }
            }
            return templates;
        }

        // 숲의 나무 견본 중 키가 크고 잎이 좁게 퍼진 것. 키 큰 쪽 절반에서, 키에 비해 폭이 좁은 쪽 절반을 고른다.
        // 줄기와 잎이 한 모델이라 잎만 뺄 수 없으므로, 처음부터 잎이 적은 나무를 고른다.
        private static List<GameObject> TallestTrees()
        {
            List<GameObject> trees = Templates(trees: true);
            trees.Sort((a, b) => Size(b).y.CompareTo(Size(a).y));
            KeepFirstHalf(trees);
            trees.Sort((a, b) => Slimness(b).CompareTo(Slimness(a)));
            KeepFirstHalf(trees);
            return trees;
        }

        private static void KeepFirstHalf(List<GameObject> list)
        {
            if (list.Count > 1)
                list.RemoveRange(list.Count / 2, list.Count - list.Count / 2);
        }

        // 키에 비해 얼마나 좁은가. 클수록 잎이 좁게 퍼져 있다.
        private static float Slimness(GameObject target)
        {
            Vector3 size = Size(target);
            return size.y / Mathf.Max(size.x, size.z, 0.001f);
        }

        private static Vector3 Size(GameObject target)
        {
            Vector3 size = Vector3.zero;
            foreach (Renderer part in target.GetComponentsInChildren<Renderer>(true))
                size = Vector3.Max(size, part.bounds.size);
            return size;
        }

        // 오염 덤불 하나를 놓는다: 검은 색과 오염 텍스처를 입힌 덤불.
        private static GameObject PlaceContaminatedBush(Transform parent, List<GameObject> templates, Vector3 spot, float scale = 1f)
        {
            GameObject bush = PlaceBush(parent, templates, spot, Tuning.contaminatedBushTint,
                                        Tuning.bushScaleMin * scale, Tuning.bushScaleMax * scale);
            ApplyGrime(bush, BushTexture, Tuning.bushGrimeAmount, Color.white, opaque: false);
            return bush;
        }

        // 견본 하나를 복제해 spot에 놓는다. 견본이 없으면 어두운 덩어리로 대신한다.
        // fades: 플레이어를 가리면 동그랗게 창이 뚫리게 할지
        private static GameObject PlaceBush(Transform parent, List<GameObject> templates, Vector3 spot, Color tint,
                                            float scaleMin, float scaleMax, bool fades = true)
        {
            GameObject bush;
            Vector3 scale = Vector3.one;
            if (templates.Count > 0)
            {
                GameObject template = templates[Random.Range(0, templates.Count)];
                bush = Object.Instantiate(template, parent);
                // 견본이 크기를 키운 부모 아래에 있을 수 있으므로, 부모의 배율까지 곱한 실제 크기로 맞춘다.
                scale = template.transform.lossyScale;
                bush.SetActive(true);
                // 몸과 길은 장벽의 상자가 막는다. 나무의 기둥 Collider가 남으면 두 차원 모두에서 막히므로 지운다.
                foreach (Collider part in bush.GetComponentsInChildren<Collider>(true))
                    Object.Destroy(part);
            }
            else
            {
                bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.Destroy(bush.GetComponent<Collider>());
                bush.transform.SetParent(parent, false);
                scale = new Vector3(2.5f, 1.8f, 2.5f);
            }

            bush.name = "Bush";
            bush.transform.position = Ground(spot);
            bush.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            bush.transform.localScale = scale * Random.Range(scaleMin, scaleMax);
            // 플레이어를 가리면 숲의 나무처럼 동그랗게 창이 뚫리게 한다.
            if (fades)
                FoliageFader.Register(bush);

            var block = new MaterialPropertyBlock();
            foreach (Renderer part in bush.GetComponentsInChildren<Renderer>(true))
            {
                part.GetPropertyBlock(block);
                block.SetColor(ColorId, tint);
                part.SetPropertyBlock(block);
            }
            return bush;
        }
    }
}
