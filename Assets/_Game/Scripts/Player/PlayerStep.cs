using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 플레이어가 움직이는 동안 주기적으로 발소리를 방출한다.
    ///
    /// 걷는지 뛰는지는 직접 계산하지 않고 Animator의 bool 파라미터를 읽는다.
    /// Player 컨트롤러가 애니메이션을 위해 이미 판단해 둔 값이므로,
    ///   - 같은 계산을 두 번 하지 않고
    ///   - 애니메이션과 발소리가 항상 같은 상태를 공유한다
    ///
    /// 파라미터 의미(Player.cs 기준):
    ///   isRun  = 움직이는 중인가 (moveVector != zero)
    ///   isWalk = Walk 버튼을 눌러 천천히 걷는 중인가
    /// 즉 기본이 뛰기이고, Walk 버튼을 누르면 걷기가 된다.
    ///
    /// 발소리는 "매 프레임"이 아니라 "발을 디딜 때마다"에 가깝게 내야
    /// 자연스러우므로 interval마다 한 번씩 낸다.
    /// </summary>
    public class PlayerStep : MonoBehaviour
    {
        [Header("■ 필수 연결 — 비워두면 에러")]
        [Tooltip("플레이어의 Animator")]
        [SerializeField] private Animator animator;

        [Header("○ 튜닝 값 — 자유롭게 조절")]
        [Tooltip("발소리를 들을 수 있는 적의 레이어")]
        [SerializeField] private LayerMask enemyLayer;
        [Tooltip("움직이는 중인지 나타내는 파라미터 이름")]
        [SerializeField] private string movingParameter = "isRun";
        [Tooltip("천천히 걷는 중인지 나타내는 파라미터 이름")]
        [SerializeField] private string walkParameter = "isWalk";
        private float walkInterval => GameTuning.Instance.player.walkStepInterval;
        private float runInterval => GameTuning.Instance.player.runStepInterval;

        // 파라미터 이름을 매번 문자열로 넘기지 않도록 해시를 미리 구해 둔다.
        private int _movingHash;
        private int _walkHash;

        private float _stepTimer;

        private Player player;

        private void Awake()
        {
            // Player.cs와 같은 방식으로, 연결이 없으면 자식에서 찾는다.
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            player = GetComponent<Player>();

            _movingHash = Animator.StringToHash(movingParameter);
            _walkHash = Animator.StringToHash(walkParameter);
        }

        private void Update()
        {
            if (animator == null)
                return;

            // 멈춰 있으면 발소리 없음. 타이머를 0으로 둬서 다시 움직이는
            // 순간 첫 발소리가 바로 나도록 한다.
            if (!animator.GetBool(_movingHash))
            {
                _stepTimer = 0f;
                return;
            }

            // Walk 버튼을 눌렀으면 걷기, 아니면 뛰기.
            bool walking = animator.GetBool(_walkHash);
            Sound sound = walking ? SoundList.Walking : SoundList.Running;
            float interval = walking ? walkInterval : runInterval;

            _stepTimer -= Time.deltaTime;
            if (_stepTimer <= 0f)
            {
                SoundEmitter.Emit(transform.position, sound, enemyLayer, player.myDimension);
                _stepTimer = interval;
            }
        }
    }
}
