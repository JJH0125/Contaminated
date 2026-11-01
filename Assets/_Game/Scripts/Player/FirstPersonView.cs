using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 1인칭 시점. Fake 차원에 들어가면 저절로 켜지고, Real로 돌아오면 쿼터뷰로 돌아온다.
    /// "그 안에 갇혀 버린 느낌"을 주는 시험 중인 연출이라, GameTuning의 dimension.fakeFirstPerson으로 끌 수 있다.
    /// F3을 누르면 차원과 상관없이 1인칭을 켜고 끌 수 있다. (둘러보기용)
    ///
    /// 켜 있는 동안에는
    ///   - 카메라가 플레이어 눈높이에서 마우스 방향을 본다. 쿼터뷰와 사이는 부드럽게 옮겨 간다 (Follow)
    ///   - 이동 키는 바라보는 방향 기준으로 움직이고, 몸도 그 방향을 본다. 돌도 그쪽으로 던진다 (Player)
    ///   - 마우스 커서를 숨긴다. 일시정지 화면 등에서는 다시 나타난다
    ///
    /// 조작이 뒤집히지 않도록 넘어가는 순간과 돌아오는 순간을 맞춘다.
    ///   켜질 때   캐릭터가 보던 방향을 바라본다. 화면이 바뀌어도 "앞"은 그대로다
    ///   꺼질 때   캐릭터는 1인칭에서 마지막으로 바라본 방향을 본다
    ///   옮겨 가는 동안  이동은 막지 않는다. 들어갈 때는 다 옮겨 간 순간에 이동 키가 시선 기준으로 바뀌고,
    ///                   나올 때는 곧바로 화면 기준으로 돌아온다
    /// 전환은 차원을 넘는 순간 시작하므로, Fake에 들어갈 때 퍼지는 오염의 물결과 함께 일어난다.
    ///
    /// 씬이나 프리팹에 붙일 필요가 없다. 게임이 시작될 때 스스로 만들어진다.
    /// </summary>
    public class FirstPersonView : MonoBehaviour
    {
        private const KeyCode ToggleKey = KeyCode.F3;
        // 눈높이: 플레이어 오브젝트 위치(발에서 1m 위)에서 위로 잰 거리(m). 모델의 키가 약 6.3m다.
        private const float EyeHeight = 4.8f;
        // 눈을 몸 앞쪽으로 내어 두는 거리(m). 내려다볼 때 어깨와 가슴이 시야를 막지 않게 한다.
        private const float EyeForward = 0.5f;
        private const float MaxPitch = 85f;

        /// 1인칭을 향하고 있는가. (옮겨 가는 중에도 true)
        public static bool On { get; private set; }

        /// 쿼터뷰와 1인칭 사이에서 카메라가 어디쯤인가 (0: 쿼터뷰, 1: 1인칭)
        public static float Blend { get; private set; }

        /// 이동 키와 몸의 방향이 1인칭 기준인가. 카메라가 다 옮겨 간 순간에 바뀐다.
        public static bool ControlsFirstPerson { get; private set; }

        private static float _yaw;
        private static float _pitch;

        /// 바라보는 방향. 위아래를 뺀 몸의 방향과, 카메라의 방향.
        public static Quaternion BodyRotation => Quaternion.Euler(0f, _yaw, 0f);
        public static Quaternion ViewRotation => Quaternion.Euler(_pitch, _yaw, 0f);

        /// 플레이어 위치에서 눈까지의 거리
        public static Vector3 EyeOffset => Vector3.up * EyeHeight + BodyRotation * Vector3.forward * EyeForward;

        // F3으로 직접 켰는가
        private bool _forced;
        private GUIStyle _labelStyle;

        // 게임이 시작되고 첫 씬이 열린 직후에 Unity가 한 번 불러 준다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            On = false;
            Blend = 0f;
            ControlsFirstPerson = false;
            var viewObject = new GameObject("First Person View");
            viewObject.AddComponent<FirstPersonView>();
            // Stage가 라운드마다 바뀌어도 이 오브젝트는 남아 있어야 한다.
            DontDestroyOnLoad(viewObject);
        }

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey))
                _forced = !_forced;

            Player player = DimensionController.Instance != null ? DimensionController.Instance.player : null;
            bool inFake = player != null && player.myDimension == Dimension.Fake;
            bool wanted = _forced || (inFake && GameTuning.Instance.dimension.fakeFirstPerson);

            if (wanted != On)
                SetOn(wanted, player);

            float step = Time.unscaledDeltaTime / Mathf.Max(GameTuning.Instance.dimension.firstPersonBlendTime, 0.01f);
            Blend = Mathf.MoveTowards(Blend, On ? 1f : 0f, step);
            // 들어갈 때는 카메라가 다 내려간 뒤에 시선 기준으로 바꾸고, 나올 때는 곧바로 화면 기준으로 되돌린다.
            // (나온 뒤에도 시선 기준을 유지하면 Real 화면에서 키와 다른 방향으로 걸어가 고장처럼 느껴진다)
            if (!On)
                ControlsFirstPerson = false;
            else if (Blend >= 1f)
                ControlsFirstPerson = true;

            if (!On)
                return;

            bool playing = Time.timeScale > 0f &&
                           (GameManager.Instance == null || GameManager.Instance.CurrentState == GameState.Playing);
            Cursor.lockState = playing ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !playing;
            if (!playing)
                return;

            // 아직 옮겨 가는 중이면 이동 키가 쿼터뷰 기준이라 몸이 가는 쪽으로 돈다. 시선은 그 몸을 따라간다.
            if (!ControlsFirstPerson)
            {
                if (player != null)
                    _yaw = player.transform.eulerAngles.y;
                return;
            }

            float sensitivity = GameTuning.Instance.dimension.firstPersonMouseSensitivity;
            _yaw += Input.GetAxisRaw("Mouse X") * sensitivity;
            _pitch = Mathf.Clamp(_pitch - Input.GetAxisRaw("Mouse Y") * sensitivity, -MaxPitch, MaxPitch);
        }

        private void SetOn(bool on, Player player)
        {
            On = on;
            if (on)
            {
                // 캐릭터가 보던 방향에서 시작한다. 캐릭터가 없으면 지금 카메라가 보는 방향에서.
                Transform facing = player != null ? player.transform : Camera.main != null ? Camera.main.transform : null;
                _yaw = facing != null ? facing.eulerAngles.y : 0f;
                _pitch = 0f;
                return;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnGUI()
        {
            if (!_forced)
                return;

            _labelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 16, normal = { textColor = Color.yellow } };
            GUI.Label(new Rect(12f, 36f, 400f, 30f), "1인칭 (F3으로 끄기)", _labelStyle);
        }
    }
}
