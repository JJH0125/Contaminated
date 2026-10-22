using UnityEngine;
using UnityEngine.SceneManagement;

namespace Squad
{
    /// <summary>
    /// 맵의 경계(지형의 네 가장자리)에 일렁이는 벽을 세워서, 어디까지 갈 수 있는지 한눈에 보이게 한다.
    /// (배틀그라운드의 자기장 벽 같은 효과)
    ///
    /// 보이기만 하는 벽이다. 몸을 막는 일은 지형에 둘러 둔 보이지 않는 경계 벽(Boundary Walls)이 한다.
    /// 그리는 일은 BoundaryShimmer 셰이더가 하고, 이 스크립트는 벽 메시를 만들고 플레이어 위치를 셰이더에 알려 준다.
    /// 셰이더가 그 위치를 보고, 플레이어에게 가까운 부분일수록 벽을 또렷하게 그린다.
    ///
    /// 씬 구성:
    ///   붙일 필요가 없다. 씬이 열릴 때마다 지형을 찾아서 스스로 만들어진다.
    ///   만들어진 "Boundary Shimmer" 오브젝트를 플레이 중에 선택하면 값을 조절할 수 있다.
    /// </summary>
    public class BoundaryShimmer : MonoBehaviour
    {
        // 수치는 GameTuning 에셋(Resources/GameTuning)에서 읽는다.
        private Color color => GameTuning.Instance.boundary.color;
        private float glow => GameTuning.Instance.boundary.glow;
        private float distortion => GameTuning.Instance.boundary.distortion;
        private float nearDistance => GameTuning.Instance.boundary.nearDistance;
        private float farStrength => GameTuning.Instance.boundary.farStrength;

        // 지형 바닥에서 잰 벽의 높이(m). 거대한 적(약 14m)보다 넉넉히 높게 한다.
        private const float WallHeight = 30f;
        // 굴곡진 땅에서도 벽 아래에 틈이 보이지 않도록 바닥보다 이만큼 아래부터 세운다(m).
        private const float WallSink = 2f;
        private const string ShaderName = "Hidden/Custom/BoundaryShimmer";

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int HeightId = Shader.PropertyToID("_Height");
        private static readonly int GlowId = Shader.PropertyToID("_Glow");
        private static readonly int DistortionId = Shader.PropertyToID("_Distortion");
        private static readonly int NearDistanceId = Shader.PropertyToID("_NearDistance");
        private static readonly int FarStrengthId = Shader.PropertyToID("_FarStrength");
        private static readonly int PlayerPosId = Shader.PropertyToID("_PlayerPos");

        private Material _material;

        // 씬이 열릴 때마다 지형의 경계에 벽을 세운다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            Create();
            SceneManager.sceneLoaded += (_, _) => Create();
        }

        private static void Create()
        {
            Terrain terrain = Terrain.activeTerrain;
            Shader shader = Shader.Find(ShaderName);
            if (terrain == null || shader == null || FindObjectOfType<BoundaryShimmer>() != null)
                return;

            var wall = new GameObject("Boundary Shimmer");
            wall.AddComponent<MeshFilter>().sharedMesh = BuildMesh(terrain);
            var wallRenderer = wall.AddComponent<MeshRenderer>();
            wallRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            wallRenderer.receiveShadows = false;

            BoundaryShimmer shimmer = wall.AddComponent<BoundaryShimmer>();
            shimmer._material = new Material(shader);
            wallRenderer.sharedMaterial = shimmer._material;
        }

        /// <summary>
        /// 지형의 네 가장자리를 따라 세운 벽 네 장의 메시. 꼭짓점은 월드 좌표다.
        /// UV는 미터 단위로 넣는다. (u: 경계를 따라 한 바퀴 돈 거리, v: 지형 바닥에서 잰 높이)
        /// </summary>
        private static Mesh BuildMesh(Terrain terrain)
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            Vector3[] corners =
            {
                new Vector3(origin.x, 0f, origin.z),
                new Vector3(origin.x + size.x, 0f, origin.z),
                new Vector3(origin.x + size.x, 0f, origin.z + size.z),
                new Vector3(origin.x, 0f, origin.z + size.z),
            };

            float bottom = origin.y - WallSink;
            float top = origin.y + WallHeight;
            var vertices = new Vector3[16];
            var uvs = new Vector2[16];
            var triangles = new int[24];
            float along = 0f;

            for (int side = 0; side < 4; side++)
            {
                Vector3 from = corners[side];
                Vector3 to = corners[(side + 1) % 4];
                float length = Vector3.Distance(from, to);
                int v = side * 4;

                vertices[v] = new Vector3(from.x, bottom, from.z);
                vertices[v + 1] = new Vector3(to.x, bottom, to.z);
                vertices[v + 2] = new Vector3(from.x, top, from.z);
                vertices[v + 3] = new Vector3(to.x, top, to.z);
                uvs[v] = new Vector2(along, -WallSink);
                uvs[v + 1] = new Vector2(along + length, -WallSink);
                uvs[v + 2] = new Vector2(along, WallHeight);
                uvs[v + 3] = new Vector2(along + length, WallHeight);
                along += length;

                // 셰이더가 양면을 다 그리므로(Cull Off) 삼각형의 방향은 상관없다.
                int t = side * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 2;
                triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1;
                triangles[t + 4] = v + 2;
                triangles[t + 5] = v + 3;
            }

            var mesh = new Mesh { name = "Boundary Shimmer" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private void Update()
        {
            if (_material == null)
                return;

            // 값을 바꾸면 플레이 중에도 바로 보이도록 매 프레임 넣는다.
            _material.SetColor(ColorId, color);
            _material.SetFloat(HeightId, WallHeight);
            _material.SetFloat(GlowId, glow);
            _material.SetFloat(DistortionId, distortion);
            _material.SetFloat(NearDistanceId, nearDistance);
            _material.SetFloat(FarStrengthId, farStrength);

            DimensionController controller = DimensionController.Instance;
            if (controller != null && controller.player != null)
                _material.SetVector(PlayerPosId, controller.player.transform.position);
        }

        private void OnDestroy()
        {
            if (_material != null)
                Destroy(_material);
        }
    }
}
