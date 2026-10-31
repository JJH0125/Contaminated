using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 디버그용. 플레이 중에 F3을 누르면 1인칭 시점으로 맵을 둘러본다. 다시 누르면 쿼터뷰로 돌아간다.
    ///
    /// 켜 있는 동안에는
    ///   - 카메라가 플레이어 눈높이에서 마우스 방향을 본다 (Follow)
    ///   - 이동 키는 바라보는 방향 기준으로 움직이고, 몸도 그 방향을 본다. 돌도 그쪽으로 던진다 (Player)
    ///   - 몸은 그대로 그린다. 눈이 머리 안에 있어도 면의 안쪽은 그려지지 않으므로 앞이 가리지 않고, 내려다보면 몸이 보인다
    ///   - 쿼터뷰와 1인칭 사이는 카메라가 부드럽게 옮겨 간다 (Follow)
    ///   - 마우스 커서를 숨긴다. 일시정지 화면 등에서는 다시 나타난다
    /// 라운드가 바뀌어도 켠 상태가 이어진다. 잡혔을 때의 연출은 그대로 나온다.
    ///
    /// 씬이나 프리팹에 붙일 필요가 없다. 게임이 시작될 때 스스로 만들어진다.
    /// 필요 없어지면 이 파일과, Follow·Player에서 On을 읽는 부분만 지우면 된다.
    /// </summary>
    public class FirstPersonDebug : MonoBehaviour
    {
        private const KeyCode ToggleKey = KeyCode.F3;
        // 눈높이: 플레이어 오브젝트 위치(발에서 1m 위)에서 위로 잰 거리(m). 모델의 키가 약 6.3m다.
        private const float EyeHeight = 4.8f;
        // 눈을 몸 앞쪽으로 내어 두는 거리(m). 내려다볼 때 어깨와 가슴이 시야를 막지 않게 한다.
        private const float EyeForward = 0.5f;
        private const float MouseSensitivity = 2f;
        private const float MaxPitch = 85f;

        /// 지금 1인칭이 켜져 있는가.
        public static bool On { get; private set; }

        private static float _yaw;
        private static float _pitch;

        /// 바라보는 방향. 위아래를 뺀 몸의 방향과, 카메라의 방향.
        public static Quaternion BodyRotation => Quaternion.Euler(0f, _yaw, 0f);
        public static Quaternion ViewRotation => Quaternion.Euler(_pitch, _yaw, 0f);

        /// 플레이어 위치에서 눈까지의 거리
        public static Vector3 EyeOffset => Vector3.up * EyeHeight + BodyRotation * Vector3.forward * EyeForward;

        private GUIStyle _labelStyle;

        // 게임이 시작되고 첫 씬이 열린 직후에 Unity가 한 번 불러 준다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            On = false;
            var debugObject = new GameObject("First Person Debug");
            debugObject.AddComponent<FirstPersonDebug>();
            // Stage가 라운드마다 바뀌어도 이 오브젝트는 남아 있어야 한다.
            DontDestroyOnLoad(debugObject);
        }

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey))
                Toggle();

            if (!On)
                return;

            bool playing = Time.timeScale > 0f &&
                           (GameManager.Instance == null || GameManager.Instance.CurrentState == GameState.Playing);
            Cursor.lockState = playing ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !playing;
            if (!playing)
                return;

            _yaw += Input.GetAxisRaw("Mouse X") * MouseSensitivity;
            _pitch = Mathf.Clamp(_pitch - Input.GetAxisRaw("Mouse Y") * MouseSensitivity, -MaxPitch, MaxPitch);
        }

        private void Toggle()
        {
            On = !On;
            if (On)
            {
                // 지금 보던 방향에서 시작한다.
                _yaw = Camera.main != null ? Camera.main.transform.eulerAngles.y : 0f;
                _pitch = 0f;
                return;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnGUI()
        {
            if (!On)
                return;

            _labelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 16, normal = { textColor = Color.yellow } };
            GUI.Label(new Rect(12f, 36f, 400f, 30f), "1인칭 (F3으로 끄기)", _labelStyle);
        }
    }
}
