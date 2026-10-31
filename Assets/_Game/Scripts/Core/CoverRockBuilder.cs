using System.Collections.Generic;
using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 숨을 곳이 되는 작은 바위 무리를 세운다. StageLayout이 관문과 모닥불 고리 가까이에 놓는다.
    ///
    /// 바위는 두 차원 모두에서 몸과 길을 막고, 적의 시선도 가린다(장애물 레이어).
    /// 수색 지도가 "가려진 곳"을 고를 때도 이 바위 뒤가 후보가 된다.
    /// Fake에서는 같은 자리에서 그을린 색으로 보인다(DimensionTint).
    ///
    /// 돌 모델은 StageLayout에 넣어 둔 돌(미끼) 모델을 키워서 쓰고, 표면에는 이끼 낀 바위 텍스처를 덧입힌다.
    /// 숲의 나무와 달리 플레이어를 가려도 창을 뚫지 않는다. 뒤에 숨으면 정말 가려지는 단단한 엄폐물로 보이게 하려는 것이다.
    /// </summary>
    public static class CoverRockBuilder
    {
        /// 바위 무리 하나가 차지하는 반지름(m). 다른 것을 이 안에 놓지 않는다.
        public const float FootprintRadius = 5f;

        private const string RockTexture = "mossy_rock";

        /// <summary>spot에 바위 2~3개로 된 무리를 세운다.</summary>
        public static void Build(Transform parent, Vector3 spot, GameObject[] stoneModels)
        {
            MapTuning tuning = GameTuning.Instance.map;
            var root = new GameObject("Cover Rocks");
            root.transform.SetParent(parent, false);
            var colliders = new GameObject("Colliders").transform;
            colliders.SetParent(root.transform, false);

            int count = Random.Range(2, 4);
            float startAngle = Random.Range(0f, 360f);
            for (int i = 0; i < count; i++)
            {
                Vector3 at = spot + ZoneMap.Direction(startAngle + i * 360f / count) * Random.Range(1.2f, 2.2f);
                float width = Random.Range(tuning.rockWidthMin, tuning.rockWidthMax);
                float height = Random.Range(tuning.rockHeightMin, tuning.rockHeightMax);
                GameObject rock = MakeRock(root.transform, stoneModels, at, width, height);
                BarrierBuilder.ApplyGrime(rock, RockTexture, tuning.rockGrimeAmount, Color.white, opaque: true);
                AddCollider(colliders, at, width, height);
            }

            BarrierBuilder.Finish(root, colliders, Dimension.None);
            root.AddComponent<DimensionTint>().Setup(Color.white, tuning.rockCharred);
        }

        // 돌 모델 하나를 width × height 크기로 맞춰 세운다. 땅에 조금 묻어서 떠 보이지 않게 한다.
        private static GameObject MakeRock(Transform parent, GameObject[] models, Vector3 at, float width, float height)
        {
            GameObject model = models != null && models.Length > 0 ? models[Random.Range(0, models.Length)] : null;
            GameObject rock;
            if (model != null)
            {
                rock = Object.Instantiate(model, parent);
                foreach (Collider part in rock.GetComponentsInChildren<Collider>(true))
                    Object.Destroy(part);
            }
            else
            {
                rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(rock.GetComponent<Collider>());
                rock.transform.SetParent(parent, false);
            }
            rock.name = "Rock";

            // 지금 크기를 재서 원하는 크기가 되도록 축마다 늘인다.
            rock.transform.rotation = Quaternion.identity;
            Vector3 size = RendererBounds(rock).size;
            Vector3 scale = rock.transform.localScale;
            float across = Mathf.Max(size.x, size.z, 0.001f);
            scale.x *= width / across;
            scale.z *= width / across;
            scale.y *= height / Mathf.Max(size.y, 0.001f);
            rock.transform.localScale = scale;
            rock.transform.rotation = Quaternion.Euler(Random.Range(-6f, 6f), Random.Range(0f, 360f), 0f);

            // 바닥이 땅보다 조금 아래에 오도록 내린다.
            Vector3 ground = BarrierBuilder.Ground(at);
            rock.transform.position = ground;
            float bottom = RendererBounds(rock).min.y;
            rock.transform.position += Vector3.up * (ground.y - bottom - 0.4f);
            return rock;
        }

        private static void AddCollider(Transform parent, Vector3 at, float width, float height)
        {
            var body = new GameObject("Rock Collider").AddComponent<CapsuleCollider>();
            body.transform.SetParent(parent, false);
            body.transform.position = BarrierBuilder.Ground(at) + Vector3.up * (height * 0.5f);
            body.radius = width * 0.5f;
            body.height = Mathf.Max(height, width);
            body.direction = 1;
            body.gameObject.layer = BarrierBuilder.SightBlockingLayer;
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
    }
}
