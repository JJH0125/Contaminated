using System.Collections.Generic;
using UnityEngine;

namespace Squad
{
    /// <summary>
    /// Fake 적의 몸. 걷는 생물이 아니라, 사람 윤곽이 희미하게 남은 채 흩어지는 재의 기둥이다.
    ///   기둥   위로 긴 검은 몸. 어깨와 머리의 윤곽만 남았고, 아래로 갈수록 가늘어져 땅 위에 떠 있다 (Custom/Ash Enemy)
    ///   재     몸에서 끊임없이 피어올라 흩어진다
    ///   발밑   땅에 재가 깔리고, 지나간 자리에 잠깐 남았다 사라진다. 어디 있는지, 어디로 갔는지 알려 준다
    ///   결정   머리 앞쪽에 모여 "얼굴 자리"가 된다. 어느 쪽을 보는지 알 수 있다. 몇 개는 몸 둘레를 천천히 돈다.
    ///          횃불 빛이 닿으면 하얗게 빛난다
    ///
    /// 원래의 뼈대 모델은 그리지 않을 뿐 그대로 둔다. 판정과 애니메이터는 지금 그대로 동작한다.
    /// 수치는 GameTuning의 ashEnemy 항목에서 읽는다.
    ///
    /// 씬 구성:
    ///   붙일 필요가 없다. StageLayout이 Fake 적을 놓을 때 붙인다.
    /// </summary>
    public class AshEnemyBody : MonoBehaviour
    {
        private const string CrystalShaderName = "Hidden/Custom/AshCrystal";
        // 기둥의 옆모습: (높이 비율, 폭 비율). 아래는 가늘고, 어깨에서 넓어졌다가 목에서 좁아져 머리가 된다.
        private static readonly Vector2[] Profile =
        {
            new Vector2(0f, 0.04f), new Vector2(0.12f, 0.16f), new Vector2(0.35f, 0.3f), new Vector2(0.55f, 0.36f),
            new Vector2(0.7f, 0.5f), new Vector2(0.76f, 0.42f), new Vector2(0.82f, 0.15f), new Vector2(0.87f, 0.23f),
            new Vector2(0.95f, 0.2f), new Vector2(1f, 0.02f),
        };
        private const int Spokes = 16;

        private static Material _ashMaterial;
        private static Material _crystalMaterial;
        private static Mesh _crystalMesh;

        private Transform _body;
        private Transform _orbit;
        private Vector3 _restPosition;
        private float _phase;

        private void Awake()
        {
            AshEnemyTuning tuning = GameTuning.Instance.ashEnemy;
            _phase = Random.value * 10f;

            // 원래 모델은 숨긴다. 몸의 재질은 Fake 적의 재질(Custom/Ash Enemy)을 그대로 쓴다.
            Material bodyMaterial = null;
            foreach (SkinnedMeshRenderer skin in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                skin.forceRenderingOff = true;
                bodyMaterial = skin.sharedMaterial;
            }

            // 적의 크기 배율과 상관없이 미터 단위로 만들도록, 배율을 지운 자식을 둔다.
            _body = new GameObject("Ash Body").transform;
            _body.SetParent(transform, false);
            Vector3 lossy = transform.lossyScale;
            _body.localScale = new Vector3(1f / lossy.x, 1f / lossy.y, 1f / lossy.z);
            float footOffset = -GameTuning.Instance.fakeEnemy.heightAboveGround;
            _body.localPosition = Vector3.up * (footOffset / Mathf.Max(lossy.y, 0.001f));
            _restPosition = _body.localPosition;

            var column = new GameObject("Column");
            column.transform.SetParent(_body, false);
            column.AddComponent<MeshFilter>().sharedMesh = BuildColumn(tuning.bodyHeight, tuning.bodyWidth);
            column.AddComponent<MeshRenderer>().sharedMaterial = bodyMaterial;

            BuildCrystals(tuning);
            BuildSmoke(tuning);
            BuildGroundAsh(tuning);

            // 플레이어와 다른 차원에 있으면 함께 숨도록 이름표에 알려 준다.
            DimensionMember member = GetComponent<DimensionMember>();
            if (member != null)
                member.AddRenderers(_body.GetComponentsInChildren<Renderer>(true));
        }

        private void Update()
        {
            // 떠 있는 것처럼 천천히 오르내리고, 결정은 몸 둘레를 돈다.
            float time = Time.time + _phase;
            _body.localPosition = _restPosition + Vector3.up * (Mathf.Sin(time * 1.1f) * 0.25f);
            _orbit.Rotate(Vector3.up, 20f * Time.deltaTime, Space.Self);
        }

        // 옆모습(Profile)을 돌려 깎은 기둥. 둘레를 들쭉날쭉하게 해서 매끈한 원기둥으로 보이지 않게 한다.
        private static Mesh BuildColumn(float height, float width)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            float seed = Random.value * 100f;

            for (int ring = 0; ring < Profile.Length; ring++)
            {
                for (int spoke = 0; spoke <= Spokes; spoke++)
                {
                    float angle = spoke % Spokes * 360f / Spokes;
                    float rough = 0.8f + 0.4f * Mathf.PerlinNoise(seed + spoke % Spokes * 0.7f, ring * 0.6f);
                    Vector3 around = ZoneMap.Direction(angle) * (Profile[ring].y * width * rough);
                    around.z *= 0.7f;   // 앞뒤로 납작하게 해서 사람 윤곽에 가깝게 한다
                    vertices.Add(around + Vector3.up * (Profile[ring].x * height));
                    uvs.Add(new Vector2(spoke / (float)Spokes, Profile[ring].x * 3f));
                }
            }

            int stride = Spokes + 1;
            for (int ring = 0; ring < Profile.Length - 1; ring++)
            {
                for (int spoke = 0; spoke < Spokes; spoke++)
                {
                    int a = ring * stride + spoke;
                    int b = a + stride;
                    triangles.AddRange(new[] { a, b, a + 1 });
                    triangles.AddRange(new[] { a + 1, b, b + 1 });
                }
            }

            var mesh = new Mesh { name = "Ash Column" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // 머리 앞쪽의 얼굴 자리와, 몸 둘레를 도는 결정들
        private void BuildCrystals(AshEnemyTuning tuning)
        {
            float height = tuning.bodyHeight;
            float width = tuning.bodyWidth;

            for (int i = 0; i < 6; i++)
            {
                Vector3 spot = new Vector3(Random.Range(-0.1f, 0.1f) * width,
                                           height * Random.Range(0.87f, 0.95f),
                                           width * Random.Range(0.13f, 0.2f));
                AddCrystal(_body, spot, Random.Range(0.25f, 0.55f));
            }

            _orbit = new GameObject("Orbit").transform;
            _orbit.SetParent(_body, false);
            for (int i = 0; i < 6; i++)
            {
                Vector3 around = ZoneMap.Direction(i * 60f) * (width * Random.Range(0.5f, 0.75f));
                AddCrystal(_orbit, around + Vector3.up * (height * Random.Range(0.25f, 0.75f)), Random.Range(0.2f, 0.45f));
            }
        }

        private static void AddCrystal(Transform parent, Vector3 localPosition, float size)
        {
            if (_crystalMaterial == null)
                _crystalMaterial = new Material(Shader.Find(CrystalShaderName));
            if (_crystalMesh == null)
                _crystalMesh = BuildCrystalMesh();

            var crystal = new GameObject("Crystal");
            crystal.transform.SetParent(parent, false);
            crystal.transform.localPosition = localPosition;
            crystal.transform.localRotation = Random.rotation;
            crystal.transform.localScale = new Vector3(size, size * 1.8f, size);
            crystal.AddComponent<MeshFilter>().sharedMesh = _crystalMesh;
            var crystalRenderer = crystal.AddComponent<MeshRenderer>();
            crystalRenderer.sharedMaterial = _crystalMaterial;
            crystalRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // 위아래로 뾰족한 팔면체
        private static Mesh BuildCrystalMesh()
        {
            var mesh = new Mesh { name = "Ash Crystal" };
            mesh.vertices = new[]
            {
                Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back,
            };
            mesh.triangles = new[]
            {
                0, 4, 3,  0, 3, 5,  0, 5, 2,  0, 2, 4,
                1, 3, 4,  1, 5, 3,  1, 2, 5,  1, 4, 2,
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        // 몸에서 피어올라 흩어지는 재
        private void BuildSmoke(AshEnemyTuning tuning)
        {
            ParticleSystem smoke = NewParticles("Ash Smoke", tuning);
            ParticleSystem.MainModule main = smoke.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
            main.maxParticles = 150;

            ParticleSystem.EmissionModule emission = smoke.emission;
            emission.rateOverTime = tuning.smokeRate;

            ParticleSystem.ShapeModule shape = smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(tuning.bodyWidth * 0.55f, tuning.bodyWidth * 0.4f, tuning.bodyHeight * 0.85f);
            smoke.transform.localPosition = Vector3.up * (tuning.bodyHeight * 0.5f);
            // 파티클의 앞(Z)이 위를 향하게 눕혀 둔다.
            smoke.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            ParticleSystem.NoiseModule noise = smoke.noise;
            noise.enabled = true;
            noise.strength = 1f;
            noise.frequency = 0.4f;

            FadeInOut(smoke, 0.2f);
        }

        // 발밑에 깔리고, 지나간 자리에 잠깐 남는 재. 땅에 머물게 월드 좌표로 뿌린다.
        private void BuildGroundAsh(AshEnemyTuning tuning)
        {
            ParticleSystem ash = NewParticles("Ground Ash", tuning);
            ParticleSystem.MainModule main = ash.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(tuning.trailTime * 0.6f, tuning.trailTime);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 3f);
            main.maxParticles = 120;

            ParticleSystem.EmissionModule emission = ash.emission;
            emission.rateOverTime = tuning.trailRate;

            ParticleSystem.ShapeModule shape = ash.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = tuning.bodyWidth * 0.7f;
            ash.transform.localPosition = Vector3.up * 0.3f;
            ash.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            FadeInOut(ash, 0.1f);
        }

        // 나타나고 사라질 때 뚝 끊기지 않게 한다.
        private static void FadeInOut(ParticleSystem particles, float fadeIn)
        {
            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn), new GradientAlphaKey(0f, 1f) });
            color.color = fade;
        }

        private ParticleSystem NewParticles(string particleName, AshEnemyTuning tuning)
        {
            var particleObject = new GameObject(particleName);
            particleObject.transform.SetParent(_body, false);
            var particles = particleObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = tuning.ashColor;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            var particleRenderer = particleObject.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = AshMaterial();
            particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            particles.Play();
            return particles;
        }

        // 부드러운 원 모양의 재 알갱이. 빛을 더하지 않고 어둡게 덮어야 하므로 알파 혼합 셰이더를 쓴다.
        private static Material AshMaterial()
        {
            if (_ashMaterial != null)
                return _ashMaterial;

            const int Size = 64;
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "Ash Puff" };
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = (x + 0.5f) / Size * 2f - 1f;
                    float dy = (y + 0.5f) / Size * 2f - 1f;
                    float edge = 1f - Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float grain = 0.6f + 0.4f * Mathf.PerlinNoise(x * 0.25f, y * 0.25f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, edge * edge * grain));
                }
            }
            texture.Apply();

            _ashMaterial = new Material(Shader.Find("Sprites/Default")) { name = "Ash Puff", mainTexture = texture };
            return _ashMaterial;
        }
    }
}
