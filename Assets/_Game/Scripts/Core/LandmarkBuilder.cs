using System.Collections.Generic;
using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 길을 잃지 않게 해 주는 랜드마크를 세운다. 모든 라운드에 같은 자리, 같은 모양이다.
    ///   봉우리 꼭대기  선돌 고리. 맵 어디서든 가운데를 알려 준다
    ///   남(출구 쪽)    높은 첨탑 하나와 작은 돌들
    ///   동            서로 기댄 두 돌 (문 모양)
    ///   서            기울어진 첨탑 무리
    ///   북            작은 선돌 고리와 가운데의 높은 돌
    ///
    /// Real에서는 원래의 돌이고, Fake에서는 같은 자리에서 검게 그을리고(DimensionTint) 검은 덩굴이 감겨 있다(Fake에서만 보임).
    /// 자리가 같으므로 Fake에서 8초 뒤 Real로 튕겨 나와도 위치를 잃지 않는다.
    /// 돌은 두 차원 모두에서 몸과 길을 막고, 적의 시선도 가린다(장애물 레이어). 숨을 곳이 되기도 한다.
    ///
    /// 돌 모델은 StageLayout에 넣어 둔 돌(미끼) 모델을 키우고 늘여서 쓴다. 모델이 없으면 상자로 대신한다.
    /// 모양은 랜드마크마다 정해 둔 시드로 뽑으므로 라운드가 바뀌어도 같다.
    /// </summary>
    public static class LandmarkBuilder
    {
        private enum Kind { Circle, Spire, Gate, Leaning, CircleWithCenter }

        private struct Spec
        {
            public string Name;
            public float Angle;      // 봉우리에서 본 방향(도). 북쪽이 0
            public float Distance;   // 봉우리에서의 거리(m)
            public Kind Kind;
            public int Seed;
        }

        // 남쪽 랜드마크는 출구와 플레이어 시작 자리를 피해 조금 비껴 둔다.
        private static readonly Spec[] Specs =
        {
            new Spec { Name = "Landmark (봉우리)", Angle = 0f, Distance = 0f, Kind = Kind.Circle, Seed = 11 },
            new Spec { Name = "Landmark (남)", Angle = 145f, Distance = 55f, Kind = Kind.Spire, Seed = 23 },
            new Spec { Name = "Landmark (동)", Angle = 90f, Distance = 62f, Kind = Kind.Gate, Seed = 37 },
            new Spec { Name = "Landmark (서)", Angle = -90f, Distance = 62f, Kind = Kind.Leaning, Seed = 41 },
            new Spec { Name = "Landmark (북)", Angle = 0f, Distance = 62f, Kind = Kind.CircleWithCenter, Seed = 53 },
        };

        /// 랜드마크 하나가 차지하는 반지름(m). 다른 것을 이 안에 놓지 않는다.
        public const float FootprintRadius = 10f;

        /// <summary>모든 랜드마크를 세우고, 그 자리들을 돌려준다.</summary>
        public static List<Vector3> BuildAll(Transform parent, Vector3 center, GameObject[] stoneModels)
        {
            var spots = new List<Vector3>();
            foreach (Spec spec in Specs)
            {
                Vector3 spot = center + ZoneMap.Direction(spec.Angle) * spec.Distance;
                Build(parent, spec, spot, stoneModels);
                spots.Add(spot);
            }
            return spots;
        }

        private static void Build(Transform parent, Spec spec, Vector3 spot, GameObject[] stoneModels)
        {
            MapTuning tuning = GameTuning.Instance.map;
            float scale = tuning.landmarkScale;
            var random = new System.Random(spec.Seed);

            var root = new GameObject(spec.Name);
            root.transform.SetParent(parent, false);
            var colliders = new GameObject("Colliders").transform;
            colliders.SetParent(root.transform, false);
            var vines = new GameObject(spec.Name + " Vines");
            vines.transform.SetParent(parent, false);

            void AddStone(Vector3 offset, float width, float height, float tilt)
            {
                Vector3 at = spot + offset * scale;
                float yaw = Range(random, 0f, 360f);
                MakeStone(root.transform, stoneModels, random, at, width * scale, height * scale, yaw, tilt);
                AddCollider(colliders, at, width * scale, height * scale);
                if (height * scale > 4f)
                    AddVine(vines.transform, at, width * scale, height * scale, random, tuning.landmarkVineColor);
            }

            switch (spec.Kind)
            {
                case Kind.Circle:
                    for (int i = 0; i < 7; i++)
                        AddStone(ZoneMap.Direction(i * 360f / 7f) * 6f, Range(random, 1.6f, 2.2f), Range(random, 5f, 7f), Range(random, -4f, 4f));
                    break;
                case Kind.Spire:
                    AddStone(Vector3.zero, 3f, 11f, 0f);
                    for (int i = 0; i < 4; i++)
                        AddStone(ZoneMap.Direction(i * 90f + 30f) * Range(random, 3.5f, 5f), Range(random, 1.2f, 1.8f), Range(random, 1.5f, 3f), Range(random, -10f, 10f));
                    break;
                case Kind.Gate:
                    AddStone(Vector3.left * 2.6f, 2.2f, 9f, 9f);
                    AddStone(Vector3.right * 2.6f, 2.2f, 9f, -9f);
                    AddStone(Vector3.forward * 4f, 1.5f, 2f, 20f);
                    break;
                case Kind.Leaning:
                    for (int i = 0; i < 3; i++)
                        AddStone(ZoneMap.Direction(i * 120f) * 2.5f, Range(random, 1.8f, 2.4f), Range(random, 7f, 9.5f), Range(random, 12f, 20f));
                    break;
                case Kind.CircleWithCenter:
                    AddStone(Vector3.zero, 2.6f, 10f, 0f);
                    for (int i = 0; i < 5; i++)
                        AddStone(ZoneMap.Direction(i * 72f) * 5f, Range(random, 1.2f, 1.6f), Range(random, 3f, 4.5f), Range(random, -6f, 6f));
                    break;
            }

            // 두 차원 모두에서 막는다. Real에서는 원래 색, Fake에서는 그을린 색.
            BarrierBuilder.Finish(root, colliders, Dimension.None);
            root.AddComponent<DimensionTint>().Setup(Color.white, tuning.landmarkCharred);

            // 덩굴은 Fake에서만 보인다.
            vines.AddComponent<DimensionMember>().Setup(Dimension.Fake);
        }

        // 돌 모델 하나를 width × height 크기로 늘여 세운다. 땅에 조금 묻어서 떠 보이지 않게 한다.
        private static Transform MakeStone(Transform parent, GameObject[] models, System.Random random,
                                           Vector3 at, float width, float height, float yaw, float tilt)
        {
            GameObject model = models != null && models.Length > 0 ? models[random.Next(models.Length)] : null;
            GameObject stone;
            if (model != null)
            {
                stone = Object.Instantiate(model, parent);
                foreach (Collider part in stone.GetComponentsInChildren<Collider>(true))
                    Object.Destroy(part);
            }
            else
            {
                stone = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(stone.GetComponent<Collider>());
                stone.transform.SetParent(parent, false);
            }
            stone.name = "Stone";

            // 지금 크기를 재서 원하는 크기가 되도록 축마다 늘인다.
            stone.transform.rotation = Quaternion.identity;
            Bounds bounds = RendererBounds(stone);
            Vector3 size = bounds.size;
            float across = Mathf.Max(size.x, size.z, 0.001f);
            Vector3 scale = stone.transform.localScale;
            scale.x *= width / across;
            scale.z *= width / across;
            scale.y *= height / Mathf.Max(size.y, 0.001f);
            stone.transform.localScale = scale;
            stone.transform.rotation = Quaternion.Euler(tilt, yaw, 0f);

            // 바닥이 땅보다 조금 아래에 오도록 내린다.
            Vector3 ground = BarrierBuilder.Ground(at);
            stone.transform.position = ground;
            float bottom = RendererBounds(stone).min.y;
            stone.transform.position += Vector3.up * (ground.y - bottom - 0.5f);
            return stone.transform;
        }

        private static void AddCollider(Transform parent, Vector3 at, float width, float height)
        {
            var body = new GameObject("Stone Collider").AddComponent<CapsuleCollider>();
            body.transform.SetParent(parent, false);
            body.transform.position = BarrierBuilder.Ground(at) + Vector3.up * (height * 0.5f);
            body.radius = width * 0.5f;
            body.height = Mathf.Max(height, width);
            body.direction = 1;
            // 적의 시선을 가리도록 장애물 레이어에 둔다. (나무 기둥과 같은 레이어)
            int obstacleLayer = LayerMask.NameToLayer("Unwalkable");
            body.gameObject.layer = obstacleLayer >= 0 ? obstacleLayer : 3;
        }

        // 돌을 휘감아 오르는 검은 덩굴과, 돌 밑에서 땅으로 뻗어 나간 덩굴 몇 가닥 (Fake에서만 보인다)
        private static void AddVine(Transform parent, Vector3 at, float width, float height, System.Random random, Color color)
        {
            Vector3 ground = BarrierBuilder.Ground(at);
            float phase = Range(random, 0f, 360f);
            float turns = Range(random, 1.5f, 2.5f);

            var climb = new List<Vector3>();
            const int Steps = 40;
            for (int i = 0; i <= Steps; i++)
            {
                float t = i / (float)Steps;
                float radius = width * 0.55f * (1f - 0.35f * t);
                Vector3 around = ZoneMap.Direction(phase + t * turns * 360f) * radius;
                climb.Add(ground + around + Vector3.up * (t * height * 0.85f));
            }
            AddLine(parent, climb, 0.35f, 0.08f, color, LineAlignment.View);

            int tendrils = random.Next(2, 5);
            for (int n = 0; n < tendrils; n++)
            {
                float angle = Range(random, 0f, 360f);
                float length = Range(random, 4f, 8f);
                var path = new List<Vector3>();
                for (int i = 0; i <= 10; i++)
                {
                    float t = i / 10f;
                    float bend = Mathf.Sin(t * Mathf.PI * 2f + angle) * 0.8f;
                    Vector3 spot = at + ZoneMap.Direction(angle) * (width * 0.5f + t * length) + ZoneMap.Direction(angle + 90f) * bend;
                    path.Add(BarrierBuilder.Ground(spot) + Vector3.up * 0.12f);
                }
                AddLine(parent, path, 0.3f, 0.05f, color, LineAlignment.TransformZ);
            }
        }

        private static void AddLine(Transform parent, List<Vector3> points, float startWidth, float endWidth,
                                    Color color, LineAlignment alignment)
        {
            var line = new GameObject("Vine").AddComponent<LineRenderer>();
            line.transform.SetParent(parent, false);
            if (alignment == LineAlignment.TransformZ)
                line.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // 땅에 눕힌다
            line.alignment = alignment;
            line.useWorldSpace = true;
            line.positionCount = points.Count;
            line.SetPositions(points.ToArray());
            line.startWidth = startWidth;
            line.endWidth = endWidth;
            line.startColor = color;
            line.endColor = color;
            line.numCornerVertices = 2;
            line.sharedMaterial = BarrierBuilder.UnlitMaterial();
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static Bounds RendererBounds(GameObject target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(target.transform.position, Vector3.one);

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer part in renderers)
                bounds.Encapsulate(part.bounds);
            return bounds;
        }

        private static float Range(System.Random random, float min, float max)
            => min + (float)random.NextDouble() * (max - min);
    }
}
