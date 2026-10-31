using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 소리가 날 때 바닥에 퍼져나가는 원을 그려 소리 반경을 보여준다.
    ///
    /// 판정(누가 들었나)과는 무관한 "보이는 것" 전용 컴포넌트다.
    /// SoundEmitter.Emit이 소리를 낼 때마다 Show를 호출하고,
    /// 원은 0에서 소리 반경까지 퍼지면서 흐려진 뒤 사라진다.
    ///
    /// 발소리는 짧은 간격으로 계속 나므로 원을 매번 만들고 지우지 않고,
    /// 미리 만들어 둔 원들을 돌려 쓴다(풀링).
    ///
    /// 씬 구성:
    ///   빈 오브젝트에 이 스크립트를 붙인다. 씬에 하나만 둔다.
    ///   녹화나 전시에서 끄고 싶으면 이 컴포넌트를 비활성화하면 된다.
    /// </summary>
    public class SoundRingDisplay : MonoBehaviour
    {
        public static SoundRingDisplay Instance { get; private set; }

        [Header("○ 모양")]
        [Tooltip("원이 반경까지 퍼지는 데 걸리는 시간(초)")]
        [SerializeField] private float duration = 0.6f;
        [Tooltip("선 두께")]
        [SerializeField] private float lineWidth = 0.06f;
        [Tooltip("원을 이루는 점의 수. 많을수록 매끄럽다")]
        [SerializeField] private int segments = 48;
        [Tooltip("바닥에서 살짝 띄우는 높이 (바닥과 겹쳐 깜빡이는 것 방지)")]
        [SerializeField] private float heightOffset = 0.03f;

        [Header("○ 색")]
        [Tooltip("차원을 넘지 않는 소리 (발소리 등)")]
        [SerializeField] private Color normalColor = new Color(0.34f, 0.77f, 0.86f);   // 청록
        [Tooltip("차원을 넘는 소리 (모닥불에 불붙는 소리 등)")]
        [SerializeField] private Color crossColor = new Color(0.91f, 0.64f, 0.24f);    // 주황

        [Header("○ 바닥 찾기")]
        [Tooltip("소리 위치에서 아래로 바닥을 찾을 레이어. 바닥이 속한 레이어를 지정")]
        [SerializeField] private LayerMask groundMask = ~0;
        [Tooltip("바닥을 찾을 최대 거리")]
        [SerializeField] private float groundSearchDistance = 5f;

        [Header("○ 기타")]
        [Tooltip("동시에 보일 수 있는 원의 최대 개수")]
        [SerializeField] private int poolSize = 12;
        [Tooltip("비워두면 조명과 무관하게 보이는 기본 머티리얼(Sprites/Default)을 쓴다")]
        [SerializeField] private Material ringMaterial;

        // 원 하나의 상태
        private class Ring
        {
            public LineRenderer line;
            public Vector3[] points;
            public Vector3 center;
            public float maxRadius;
            public Color color;
            public float elapsed;
            public bool active;
        }

        private Ring[] _rings;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            // 어두운 맵에서도 보여야 하므로 조명의 영향을 받지 않는 머티리얼을 쓴다.
            if (ringMaterial == null)
                ringMaterial = new Material(Shader.Find("Sprites/Default"));

            _rings = new Ring[poolSize];
            for (int i = 0; i < poolSize; i++)
                _rings[i] = CreateRing(i);
        }

        private Ring CreateRing(int index)
        {
            var ringObject = new GameObject("SoundRing_" + index);
            ringObject.transform.SetParent(transform, false);

            var line = ringObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = segments;
            line.widthMultiplier = lineWidth;
            line.material = ringMaterial;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;

            return new Ring { line = line, points = new Vector3[segments] };
        }

        /// <summary>
        /// 소리가 난 위치에 반경만큼 퍼지는 원을 띄운다.
        /// </summary>
        public void Show(Vector3 position, Sound sound)
        {
            if (!isActiveAndEnabled || sound == null || _rings == null) return;

            Ring ring = GetFreeRing();

            ring.center = FindGround(position);
            ring.maxRadius = sound.Radius;
            ring.color = sound.CanCrossDimension ? crossColor : normalColor;
            ring.elapsed = 0f;
            ring.active = true;
            ring.line.enabled = true;

            UpdateRing(ring);
        }

        private void Update()
        {
            // 플레이 중에 스크립트가 다시 컴파일되면(파일 이동·수정 등) Awake가 다시 호출되지 않은 채
            // 원 목록(_rings)만 비워진다. 그 상태에서 매 프레임 에러가 나지 않도록 막는다.
            if (_rings == null)
                return;

            foreach (Ring ring in _rings)
            {
                if (!ring.active) continue;

                ring.elapsed += Time.deltaTime;
                if (ring.elapsed >= duration)
                {
                    ring.active = false;
                    ring.line.enabled = false;
                    continue;
                }
                UpdateRing(ring);
            }
        }

        // 경과 시간에 맞춰 원의 크기와 투명도를 갱신한다.
        private void UpdateRing(Ring ring)
        {
            float t = Mathf.Clamp01(ring.elapsed / duration);   // 0 → 1

            // 처음에 빠르게 퍼지고 끝에서 느려지도록 (ease-out)
            float eased = 1f - (1f - t) * (1f - t);
            float radius = ring.maxRadius * eased;

            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                ring.points[i] = ring.center +
                    new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            }
            ring.line.SetPositions(ring.points);

            // 퍼질수록 흐려진다
            Color c = ring.color;
            c.a = 1f - t;
            ring.line.startColor = c;
            ring.line.endColor = c;
        }

        // 쉬고 있는 원을 꺼내고, 모두 사용 중이면 가장 오래된 원을 재사용한다.
        private Ring GetFreeRing()
        {
            Ring oldest = _rings[0];
            foreach (Ring ring in _rings)
            {
                if (!ring.active) return ring;
                if (ring.elapsed > oldest.elapsed) oldest = ring;
            }
            return oldest;
        }

        // 소리 위치는 캐릭터의 중심(허리 높이)일 수 있으므로, 아래로 바닥을 찾아 그 높이에 그린다.
        private Vector3 FindGround(Vector3 position)
        {
            Vector3 origin = position + Vector3.up * 0.5f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                    groundSearchDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                return hit.point + Vector3.up * heightOffset;
            }
            return position + Vector3.up * heightOffset;
        }
    }
}