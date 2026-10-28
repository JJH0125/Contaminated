using System.Collections.Generic;
using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 꺼진 모닥불에서 출구까지 땅을 따라 뻗은 검은 뿌리. Fake에서만 보인다.
    ///
    /// 출구는 이 뿌리들에 봉인되어 있다. 모닥불을 켜면 그 불에서 뻗은 뿌리가 타서 사라지고,
    /// 정해 둔 수만큼 뿌리를 태우면 출구가 열린다.
    /// Real에서는 멀리서 연기로 불의 자리를 알 수 있고, Fake에서는 뿌리로 "어느 불이 출구와 이어져 있는지"와
    /// 그 불까지 가는 길을 알 수 있다. 뿌리는 Fake의 길찾기 격자로 길을 내므로 Fake의 장애물을 피해 돌아간다.
    ///
    /// 어둠 속에서도 보이도록, 희미한 빛이 불에서 출구 쪽으로 뿌리를 따라 흐른다.
    ///
    /// 씬 구성:
    ///   붙일 필요가 없다. StageLayout이 모닥불을 놓을 때 만든다.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class RootLine : MonoBehaviour
    {
        // 빛이 뿌리 한 끝에서 다른 끝까지 흐르는 데 걸리는 시간(초)
        private const float PulsePeriod = 2.5f;
        // 흐르는 빛 띠의 길이 (뿌리 전체 길이에 대한 비율)
        private const float PulseLength = 0.08f;
        // 탈 때의 색
        private static readonly Color EmberColor = new Color(1f, 0.4f, 0.1f);

        private LineRenderer _line;
        private float _burn = -1f;
        private float _offset;

        /// <summary>
        /// 땅을 따라 points를 잇는 뿌리를 만든다. points는 불에서 출구 쪽으로 늘어선 자리다.
        /// 길찾기 경로는 칸을 따라 꺾이므로 부드럽게 다듬고, 좌우로 조금씩 구불거리게 한다.
        /// </summary>
        public static RootLine Build(Transform parent, List<Vector3> points)
        {
            MapTuning tuning = GameTuning.Instance.map;
            var root = new GameObject("Root");
            root.transform.SetParent(parent, false);

            List<Vector3> smooth = Smooth(points, tuning.rootWiggle);
            var line = root.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = smooth.Count;
            line.SetPositions(smooth.ToArray());
            line.sharedMaterial = BarrierBuilder.UnlitMaterial();
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.TransformZ;
            // 땅에 눕혀 그린다. (카메라를 향해 세우면 쿼터뷰에서 뿌리가 벽처럼 서 보인다)
            root.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            // 불 쪽이 굵고 출구 쪽으로 갈수록 가늘어진다.
            line.widthCurve = new AnimationCurve(new Keyframe(0f, 1.6f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0.6f));
            line.widthMultiplier = tuning.rootWidth;
            line.numCornerVertices = 2;

            RootLine rootLine = root.AddComponent<RootLine>();
            rootLine._line = line;
            rootLine._offset = Random.value;
            root.AddComponent<DimensionMember>().Setup(Dimension.Fake);
            return rootLine;
        }

        /// <summary>불이 붙었을 때 부른다. 뿌리가 불씨 색으로 타오른 뒤 사라진다.</summary>
        public void Burn()
        {
            if (_burn < 0f)
                _burn = 0f;
        }

        private void Awake() => _line = GetComponent<LineRenderer>();

        private void Update()
        {
            MapTuning tuning = GameTuning.Instance.map;

            if (_burn >= 0f)
            {
                _burn += Time.deltaTime / Mathf.Max(tuning.burnTime, 0.01f);
                // 불에서 출구 쪽으로 타 들어간다: 탄 부분은 사라지고, 타는 경계는 불씨 색이다.
                float front = Mathf.Clamp01(_burn);
                _line.colorGradient = Gradient(
                    (0f, Color.clear), (Mathf.Max(0f, front - 0.001f), Color.clear),
                    (front, EmberColor), (Mathf.Min(1f, front + 0.06f), tuning.rootColor), (1f, tuning.rootColor));
                if (_burn >= 1f)
                    Destroy(gameObject);
                return;
            }

            // 빛 한 줄기가 불에서 출구 쪽으로 흐른다.
            float pulse = Mathf.Repeat(Time.time / PulsePeriod + _offset, 1f);
            Color dark = tuning.rootColor;
            Color glow = tuning.rootPulseColor;
            _line.colorGradient = Gradient(
                (0f, dark),
                (Mathf.Clamp01(pulse - PulseLength), dark),
                (pulse, glow),
                (Mathf.Clamp01(pulse + PulseLength), dark),
                (1f, dark));
        }

        // 색 열쇠 여러 개로 된 그라디언트. (LineRenderer는 열쇠를 8개까지 받는다)
        private static Gradient Gradient(params (float time, Color color)[] keys)
        {
            var colors = new GradientColorKey[keys.Length];
            var alphas = new GradientAlphaKey[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                colors[i] = new GradientColorKey(keys[i].color, keys[i].time);
                alphas[i] = new GradientAlphaKey(keys[i].color.a, keys[i].time);
            }
            var gradient = new Gradient();
            gradient.SetKeys(colors, alphas);
            return gradient;
        }

        // 꺾인 경로를 부드럽게 다듬고, 좌우로 구불거리게 한 뒤 땅 높이로 붙인다.
        private static List<Vector3> Smooth(List<Vector3> points, float wiggle)
        {
            // 1. 칸마다 꺾이는 점을 솎아 낸다 (3칸마다 하나, 끝점은 남긴다)
            var sparse = new List<Vector3>();
            for (int i = 0; i < points.Count; i += 3)
                sparse.Add(points[i]);
            if (points.Count > 0 && sparse[sparse.Count - 1] != points[points.Count - 1])
                sparse.Add(points[points.Count - 1]);

            // 2. 모서리를 깎아 부드럽게 한다 (Chaikin, 두 번)
            for (int pass = 0; pass < 2 && sparse.Count > 2; pass++)
            {
                var next = new List<Vector3> { sparse[0] };
                for (int i = 0; i < sparse.Count - 1; i++)
                {
                    next.Add(Vector3.Lerp(sparse[i], sparse[i + 1], 0.25f));
                    next.Add(Vector3.Lerp(sparse[i], sparse[i + 1], 0.75f));
                }
                next.Add(sparse[sparse.Count - 1]);
                sparse = next;
            }

            // 3. 진행 방향의 옆으로 조금씩 흔들고, 땅에 붙인다.
            float seed = Random.value * 100f;
            var result = new List<Vector3>(sparse.Count);
            for (int i = 0; i < sparse.Count; i++)
            {
                Vector3 ahead = sparse[Mathf.Min(i + 1, sparse.Count - 1)] - sparse[Mathf.Max(i - 1, 0)];
                ahead.y = 0f;
                Vector3 side = ahead.sqrMagnitude > 0.0001f ? Vector3.Cross(Vector3.up, ahead.normalized) : Vector3.zero;
                // 끝점(불과 출구)은 흔들지 않는다.
                float edge = Mathf.Min(i, sparse.Count - 1 - i) / 3f;
                float sway = (Mathf.PerlinNoise(seed, i * 0.25f) - 0.5f) * 2f * wiggle * Mathf.Clamp01(edge);
                result.Add(BarrierBuilder.Ground(sparse[i] + side * sway) + Vector3.up * 0.15f);
            }
            return result;
        }
    }
}
