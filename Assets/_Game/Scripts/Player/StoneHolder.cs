using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 플레이어가 돌을 줍고 던지는 것을 관리한다. 던진 돌은 미끼가 된다(Stone).
    ///   줍기    근처(pickupRadius)에 떨어진 돌이 있으면 E로 줍는다. 한 번에 하나만 들 수 있다.
    ///           허리를 숙여 줍는 동작(PickUp, 왼손)을 하는 동안은 제자리에 멈추고, 손이 땅에 닿는 순간 돌을 쥔다
    ///   던지기  돌을 든 채로 다시 E를 누르면, 던지는 동작을 재생하고 팔을 휘두르는 순간에 바라보는 방향으로 던진다
    ///
    /// 오른손에는 횃불이 있으므로 돌은 왼손에 든다. 던지는 애니메이션(Throw)은 오른손으로 던지는 동작이라,
    /// 컨트롤러(PlayerHazmat)의 Throw 레이어에서 좌우를 뒤집어(Mirror) 왼손으로 던지게 했다.
    /// 상체에만 적용되는 레이어라 걷거나 뛰면서도 던질 수 있다.
    ///
    /// 씬 구성:
    ///   Player가 붙은 오브젝트에 붙인다. 돌은 StageLayout이 맵 곳곳에 놓는다.
    /// </summary>
    [RequireComponent(typeof(Player))]
    public class StoneHolder : MonoBehaviour
    {
        [Header("○ 줍기")]
        [Tooltip("돌을 줍고 던지는 키")]
        [SerializeField] private KeyCode useKey = KeyCode.E;
        [Tooltip("이 거리(m, 평면) 안에 있는 돌을 주울 수 있다")]
        [SerializeField] private float pickupRadius = 4f;
        [Tooltip("돌 근처에서 화면에 띄울 안내 문구")]
        [SerializeField] private string pickupMessage = "[E] 돌 줍기";
        [Tooltip("줍는 동작의 재생 속도 배율. 줍는 동안은 멈춰 있어야 하므로 짧게 끝나도록 빠르게 재생한다")]
        [SerializeField] private float pickupAnimationSpeed = 2f;
        [Tooltip("줍는 동작에서 손이 땅에 닿아 돌을 쥐는 순간(초, 원래 애니메이션 기준). 몸이 가장 낮게 숙여지는 때다")]
        [SerializeField] private float grabTime = 0.47f;
        [Tooltip("줍는 동작이 이 지점(원래 애니메이션 길이에 대한 비율)까지 재생되면 다시 움직일 수 있다")]
        [Range(0f, 1f)]
        [SerializeField] private float pickupFinishAt = 0.85f;

        [Header("○ 손에 쥐는 자리")]
        [Tooltip("돌을 쥘 손뼈의 이름. 켜져 있는 모델에서 찾는다")]
        [SerializeField] private string handBoneName = "mixamorig:LeftHand";
        [Tooltip("손뼈를 기준으로 한 돌의 위치(m, 모델 Scale 1 기준). Mixamo 뼈대에서는 Y가 손가락 쪽, Z가 손바닥이 향하는 쪽이다")]
        [SerializeField] private Vector3 handOffset = new Vector3(0f, 0.08f, 0.05f);

        [Header("○ 던지기")]
        [Tooltip("던질 때 앞으로 나가는 속도(m/s)")]
        [SerializeField] private float throwSpeed = 40f;
        [Tooltip("던질 때 위로 솟는 속도(m/s). 클수록 높이, 멀리 날아간다")]
        [SerializeField] private float throwUpSpeed = 20f;
        [Tooltip("던지는 동작의 재생 속도 배율. 누르자마자 던지도록 빠르게 재생한다")]
        [SerializeField] private float throwAnimationSpeed = 3f;
        [Tooltip("던지는 동작을 이 지점(초, 원래 애니메이션 기준)부터 재생한다. 팔을 뒤로 젖히는 준비 동작을 건너뛰고 " +
                 "앞으로 휘두르는 부분부터 시작해서, 누르자마자 던지는 것처럼 보이게 한다")]
        [SerializeField] private float throwStartTime = 0.65f;
        [Tooltip("던지는 동작에서 돌이 손을 떠나는 순간(초, 원래 애니메이션 기준). 팔을 가장 빠르게 휘두르는 때다")]
        [SerializeField] private float releaseTime = 0.8f;
        [Tooltip("던진 돌이 떨어지는 소리를 들을 수 있는 적의 레이어")]
        [SerializeField] private LayerMask enemyLayer = (1 << 8) | (1 << 9);   // RealEnemy + FakeEnemy

        // 컨트롤러(PlayerHazmat)의 Throw 레이어와 상태, 속도 파라미터 이름
        private const string ThrowLayerName = "Throw";
        private const string ThrowStateName = "Throw";
        private const string SpeedParameter = "throwSpeed";
        private const float BlendIn = 0.1f;
        // 컨트롤러의 Pick Up 레이어(전신)와 상태, 속도 파라미터 이름
        private const string PickUpLayerName = "Pick Up";
        private const string PickUpStateName = "PickUp";
        private const string PickUpSpeedParameter = "pickUpSpeed";

        private Player _player;
        private Transform _hand;
        private Animator _animator;
        private int _throwLayer = -1;
        private int _pickUpLayer = -1;
        private float _pickUpClipLength;

        // 줍는 중인 돌과, 손이 닿기까지 / 동작이 끝나기까지 남은 시간(초). 줍는 중이 아니면 null.
        private Stone _picking;
        private float _grabTimer;
        private float _finishTimer;

        // 지금 든 돌. 빈손이면 null.
        private Stone _held;
        // 던지는 동작 중 돌을 놓기까지 남은 시간(초). 던지는 중이 아니면 0 이하.
        private float _releaseTimer;
        private bool _throwing;
        // 지금 줍기 안내를 띄워 두었는가
        private bool _promptShown;

        public bool HasStone => _held != null;

        private void Awake()
        {
            _player = GetComponent<Player>();
            foreach (Transform child in GetComponentsInChildren<Transform>())
                if (child.name == handBoneName)
                    _hand = child;

            _animator = GetComponentInChildren<Animator>();
            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                _throwLayer = _animator.GetLayerIndex(ThrowLayerName);
                _pickUpLayer = _animator.GetLayerIndex(PickUpLayerName);
                foreach (AnimationClip clip in _animator.runtimeAnimatorController.animationClips)
                    if (clip.name == PickUpStateName)
                        _pickUpClipLength = clip.length;
            }
        }

        private void Update()
        {
            // 일시정지 중에는 줍거나 던지지 않는다.
            if (Time.timeScale == 0f)
                return;

            if (_throwing)
            {
                _releaseTimer -= Time.deltaTime;
                if (_releaseTimer <= 0f)
                    Release();
                SetPrompt(false);
                return;
            }

            if (_picking != null)
            {
                UpdatePickUp();
                SetPrompt(false);
                return;
            }

            Stone nearby = HasStone ? null : Stone.Nearest(transform.position, pickupRadius);
            SetPrompt(nearby != null);

            if (!Input.GetKeyDown(useKey))
                return;

            if (HasStone)
                StartThrow();
            else if (nearby != null)
                StartPickUp(nearby);
        }

        /// <summary>
        /// 줍는 동작을 시작한다. 허리를 숙여 다리까지 쓰는 동작이라 그동안은 제자리에 멈춘다.
        /// 손이 땅에 닿는 순간(grabTime) 돌을 손에 쥐고, 동작이 거의 끝나면(pickupFinishAt) 다시 움직일 수 있다.
        /// 동작이 없으면 바로 줍는다.
        /// </summary>
        private void StartPickUp(Stone stone)
        {
            if (_animator == null || _pickUpLayer < 0 || _pickUpClipLength <= 0f)
            {
                PickUp(stone);
                return;
            }

            float speed = Mathf.Max(pickupAnimationSpeed, 0.01f);
            _picking = stone;
            _grabTimer = Mathf.Min(grabTime, _pickUpClipLength) / speed;
            _finishTimer = _pickUpClipLength * pickupFinishAt / speed;
            _player.MovementLocked = true;

            // 줍는 쪽(돌이 있는 쪽)을 바라본다.
            Vector3 toStone = stone.transform.position - transform.position;
            toStone.y = 0f;
            if (toStone.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(toStone, Vector3.up);

            _animator.SetFloat(PickUpSpeedParameter, speed);
            _animator.CrossFadeInFixedTime(PickUpStateName, BlendIn, _pickUpLayer, 0f);
        }

        private void UpdatePickUp()
        {
            if (_grabTimer > 0f)
            {
                _grabTimer -= Time.deltaTime;
                if (_grabTimer <= 0f && _picking.CanPickUp)
                    PickUp(_picking);
            }

            _finishTimer -= Time.deltaTime;
            if (_finishTimer <= 0f)
            {
                _picking = null;
                _player.MovementLocked = false;
            }
        }

        // 든 돌을 손에 붙여 둔다. (애니메이션이 손을 움직인 뒤에 맞춰야 하므로 LateUpdate에서 한다)
        private void LateUpdate()
        {
            if (_held == null)
                return;

            if (_hand != null)
                _held.transform.position = _hand.position + _hand.rotation * (handOffset * ModelScale());
            else
                _held.transform.position = transform.position + transform.right * -0.6f + transform.forward * 0.3f;
        }

        private void PickUp(Stone stone)
        {
            _held = stone;
            stone.OnPickedUp();
            SetPrompt(false);
        }

        // 던지는 동작을 시작한다. 동작이 없으면 바로 던진다.
        private void StartThrow()
        {
            if (_animator == null || _throwLayer < 0)
            {
                Release();
                return;
            }

            _animator.SetFloat(SpeedParameter, throwAnimationSpeed);
            // 준비 동작을 건너뛰고 앞으로 휘두르는 부분부터 빠르게 재생한다.
            // 돌은 손을 떠나는 지점까지 남은 아주 짧은 시간(지금 값으로 약 0.05초) 뒤에 날아간다.
            float start = Mathf.Min(throwStartTime, releaseTime);
            _animator.CrossFadeInFixedTime(ThrowStateName, BlendIn, _throwLayer, start);
            _releaseTimer = (releaseTime - start) / Mathf.Max(throwAnimationSpeed, 0.01f);
            _throwing = true;
        }

        // 돌을 손에서 놓아 바라보는 방향으로 던진다.
        private void Release()
        {
            _throwing = false;
            if (_held == null)
                return;

            Stone stone = _held;
            _held = null;

            // 비탈에 서 있으면 그 기울기를 따라 던진다. 수평으로만 던지면 오르막에서 발 앞의 땅에 바로 박힌다.
            Vector3 forward = transform.forward;
            Terrain terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                Vector3 local = transform.position - terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                Vector3 groundNormal = terrain.terrainData.GetInterpolatedNormal(local.x / size.x, local.z / size.z);
                forward = Vector3.ProjectOnPlane(forward, groundNormal).normalized;
            }

            stone.Throw(forward * throwSpeed + Vector3.up * throwUpSpeed, _player.myDimension, enemyLayer);
        }

        // 손뼈가 들어 있는 모델(플레이어 바로 아래 자식)의 Scale.
        private float ModelScale()
        {
            Transform model = _hand;
            while (model.parent != null && model.parent != transform)
                model = model.parent;

            return model.localScale.x;
        }

        private void SetPrompt(bool show)
        {
            if (show == _promptShown || InteractionPrompt.Instance == null)
                return;

            _promptShown = show;
            if (show)
                InteractionPrompt.Instance.Show(this, pickupMessage);
            else
                InteractionPrompt.Instance.Hide(this);
        }

        // Stage가 사라질 때 안내가 남지 않게 한다.
        private void OnDisable() => SetPrompt(false);
    }
}
