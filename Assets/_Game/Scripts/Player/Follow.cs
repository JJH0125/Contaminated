using System.Collections;
using System.Collections.Generic;
using UnityEngine;

///<summary>
/// 카메라에 붙일 스크립트. 플레이어가 생성되면 쿼터뷰 형태로 플레이어를 비춘다.
///
/// 카메라의 회전과 위치를 각도·거리 값으로부터 함께 계산하므로,
/// 각도를 바꿔도 플레이어가 항상 화면 정중앙에 온다.
/// (회전과 offset을 따로 맞추면 둘이 어긋나 플레이어가 중앙에서 벗어나기 쉽다)
///
/// 플레이어가 움직이는 동안에는 가는 방향 쪽으로 카메라를 조금 밀어서, 앞쪽이 더 넓게 보이게 한다.
/// 방향을 바꾸거나 멈춰도 화면이 휙 넘어가지 않도록, 밀어 둔 정도는 서서히 따라간다.
///</summary>
public class Follow : MonoBehaviour
{
    // 수치는 GameTuning 에셋(Resources/GameTuning)에서 읽는다.
    private float pitch => Squad.GameTuning.Instance.camera.pitch;
    private float yaw => Squad.GameTuning.Instance.camera.yaw;
    private float distance => Squad.GameTuning.Instance.camera.distance;
    private float lookAheadTime => Squad.GameTuning.Instance.camera.lookAheadTime;
    private float maxLookAhead => Squad.GameTuning.Instance.camera.maxLookAhead;
    private float lookAheadSmoothTime => Squad.GameTuning.Instance.camera.lookAheadSmoothTime;

    private Transform target;

    // 지금 카메라를 밀어 둔 정도와, 그것을 서서히 바꾸는 데 쓰는 값들
    private Vector3 lookAhead;
    private Vector3 lookAheadVelocity;
    private Vector3 lastTargetPosition;

    // 클로즈업 중에는 플레이어를 따라가지 않는다.
    private bool isCloseUp;
    private Camera cam;
    private float originalFieldOfView;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    /// <summary>
    /// Update에서 바뀐 target의 position을
    /// LateUpdate에서 안전하게 참조하여
    /// 카메라의 위치를 바꾼다.
    /// 이를 통해 카메라가 떨리는 것을 방지할 수 있다.
    /// </summary>
    void LateUpdate()
    {
        if (target == null || isCloseUp)
            return;

        UpdateLookAhead();

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        transform.rotation = rotation;
        // 카메라가 바라보는 방향(rotation * forward)의 반대쪽으로 distance만큼 물러난 곳에 둔다.
        transform.position = target.position + lookAhead - rotation * Vector3.forward * distance;
    }

    // 플레이어가 이번 프레임에 움직인 만큼으로 속도를 구하고, 그 방향으로 밀어 둘 정도를 서서히 맞춘다.
    void UpdateLookAhead()
    {
        // 일시정지 중에는 시간이 흐르지 않으므로 그대로 둔다.
        if (Time.deltaTime <= 0f)
            return;

        // 점프로 위아래로 움직이는 것은 빼고, 땅 위에서 움직인 것만 본다.
        Vector3 moved = target.position - lastTargetPosition;
        moved.y = 0f;
        lastTargetPosition = target.position;

        // 속도에 비례해서 밀기 때문에, 걸을 때는 조금만, 달릴 때는 더 많이 앞을 비춘다.
        Vector3 desired = Vector3.ClampMagnitude(moved / Time.deltaTime * lookAheadTime, maxLookAhead);
        lookAhead = Vector3.SmoothDamp(lookAhead, desired, ref lookAheadVelocity, lookAheadSmoothTime);
    }

    public void SetTarget(Transform player)
    {
        target = player;

        // 새 라운드의 플레이어는 다른 자리에서 시작한다.
        // 그 차이를 "움직였다"로 읽어 카메라가 튀지 않도록, 밀어 둔 정도를 처음으로 되돌린다.
        lastTargetPosition = player.position;
        lookAhead = Vector3.zero;
        lookAheadVelocity = Vector3.zero;
    }

    /// <summary>
    /// 쿼터뷰를 잠시 멈추고, 지정한 자리에서 지정한 방향을 비춘다.
    /// 적에게 잡혔을 때 적의 얼굴을 화면 가득 보여주는 데 쓴다.
    /// EndCloseUp을 부르면 시야각이 원래대로 돌아가고 다시 플레이어를 따라간다.
    /// </summary>
    public void BeginCloseUp(Vector3 position, Quaternion rotation, float fieldOfView)
    {
        // 클로즈업이 겹쳐 불려도 원래 시야각을 잃지 않도록 처음 한 번만 기억한다.
        if (!isCloseUp)
            originalFieldOfView = cam.fieldOfView;

        isCloseUp = true;
        cam.fieldOfView = fieldOfView;
        transform.SetPositionAndRotation(position, rotation);
    }

    public void EndCloseUp()
    {
        if (!isCloseUp)
            return;

        isCloseUp = false;
        cam.fieldOfView = originalFieldOfView;
    }
}
