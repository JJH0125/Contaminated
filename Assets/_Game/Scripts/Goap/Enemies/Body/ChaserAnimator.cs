using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 추격자의 실제 이동 속도를 보고 애니메이션(Idle / Walk)을 고른다.
    ///
    /// GOAP이나 ChaserLocomotion이 "지금 무슨 행동 중인지"를 묻지 않고,
    /// 위치가 실제로 얼마나 변했는지만 본다. 그래서 행동이 늘어나도 이 스크립트는
    /// 고칠 필요가 없고, 벽에 막혀 제자리걸음을 하는 일도 없다.
    ///
    /// 달리기 애니메이션이 따로 없는 모델이므로, 빨리 움직일수록 Walk를 빠르게 재생한다.
    ///
    /// 발소리: footsteps에 클립을 넣은 적만 낸다. 걸은 거리가 footstepStride를 넘을 때마다 하나씩 골라 3D 소리로 낸다.
    /// 플레이어에게만 들리는 연출이고 적의 감지(SoundEmitter)와는 상관없다. 플레이어와 같은 차원일 때만 들린다.
    /// 재의 적은 발 대신 바람 소리(AshWindSound)를 내므로 비워 둔다.
    ///
    /// Animator Controller 구성:
    ///   Bool 파라미터 "IsMove"
    ///   Idle → Walk  조건 IsMove = true   (Has Exit Time 끔)
    ///   Walk → Idle  조건 IsMove = false  (Has Exit Time 끔)
    ///
    /// 씬 구성:
    ///   ChaserLocomotion이 붙은 적 오브젝트에 붙인다. 모델(Animator)은 그 자식으로 둔다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ChaserAnimator : MonoBehaviour
    {
        [Header("□ 선택 연결 — 비워두면 자동 처리")]
        [Tooltip("모델의 Animator. 비워두면 자식에서 찾는다")]
        [SerializeField] private Animator animator;
        [Tooltip("걸을 때 낼 발소리. 여러 개면 번갈아 고른다. 비워두면 발소리를 내지 않는다")]
        [SerializeField] private AudioClip[] footsteps;

        // 수치는 GameTuning 에셋의 enemyBody 항목에서 읽는다.
        private float moveThreshold => GameTuning.Instance.enemyBody.moveThreshold;
        private float walkAnimMoveSpeed => GameTuning.Instance.enemyBody.walkAnimMoveSpeed;
        private float maxAnimSpeed => GameTuning.Instance.enemyBody.maxAnimSpeed;
        private float animSpeedMultiplier => GameTuning.Instance.enemyBody.animSpeedMultiplier;

        private Rigidbody _rigidbody;
        private Vector3 _lastPosition;
        private float _moveSpeed;

        private AudioSource _stepSource;
        private DimensionMember _member;
        private float _stepDistance;
        private int _lastStep = -1;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();

            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            _member = GetComponent<DimensionMember>();
            if (footsteps != null && footsteps.Length > 0)
            {
                _stepSource = gameObject.AddComponent<AudioSource>();
                _stepSource.playOnAwake = false;
                _stepSource.spatialBlend = 1f;
                _stepSource.rolloffMode = AudioRolloffMode.Linear;
                _stepSource.minDistance = 3f;
                _stepSource.dopplerLevel = 0f;
            }
        }

        private void OnEnable() => _lastPosition = _rigidbody.position;

        // Rigidbody의 위치는 물리 갱신 때만 바뀌므로, 속도도 같은 주기로 잰다.
        // (Update에서 재면 물리 갱신이 없던 프레임에 속도가 0으로 튄다)
        private void FixedUpdate()
        {
            Vector3 moved = _rigidbody.position - _lastPosition;
            moved.y = 0f;
            // 이동 명령이 한 번 빠진 물리 갱신에서 Idle로 깜빡이지 않도록 부드럽게 따라간다.
            _moveSpeed = Mathf.Lerp(_moveSpeed, moved.magnitude / Time.fixedDeltaTime, 0.25f);
            _lastPosition = _rigidbody.position;

            if (_stepSource != null && _moveSpeed > moveThreshold)
            {
                _stepDistance += moved.magnitude;
                if (_stepDistance >= Mathf.Max(GameTuning.Instance.enemyBody.footstepStride, 0.2f))
                {
                    _stepDistance = 0f;
                    PlayFootstep();
                }
            }
        }

        // 발소리 하나를 낸다. 같은 클립이 연달아 나지 않게 고른다.
        private void PlayFootstep()
        {
            // 다른 차원의 적은 보이지 않으므로 발소리도 들리지 않는다.
            DimensionController controller = DimensionController.Instance;
            if (controller != null && _member != null && !controller.CompareDimension(_member))
                return;

            int index = Random.Range(0, footsteps.Length);
            if (footsteps.Length > 1 && index == _lastStep)
                index = (index + 1) % footsteps.Length;
            _lastStep = index;
            if (footsteps[index] == null)
                return;

            EnemyBodyTuning tuning = GameTuning.Instance.enemyBody;
            float sfxVolume = SfxPlayer.Instance != null ? SfxPlayer.Instance.Volume : 1f;
            _stepSource.maxDistance = tuning.footstepMaxDistance;
            _stepSource.pitch = Random.Range(0.9f, 1.05f);
            _stepSource.PlayOneShot(footsteps[index], tuning.footstepVolume * sfxVolume);
        }

        private void Update()
        {
            if (animator == null)
                return;

            bool isMove = _moveSpeed > moveThreshold;
            animator.SetBool("IsMove", isMove);

            // 걷는 동안에만 이동 속도에 맞춰 재생 속도를 바꾸고, 멈추면 원래대로 돌린다.
            float speed = isMove
                ? Mathf.Clamp(_moveSpeed / walkAnimMoveSpeed, 0.5f, maxAnimSpeed)
                : 1f;
            animator.speed = speed * animSpeedMultiplier;
        }

        /// <summary>
        /// 이동 애니메이션을 멈추고 지정한 상태(공격 등)를 처음부터 원래 빠르기로 재생한다.
        /// 이후로는 이동 속도를 따라가지 않는다. 적에게 잡힌 연출에서 쓴다.
        /// </summary>
        public void PlayOnce(string stateName)
        {
            // Update가 재생 속도와 IsMove를 다시 덮어쓰지 않도록 끈다.
            enabled = false;

            if (animator == null)
                return;

            animator.speed = 1f;
            animator.Play(stateName, 0, 0f);
        }
    }
}
