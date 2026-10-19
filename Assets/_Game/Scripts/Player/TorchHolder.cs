using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 플레이어가 손에 든 횃불과, 횃불을 숨기는 동안 줄어드는 온기 게이지를 관리한다.
    ///
    /// 횃불은 라운드가 시작될 때부터 손에 들고 있다. 던지거나 놓을 수 없다.
    ///   숨기기  Space를 누르고 있는 동안 횃불을 주머니에 넣어 둔다(횃불 오브젝트를 끈다).
    ///           불빛이 꺼지므로 적에게 "불빛에 드러난" 상태가 되지 않고, 주변도 어두워진다.
    ///   꺼내기  Space를 떼면 다시 꺼낸다.
    ///
    /// 온기 게이지(Warmth, 0~1):
    ///   숨기고 있는 동안 점점 줄어들고, 들고 있는 동안 다시 찬다.
    ///   다 떨어지면 의지와 상관없이 횃불을 꺼내 든다. 이때는 Space를 한 번 뗐다가 다시 눌러야 숨길 수 있다.
    ///   게이지 표시(WarmthGauge)와 화면 가장자리 오염 효과(ContaminationEdgeEffect)가 이 값을 읽어 간다.
    ///
    /// 모델에서 손뼈를 찾으면 횃불을 손에 쥐여서, 달리거나 걸을 때 팔과 함께 흔들리게 한다.
    /// 손뼈를 찾지 못하면(뼈 이름이 다른 모델 등) 몸 옆의 정해진 자리에 든다.
    ///
    /// 손에 든 동안에는 횃불의 불빛을 플레이어 앞쪽으로 내어 둬서, 밝은 곳이 진행 방향으로 쏠리게 한다.
    /// 플레이어 주변의 작은 기본 조명은 Player 아래의 일반 Light가 맡고, 적의 시야 규칙과는 상관없다.
    ///
    /// 넣고 꺼내는 동작은 이 스크립트가 직접 재생한다. 팔이 지금 동작의 어디쯤에 있는지(_armTime)를 기억해 두고,
    ///   누르면  그 지점부터 넣는 동작(HideTorch)을 앞으로 재생하고, 손이 주머니 지점(torchVanishDelay)을 지나면 횃불을 끈다
    ///   떼면    그 지점부터 같은 동작을 거꾸로(DrawTorch) 재생하고, 손이 주머니 지점을 다시 지나면 횃불을 켠다
    /// 그래서 넣다가 바로 떼거나 꺼내다가 다시 눌러도 팔이 튀지 않는다.
    /// 넣는 동작이 끝나면 팔은 다리처럼 걷기·뛰기·대기 동작을 그대로 따른다.
    /// (PlayerHazmat 컨트롤러의 Hide Torch 레이어가 상체에만 이 동작을 입힌다)
    /// 컨트롤러에 이 레이어가 없으면 모션 없이 횃불만 사라진다.
    ///
    /// 씬 구성:
    ///   Player가 붙은 오브젝트에 붙이고, torchPrefab에 횃불 프리팹(FireSource가 붙은 것)을 연결한다.
    /// </summary>
    [RequireComponent(typeof(Player))]
    public class TorchHolder : MonoBehaviour
    {
        [Header("■ 필수 연결 — 비워두면 에러")]
        [Tooltip("횃불 프리팹. FireSource가 붙어 있어야 한다")]
        [SerializeField] private Torch torchPrefab;

        [Header("○ 손에 쥐는 자리")]
        [Tooltip("횃불을 쥘 손뼈의 이름. 켜져 있는 모델에서 찾는다")]
        [SerializeField] private string handBoneName = "mixamorig:RightHand";
        [Tooltip("손뼈를 기준으로 한, 주먹 안쪽(횃불을 쥐는 자리)의 위치(m, 모델 Scale 1 기준). 손뼈의 축 방향 기준이다. " +
                 "Mixamo 뼈대에서는 Y가 손가락 쪽, Z가 손바닥이 향하는 쪽, -X가 엄지 쪽이다")]
        [SerializeField] private Vector3 handOffset = new Vector3(0f, 0.07f, 0.04f);
        [Tooltip("손뼈를 기준으로 한 횃불의 각도. Z가 90이면 횃불의 불꽃 쪽이 엄지 방향을 향한다")]
        [SerializeField] private Vector3 handRotation = new Vector3(0f, 0f, 120f);
        [Tooltip("횃불에서 손으로 쥐는 지점. 횃불 프리팹의 모델 기준이다. " +
                 "횃불 모델의 기준점은 막대의 한가운데이고 불꽃 쪽이 +Y이므로, 음수일수록 손잡이 끝 쪽을 쥔다")]
        [SerializeField] private Vector3 gripPoint = new Vector3(0f, -0.12f, 0f);
        [Tooltip("손뼈를 찾지 못했을 때 횃불을 드는 위치. 플레이어 오브젝트 기준 (오른쪽, 위, 앞). 플레이어 오브젝트의 위치는 발이 아니라 몸 한가운데다")]
        [SerializeField] private Vector3 holdPosition = new Vector3(0.65f, 0.2f, 0.35f);
        [Tooltip("손뼈를 찾지 못했을 때 횃불을 드는 각도. 플레이어 오브젝트 기준")]
        [SerializeField] private Vector3 holdRotation = new Vector3(10f, 0f, -25f);

        [Header("○ 손에 든 횃불의 불빛")]
        [Tooltip("불빛의 중심을 플레이어 앞으로 얼마나 내어 둘지(m). 빛은 둥글게 퍼지지만 밝은 곳이 진행 방향으로 쏠린다")]
        [SerializeField] private float heldLightForward = 6f;
        [Tooltip("불빛의 높이(m). 플레이어 오브젝트 위치(발에서 1m 위) 기준")]
        [SerializeField] private float heldLightHeight = 3f;
        [Tooltip("손에 들고 있는 동안 불빛이 닿는 거리(m)")]
        [SerializeField] private float heldLightRange = 22f;

        [Header("○ 횃불 숨기기와 온기 게이지")]
        [Tooltip("누르고 있는 동안 횃불을 숨기는 키")]
        [SerializeField] private KeyCode hideKey = KeyCode.Space;
        [Tooltip("가득 찬 온기 게이지가 숨기고 걷는 동안 모두 닳는 데 걸리는 시간(초)")]
        [SerializeField] private float warmthDrainTime = 8f;
        [Tooltip("숨긴 채 달리면 게이지가 걸을 때의 몇 배 빠르기로 닳는가")]
        [SerializeField] private float runDrainMultiplier = 1.6f;
        [Tooltip("빈 온기 게이지가 횃불을 들고 있는 동안 모두 차는 데 걸리는 시간(초)")]
        [SerializeField] private float warmthRecoverTime = 6f;
        [Tooltip("넣고 꺼내는 동작의 재생 속도 배율. 2면 원래 애니메이션의 2배 빠르기다")]
        [SerializeField] private float hideAnimationSpeed = 2f;
        [Tooltip("넣는 동작에서 손이 주머니에 닿는 순간(초, 원래 애니메이션 기준). 넣을 때는 이 지점을 지나면 횃불이 사라지고, " +
                 "꺼낼 때는 거꾸로 이 지점을 지나면 다시 나타난다. 재생 속도를 바꿔도 이 값은 그대로 두면 된다")]
        [SerializeField] private float torchVanishDelay = 0.4f;

        /// <summary>온기 게이지 (1: 가득, 0: 다 떨어짐). 라운드마다 가득 찬 채로 시작한다.</summary>
        public static float Warmth { get; private set; } = 1f;
        /// <summary>지금 횃불을 숨기고 있는가.</summary>
        public static bool IsHiding { get; private set; }

        // 손에 든 횃불. 횃불 프리팹이 비어 있으면 null.
        private Torch _torch;
        // 들고 있는 횃불의 불빛
        private FireSource _torchFire;
        // 횃불을 쥘 손뼈. 찾지 못하면 null.
        private Transform _hand;
        // 달리는 중인지 알아보기 위한 이동 담당
        private Player _player;
        // 넣고 꺼내는 동작을 재생할 Animator, 그 레이어 번호, 넣는 동작의 길이(초). 없으면 null / -1 / 0.
        private Animator _animator;
        private int _hideLayer = -1;
        private float _hideClipLength;

        // 팔이 지금 넣는 동작의 어디쯤에 있는가 (초, 원래 애니메이션 기준. 0 = 횃불을 쥔 자세)
        private float _armTime;
        // 팔이 움직이는 방향 (+1 넣는 중, -1 꺼내는 중, 0 넣고 꺼내는 동작을 하지 않음)
        private int _armDirection;

        // 컨트롤러(PlayerHazmat)의 Hide Torch 레이어 안의 상태와 파라미터 이름
        private const string HideLayerName = "Hide Torch";
        private const string HideStateName = "HideTorch";
        private const string DrawStateName = "DrawTorch";
        private const string EmptyStateName = "Empty";
        private const string SpeedParameter = "hideSpeed";
        // 넣기·꺼내기를 시작할 때, 다 꺼낸 뒤 원래 팔 동작으로 돌아갈 때 섞는 시간(초)
        private const float BlendIn = 0.12f;
        private const float BlendOut = 0.25f;
        // 게이지가 다 떨어져 강제로 꺼낸 뒤, 숨기기 키를 한 번 뗄 때까지 다시 숨기지 못하게 한다.
        private bool _mustReleaseKey;

        public bool HasTorch => _torch != null;

        // 넣고 꺼내는 동작을 재생할 수 있는가. (컨트롤러에 Hide Torch 레이어와 HideTorch 클립이 있어야 한다)
        private bool CanAnimate => _animator != null && _hideLayer >= 0 && _hideClipLength > 0f;

        private void Awake()
        {
            _hand = FindActiveChild(transform, handBoneName);
            _player = GetComponent<Player>();

            // 꺼 둔 예전 모델의 Animator는 건너뛴다.
            _animator = GetComponentInChildren<Animator>();
            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                _hideLayer = _animator.GetLayerIndex(HideLayerName);
                foreach (AnimationClip clip in _animator.runtimeAnimatorController.animationClips)
                    if (clip.name == HideStateName)
                        _hideClipLength = clip.length;
            }

            Warmth = 1f;
            IsHiding = false;

            // 게이지 표시가 없으면 만든다. (가장자리 오염 효과는 카메라에 스스로 붙는다)
            WarmthGauge.EnsureExists();
        }

        private void Start() => TakeTorch();

        private void Update()
        {
            // 일시정지 중에는 게이지도 멈춘다.
            if (Time.timeScale == 0f || !HasTorch)
                return;

            if (!Input.GetKey(hideKey))
                _mustReleaseKey = false;

            bool wantHide = Input.GetKey(hideKey) && !_mustReleaseKey;

            if (wantHide)
            {
                // 숨긴 채 달리면 더 빨리 닳는다.
                float drain = Time.deltaTime / Mathf.Max(warmthDrainTime, 0.01f);
                if (_player != null && _player.IsRunning)
                    drain *= runDrainMultiplier;
                Warmth = Mathf.Max(0f, Warmth - drain);

                // 다 떨어지면 의지와 상관없이 꺼내 든다.
                if (Warmth <= 0f)
                {
                    wantHide = false;
                    _mustReleaseKey = true;
                }
            }
            else
            {
                Warmth = Mathf.Min(1f, Warmth + Time.deltaTime / Mathf.Max(warmthRecoverTime, 0.01f));
            }

            SetHidden(wantHide);
            AdvanceArm();
        }

        // 숨기기 키를 누르거나 뗐을 때.
        private void SetHidden(bool hidden)
        {
            if (IsHiding == hidden)
                return;

            IsHiding = hidden;

            if (!CanAnimate)
            {
                // 동작이 없으면 바로 숨기고 바로 꺼낸다.
                _torch.gameObject.SetActive(!hidden);
                return;
            }

            _animator.SetFloat(SpeedParameter, hideAnimationSpeed);
            if (hidden)
                StartHide();
            else
                StartDraw();
        }

        /// <summary>
        /// 넣는 동작을 시작한다. 꺼내던 중이면 팔이 있던 그 지점부터 다시 넣는다(되감던 것을 그대로 다시 감는다).
        /// </summary>
        private void StartHide()
        {
            if (_armDirection < 0)
                _animator.Play(HideStateName, _hideLayer, _armTime / _hideClipLength);
            else
            {
                _armTime = 0f;
                _animator.CrossFadeInFixedTime(HideStateName, BlendIn, _hideLayer, 0f);
            }

            _armDirection = 1;
        }

        /// <summary>
        /// 꺼내는 동작을 시작한다. 넣는 동작을 거꾸로 재생하는 것이다.
        ///   넣던 중이면  팔이 있던 그 지점부터 되감는다. (넣다가 바로 떼도 팔이 튀지 않는다)
        ///   다 넣은 뒤면 팔은 이미 걷기·뛰기 동작으로 돌아가 있으므로, 손이 주머니에 닿은 지점부터 되감는다.
        /// 횃불은 되감다가 손이 주머니 지점을 지날 때 다시 나타난다. (AdvanceArm에서)
        /// </summary>
        private void StartDraw()
        {
            if (_armDirection > 0)
                _animator.Play(DrawStateName, _hideLayer, _armTime / _hideClipLength);
            else
            {
                _armTime = Mathf.Clamp(torchVanishDelay, 0f, _hideClipLength);
                _animator.CrossFadeInFixedTime(DrawStateName, BlendIn, _hideLayer, _armTime);
            }

            _armDirection = -1;
        }

        /// <summary>
        /// 팔의 위치를 재생 속도만큼 앞이나 뒤로 옮기고, 손이 주머니 지점을 지나는 순간 횃불을 끄거나 켠다.
        /// 끄면 불빛도 꺼지고, 불빛에 드러나는 규칙(FireSource)에서도 빠진다.
        /// </summary>
        private void AdvanceArm()
        {
            if (_armDirection == 0)
                return;

            float pocketPoint = Mathf.Clamp(torchVanishDelay, 0f, _hideClipLength);
            _armTime += _armDirection * hideAnimationSpeed * Time.deltaTime;

            if (_armDirection > 0)
            {
                if (_armTime >= pocketPoint && _torch.gameObject.activeSelf)
                    _torch.gameObject.SetActive(false);

                // 넣는 동작이 끝나 가면 컨트롤러가 알아서 원래 팔 동작으로 섞어 돌려보낸다.
                if (_armTime >= _hideClipLength)
                {
                    _armTime = _hideClipLength;
                    _armDirection = 0;
                }
            }
            else
            {
                if (_armTime <= pocketPoint && !_torch.gameObject.activeSelf)
                    _torch.gameObject.SetActive(true);

                // 다 꺼냈으면 팔을 원래 이동 동작으로 돌려보낸다.
                if (_armTime <= 0f)
                {
                    _armTime = 0f;
                    _armDirection = 0;
                    _animator.CrossFadeInFixedTime(EmptyStateName, BlendOut, _hideLayer);
                }
            }
        }

        private void OnDestroy()
        {
            // 라운드가 끝나 Stage와 함께 사라지면, 화면 효과가 남지 않도록 게이지를 되돌린다.
            Warmth = 1f;
            IsHiding = false;
        }

        // 횃불을 하나 만들어 손에 든다. 라운드가 시작될 때 한 번만 부른다.
        private void TakeTorch()
        {
            if (HasTorch || torchPrefab == null)
                return;

            if (_hand == null)
            {
                _torch = Instantiate(torchPrefab, transform);
                _torch.transform.localPosition = holdPosition;
                _torch.transform.localRotation = Quaternion.Euler(holdRotation);
            }
            else
            {
                // 손뼈는 모델을 들여올 때의 크기 배율을 갖고 있을 수 있다.
                // 월드 기준 크기를 그대로 두고 붙여야 횃불이 갑자기 커지거나 작아지지 않는다.
                _torch = Instantiate(torchPrefab);
                _torch.transform.SetParent(_hand, true);
                PlaceInHand();
            }

            _torchFire = _torch.GetComponent<FireSource>();
            PlaceHeldLight();
        }

        // 손에 든 횃불의 자리와 불빛을 맞춘다. 값을 바꾸면 플레이 중에도 바로 보이도록 매 프레임 맞춘다.
        // (애니메이션이 손을 움직인 뒤에 맞춰야 하므로 LateUpdate에서 한다)
        private void LateUpdate()
        {
            // 횃불이 꺼진 뒤(주머니 속)에는 맞출 필요가 없다. 사라지기 전까지는 손에 붙어 따라간다.
            if (!HasTorch || !_torch.gameObject.activeSelf)
                return;

            if (_hand != null && _torch.transform.parent == _hand)
                PlaceInHand();
            PlaceHeldLight();
        }

        // 손에 든 횃불의 불빛을 플레이어 앞쪽에 둔다.
        // 빛은 여전히 둥글게 퍼지지만, 밝은 곳이 진행 방향으로 쏠려서 앞을 비추는 도구가 된다.
        // (적에게 드러나는 범위는 FireSource가 횃불 자체의 자리로 재므로 바뀌지 않는다)
        private void PlaceHeldLight()
        {
            if (_torchFire == null)
                return;

            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward.Normalize();
            _torchFire.PlaceLight(
                transform.position + forward * heldLightForward + Vector3.up * heldLightHeight, heldLightRange);
        }

        // 위치는 미터 단위로 다루기 위해, 손뼈의 크기 배율을 빼고 방향만 써서 계산한다.
        // 대신 모델을 키우거나 줄인 만큼은 따라가도록, 모델의 Scale을 곱한다.
        // 횃불의 기준점(막대 한가운데)이 아니라 손잡이의 gripPoint가 주먹 안쪽에 오도록 옮긴다.
        private void PlaceInHand()
        {
            Transform torch = _torch.transform;
            Vector3 fist = _hand.position + _hand.rotation * (handOffset * ModelScale());
            torch.rotation = _hand.rotation * Quaternion.Euler(handRotation);
            torch.position = fist - torch.TransformVector(gripPoint);
        }

        // 손뼈가 들어 있는 모델(플레이어 바로 아래 자식)의 Scale.
        private float ModelScale()
        {
            Transform model = _hand;
            while (model.parent != null && model.parent != transform)
                model = model.parent;

            return model.localScale.x;
        }

        // root 아래에서 이름이 같은 오브젝트를 찾는다. 꺼 둔 모델(예전 모델 등)의 뼈는 건너뛴다.
        private static Transform FindActiveChild(Transform root, string childName)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>())
                if (child.name == childName)
                    return child;

            return null;
        }
    }
}
