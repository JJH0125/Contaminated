using System.Collections.Generic;
using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 땅에 떨어져 있는 돌. 플레이어가 주워서(StoneHolder) 던지면 미끼가 된다.
    ///   땅에 있음  주울 수 있다. 근처의 돌은 Nearest로 찾는다
    ///   손에 있음  StoneHolder가 손에 붙여 들고 다닌다
    ///   날아감     포물선을 그리며 날아가 땅에 떨어지고, 멈춘 자리에서 미끼 소리(SoundList.Decoy)를 한 번 낸다.
    ///              멈추면 다시 "땅에 있음"이 되어 또 주울 수 있다
    ///
    /// 날아가는 동안의 움직임은 예전 횃불 던지기와 같다. 물리 엔진에 맡기지 않고 직접 계산한다.
    ///   - 땅에 닿으면 튕긴다. 튕길 힘이 남지 않았으면 그 자리에 멈춘다
    ///   - 맵 가장자리의 벽에 닿으면 튕겨 나온다
    ///   - 나무는 그냥 지나간다 (나무가 빽빽한 숲에서 던질 때마다 걸리지 않도록)
    ///
    /// 미끼 소리에는 출처를 넘기지 않는다. 같은 돌을 다시 던져도 적이 또 반응하게 하기 위해서다.
    /// (출처를 넘기면 적이 한 번 조사한 출처의 소리는 무시한다)
    ///
    /// 씬 구성:
    ///   따로 할 일이 없다. StageLayout이 돌 모델을 맵 곳곳에 놓으면서 이 스크립트를 붙인다.
    /// </summary>
    public class Stone : MonoBehaviour
    {
        // 수치는 GameTuning 에셋(Resources/GameTuning)에서 읽는다.
        private float gravity => GameTuning.Instance.stone.gravity;
        [Tooltip("돌이 부딪히는 대상의 레이어. 땅(지형)이 속한 레이어가 반드시 들어 있어야 한다")]
        [SerializeField] private LayerMask landMask = (1 << 0) | (1 << 10);   // Default + Floor (땅, 맵 가장자리의 벽)
        private float bounciness => GameTuning.Instance.stone.bounciness;
        private int maxBounces => GameTuning.Instance.stone.maxBounces;
        private float spinSpeed => GameTuning.Instance.stone.spinSpeed;
        private Color outlineColor => GameTuning.Instance.stone.outlineColor;
        private float outlineWidth => GameTuning.Instance.stone.outlineWidth;

        private const string OutlineShaderName = "Hidden/Custom/StoneOutline";
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        // 돌 모델마다 한 번만 만드는, 면 방향을 부드럽게 다시 계산한 테두리용 메시
        private static readonly Dictionary<Mesh, Mesh> _outlineMeshes = new();

        // 면이 위를 향하는 정도(normal.y)가 이 값 이상이어야 땅으로 본다. 그보다 가파르면 벽으로 본다.
        private const float GroundNormalY = 0.5f;
        // 튕길 때 면을 따라 미끄러지는 속도가 남는 비율
        private const float SlideKeep = 0.6f;
        // 튀어 오르는 속도가 이 값(m/s)보다 작으면 더 튕기지 않고 멈춘다
        private const float MinBounceSpeed = 4f;

        // 맵에 있는 모든 돌. 가까운 돌을 찾을 때 쓴다.
        private static readonly List<Stone> _all = new();

        private bool _flying;
        private bool _held;
        private Vector3 _velocity;
        private int _bouncesLeft;
        // 던진 순간 플레이어가 있던 차원. 미끼 소리는 이 차원의 적에게만 들린다.
        private Dimension _dimension;
        private LayerMask _enemyLayer;
        // 돌의 모습. 바닥 높이를 잴 때 쓴다.
        private Renderer _body;
        // 흰 테두리. 땅에 놓여 주울 수 있을 때만 켠다.
        private Renderer _outline;

        /// <summary>땅에 놓여 있어서 주울 수 있는가.</summary>
        public bool CanPickUp => !_flying && !_held;

        private void Awake()
        {
            _body = GetComponentInChildren<Renderer>();
            CreateOutline();
        }

        /// <summary>
        /// 돌 모양의 복사본을 자식으로 하나 만들어 흰 테두리 셰이더로 그린다. (StoneOutline.shader 참고)
        /// 이 돌은 각진 저폴리곤 모델이라, 원래 메시의 면 방향대로 부풀리면 모서리마다 테두리가 갈라진다.
        /// 그래서 같은 자리에 있는 꼭짓점들의 면 방향을 평균 낸 테두리용 메시를 따로 만들어 쓴다.
        /// </summary>
        private void CreateOutline()
        {
            if (outlineWidth <= 0f)
                return;

            MeshFilter source = GetComponentInChildren<MeshFilter>();
            Shader shader = Shader.Find(OutlineShaderName);
            if (source == null || source.sharedMesh == null || shader == null)
                return;

            var outline = new GameObject("Outline");
            outline.transform.SetParent(source.transform, false);
            outline.AddComponent<MeshFilter>().sharedMesh = OutlineMeshFor(source.sharedMesh);

            var outlineRenderer = outline.AddComponent<MeshRenderer>();
            outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outlineRenderer.receiveShadows = false;
            var material = new Material(shader);
            material.SetColor(OutlineColorId, outlineColor);
            material.SetFloat(OutlineWidthId, outlineWidth);
            outlineRenderer.sharedMaterial = material;
            _outline = outlineRenderer;
        }

        // 같은 자리의 꼭짓점들이 같은 면 방향을 갖도록 평균 낸 복사본. 모델마다 한 번만 만든다.
        private static Mesh OutlineMeshFor(Mesh mesh)
        {
            if (_outlineMeshes.TryGetValue(mesh, out Mesh smooth))
                return smooth;

            smooth = Instantiate(mesh);
            Vector3[] vertices = smooth.vertices;
            Vector3[] normals = smooth.normals;
            var sums = new Dictionary<Vector3, Vector3>();
            for (int i = 0; i < vertices.Length; i++)
            {
                sums.TryGetValue(vertices[i], out Vector3 sum);
                sums[vertices[i]] = sum + normals[i];
            }
            for (int i = 0; i < vertices.Length; i++)
                normals[i] = sums[vertices[i]].normalized;

            smooth.normals = normals;
            _outlineMeshes.Add(mesh, smooth);
            return smooth;
        }

        private void OnEnable() => _all.Add(this);

        private void OnDisable() => _all.Remove(this);

        /// <summary>
        /// position에서 radius(평면 거리) 안에 있는, 주울 수 있는 돌 중 가장 가까운 것. 없으면 null.
        /// </summary>
        public static Stone Nearest(Vector3 position, float radius)
        {
            Stone best = null;
            float bestDistance = radius * radius;
            foreach (Stone stone in _all)
            {
                if (!stone.CanPickUp)
                    continue;

                Vector3 offset = stone.transform.position - position;
                offset.y = 0f;
                float distance = offset.sqrMagnitude;
                if (distance <= bestDistance)
                {
                    best = stone;
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>
        /// 지금 자리의 높이를 땅으로 보고, 돌의 바닥이 그 높이에 닿도록 올리거나 내린다.
        /// (모델의 기준점이 가운데가 아니어도 바닥이 땅에 맞는다)
        /// </summary>
        public void SnapToGround()
        {
            if (_body == null)
                return;

            float ground = transform.position.y;
            transform.position += Vector3.up * (ground - _body.bounds.min.y);
        }

        /// <summary>주웠을 때 StoneHolder가 부른다. 이 뒤로는 StoneHolder가 손에 붙여 움직인다.</summary>
        public void OnPickedUp()
        {
            _held = true;
            // 손에 든 돌은 이미 어디 있는지 알므로 테두리를 끈다. (던져서 땅에 멈추면 다시 켠다)
            SetOutline(false);
            _flying = false;
        }

        /// <summary>
        /// 돌을 velocity의 속도로 던진다. 부르기 전에 플레이어의 손에서 떼어 놓아야 한다.
        /// </summary>
        public void Throw(Vector3 velocity, Dimension dimension, LayerMask enemyLayer)
        {
            _held = false;
            _velocity = velocity;
            _dimension = dimension;
            _enemyLayer = enemyLayer;
            _bouncesLeft = maxBounces;
            _flying = true;
        }

        private void Update()
        {
            if (!_flying)
                return;

            _velocity.y += gravity * Time.deltaTime;

            Vector3 from = transform.position;
            Vector3 to = from + _velocity * Time.deltaTime;

            // 이번 프레임에 지나갈 구간에 걸리는 것이 있는가
            if (Physics.Linecast(from, to, out RaycastHit hit, landMask, QueryTriggerInteraction.Ignore))
            {
                HitSurface(hit);
                return;
            }

            transform.position = to;
            transform.Rotate(Vector3.right, spinSpeed * Time.deltaTime, Space.Self);

            // 맵 밖으로 떨어져 끝없이 내려가는 경우에 대한 방어.
            if (to.y < -100f)
                Destroy(gameObject);
        }

        // 면에 부딪혔을 때. 튕기거나, 땅이고 튕길 힘이 없으면 멈춘다.
        private void HitSurface(RaycastHit hit)
        {
            Vector3 intoSurface = Vector3.Project(_velocity, hit.normal);
            Vector3 alongSurface = _velocity - intoSurface;
            float bounceSpeed = intoSurface.magnitude * bounciness;

            bool isGround = hit.normal.y >= GroundNormalY;
            if (isGround)
            {
                if (_bouncesLeft <= 0 || bounceSpeed < MinBounceSpeed)
                {
                    Land(hit.point);
                    return;
                }
                _bouncesLeft--;
            }

            _velocity = alongSurface * SlideKeep - intoSurface * bounciness;
            transform.position = hit.point + hit.normal * 0.05f;
        }

        private void SetOutline(bool visible)
        {
            if (_outline != null)
                _outline.enabled = visible;
        }

        private void Land(Vector3 point)
        {
            // 다시 주울 수 있게 되었으니 테두리를 켠다.
            SetOutline(true);
            _flying = false;
            transform.position = point;
            SnapToGround();

            // 출처를 넘기지 않아서, 같은 돌을 다시 던져도 적이 또 조사하러 온다.
            SoundEmitter.Emit(point, SoundList.Decoy, _enemyLayer, _dimension);
        }
    }
}
