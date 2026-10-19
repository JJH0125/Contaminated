using UnityEngine;

/// <summary>
/// 플레이어 이동. CharacterController를 써서 벽에 막히고, 경사·계단을
/// 오르고, 중력을 처리한다. (점프는 없다)
///
/// CharacterController는 물리 시뮬레이션에 참여하지 않으므로 중력이
/// 자동으로 적용되지 않는다. 그래서 Y_velocity(수직 속도)를
/// 직접 관리한다:
///   - 매 프레임 중력만큼 아래로 가속
///   - 바닥에 닿으면 0 근처로 리셋(계속 아래로 쌓이지 않게)
/// 실제 물리의 "가속도 → 속도 → 위치" 흐름을 손으로 구현하는 셈이다.
/// </summary>

[RequireComponent(typeof(CharacterController))]
public class Player : MonoBehaviour
{
    [Header("○ 튜닝 값 — 자유롭게 조절")]
    [Tooltip("달리는 속도 (Shift를 누르고 있을 때)")]
    [SerializeField] private float moveSpeed = 15f;
    [Tooltip("걷는 속도 (기본)")]
    [SerializeField] private float walkSpeed = 7.5f;
    [Tooltip("중력 가속도(음수)")]
    [SerializeField] private float gravity = -20f;

    private Vector3 cameraForward;
    private Vector3 cameraRight;

    private CharacterController controller;
    private Animator animator;

    // x와 z축은 키보드의 입력을 받아 결정되는 입력값
    // y축은 수학적으로 계산되는 속도의 값
    private float X_Axis;
    private float Y_velocity;
    private float Z_Axis;
    private Vector3 horizontal;
    private bool isWalk;

    /// <summary>지금 달리고 있는가 (움직이는 중이고 걷기가 아님). 횃불 게이지가 더 빨리 닳는 데 쓴다.</summary>
    public bool IsRunning => !isWalk && horizontal != Vector3.zero;

    /// <summary>
    /// true인 동안 이동과 회전을 멈춘다. 돌을 줍는 동작처럼 제자리에서 해야 하는 동작이 켜고 끈다(StoneHolder).
    /// 중력은 계속 받는다.
    /// </summary>
    public bool MovementLocked { get; set; }
    /// 플레이어가 속한 차원. 게임 시작 시 항상 Real 차원.
    public Dimension myDimension { get; private set; } = Dimension.Real;
    private Transform cameraTransform;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (cameraTransform == null)
            cameraTransform = Camera.main.transform;

        Follow follow = cameraTransform.GetComponent<Follow>();
        if (follow != null)
            follow.SetTarget(transform);

        /// 쿼터뷰 느낌을 살리기 위해 카메라를 y축 기준 45도로 돌렸기 때문에,
        /// 방향키 입력에 따른 움직임도 카메라를 따라 돌려야 한다.
        /// 마우스로 카메라를 회전시키는 기능을 넣지 않기로 했으므로
        /// 이 작업을 Awake에 해도 무방하지만,
        /// 만약 기능을 넣게 된다면 실시간으로 카메라의 축을 받아야 하기 때문에
        /// Move() 함수로 이 내용을 옮겨야 한다.
        cameraForward = cameraTransform.forward;
        cameraRight = cameraTransform.right;
        
        cameraForward.y = 0f;
        cameraRight.y = 0f;
        cameraForward.Normalize();
        cameraRight.Normalize();
    }

    void Update()
    {
        /// 일시정지 중에는 입력을 받지 않는다.
        /// Move는 deltaTime이 0이라 멈추지만, Turn은 회전값을 바로 넣기 때문에
        /// 막지 않으면 일시정지 화면에서도 방향키로 캐릭터가 돌아간다.
        if (Time.timeScale == 0f)
            return;

        GetInput();
        Move();
        Turn();
    }

    // 이동에 필요한 키를 입력받는다
    void GetInput()
    {
        X_Axis = Input.GetAxisRaw("Horizontal");
        Z_Axis = Input.GetAxisRaw("Vertical");
        // 기본은 걷기이고, Walk 버튼(왼쪽 Shift)을 누르고 있는 동안만 달린다.
        // 횃불을 들고 있으면 뛸 수 없다. 횃불을 숨긴 상태(TorchHolder)에서만 Shift로 달릴 수 있다.
        isWalk = !(Input.GetButton("Walk") && Squad.TorchHolder.IsHiding);
    }

    /// 1. 입력받은 x와 z, Walk키에 따라 수평 속도를 결정한다
    /// 2. 최종 결정된 속도로 캐릭터를 한 프레임 이동시킨다
    /// 3. 이동 후 캐릭터가 공중에 떠있다면(비탈에서 떨어질 때 등) y 속도를 감소시키고
    ///    착지했다면 그에 맞게 처리한다
    /// 4. 애니메이션을 상황에 맞게 적절히 적용한다
    void Move()
    {
        horizontal = (cameraRight * X_Axis + cameraForward * Z_Axis).normalized;
        horizontal *= isWalk ? walkSpeed : moveSpeed;
        if (MovementLocked)
            horizontal = Vector3.zero;

        Vector3 velocity = horizontal + Vector3.up * Y_velocity;
        controller.Move(velocity * Time.deltaTime);
        
        if (!controller.isGrounded)
            Y_velocity += gravity * Time.deltaTime;
        /// isGrounded가 true이면서 속도가 음수이다
        /// = 착지했다
        else if (Y_velocity < 0f)
            /// 착지 후 미세하게 떠있는 것을 방지
            /// 안정적으로 땅에 붙어있을 수 있게 한다
            Y_velocity = -2f;
        
        animator.SetBool("isRun", horizontal != Vector3.zero);
        animator.SetBool("isWalk", isWalk);
        animator.SetBool("isInTheAir", !controller.isGrounded);
    }

    // 움직이고 있는 방향을 바라본다
    // void Turn()
    // {
    //     // 움직이지 않았다면 Turn을 실행하지 않는다
    //     if (horizontal == Vector3.zero)
    //         return;

    //     /// 참고 : horizontal은 speed가 곱해진 값이다
    //     /// 즉 플레이어가 멀리 있는 지점을 바라보기 때문에
    //     /// 경사를 오르는 상황에서는 예기치 못한 오류가 발생할 수 있다.
    //     /// 버그가 발생한다면 speed가 곱해지기 전의 값을 가져올 것
    //     transform.LookAt(transform.position + horizontal);
    // }

    void Turn()
    {
        // 입력이 없으면 회전하지 않음 (horizontal이 아니라 입력을 직접 확인)
        if (MovementLocked || (X_Axis == 0f && Z_Axis == 0f))
            return;

        Vector3 lookDir = horizontal;
        lookDir.y = 0f;                          // y 성분 확실히 제거
        if (lookDir.sqrMagnitude < 0.0001f)      // 0벡터 방어
            return;

        transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);
    }

    /// 현재 플레이어의 차원을 다른 차원으로 변경
    public void SwitchMyDimension()
    {
        if (myDimension == Dimension.Real)
            myDimension = Dimension.Fake;
        else
            myDimension = Dimension.Real;
    }
}
