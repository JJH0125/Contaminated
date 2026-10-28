using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 덤불이 타서 사라지는 모습. 불씨 색으로 달아올랐다가 검게 그을리며 오그라든 뒤 오브젝트째 사라진다.
    /// 막는 것은 이것과 상관없이 DimensionObstacle.Open이 그 순간 바로 푼다. 이 스크립트는 모습만 맡는다.
    ///
    /// 씬 구성:
    ///   붙일 필요가 없다. 모닥불이 켜질 때 덤불 고리에 붙인다. (Play)
    /// </summary>
    public class BurnAway : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Color Ember = new Color(1f, 0.35f, 0.08f);
        private static readonly Color Char = new Color(0.05f, 0.04f, 0.04f);

        private float _duration;
        private float _time;
        private Renderer[] _renderers;
        private Transform[] _parts;
        private Vector3[] _scales;
        private MaterialPropertyBlock _block;

        /// <summary>target을 duration초 동안 태워 없앤다.</summary>
        public static void Play(GameObject target, float duration)
        {
            if (target == null || target.GetComponent<BurnAway>() != null)
                return;

            BurnAway burn = target.AddComponent<BurnAway>();
            burn._duration = Mathf.Max(duration, 0.01f);
        }

        private void Start()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _block = new MaterialPropertyBlock();

            // 덤불 하나하나(바로 아래 자식)가 따로 오그라든다.
            _parts = new Transform[transform.childCount];
            _scales = new Vector3[transform.childCount];
            for (int i = 0; i < transform.childCount; i++)
            {
                _parts[i] = transform.GetChild(i);
                _scales[i] = _parts[i].localScale;
            }
        }

        private void Update()
        {
            _time += Time.deltaTime;
            float t = Mathf.Clamp01(_time / _duration);

            // 앞 절반은 불씨 색으로 달아오르고, 뒤 절반은 그을리며 오그라든다.
            Color color = t < 0.5f ? Color.Lerp(Color.white, Ember, t * 2f) : Color.Lerp(Ember, Char, (t - 0.5f) * 2f);
            foreach (Renderer part in _renderers)
            {
                if (part == null)
                    continue;
                // Real에서만 보이던 덤불도, 타는 동안은 어느 차원에서든 보인다.
                // (불은 보통 Fake에서 켜므로, 켠 자리에서 덤불이 타오르는 모습을 볼 수 있게 한다)
                part.enabled = true;
                part.GetPropertyBlock(_block);
                _block.SetColor(ColorId, color);
                part.SetPropertyBlock(_block);
            }

            float shrink = t < 0.5f ? 1f : 1f - (t - 0.5f) * 2f;
            for (int i = 0; i < _parts.Length; i++)
                if (_parts[i] != null)
                    _parts[i].localScale = _scales[i] * Mathf.Max(shrink, 0.001f);

            if (t >= 1f)
                Destroy(gameObject);
        }
    }
}
