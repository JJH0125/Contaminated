using UnityEngine;
using TMPro;

namespace Squad
{
    /// <summary>
    /// 재의 적이 모닥불을 끄고 있으면 화면 위쪽에 알리고, 그 불이 화면의 어느 쪽에 있는지 화살표로 보여 준다.
    /// 다 꺼지면 "모닥불이 꺼졌다"를 잠깐 띄운다. 모르는 사이에 켜진 불 수가 줄어 버그처럼 보이지 않게 하려는 것이다.
    ///
    /// 값은 모닥불(Campfire)에서 매 프레임 읽는다.
    ///
    /// 씬 구성:
    ///   따로 할 일이 없다. 모닥불이 처음 꺼지기 시작할 때 GameUI 아래에 스스로 만들어진다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class FireAlertLabel : MonoBehaviour
    {
        // 꺼졌다는 알림을 띄워 두는 시간(초)
        private const float GoneNoticeTime = 3f;
        private static readonly string[] Arrows = { "→", "↗", "↑", "↖", "←", "↙", "↓", "↘" };

        private static FireAlertLabel _instance;
        private TextMeshProUGUI _label;
        private float _goneUntil;

        /// 모닥불 하나가 다 꺼졌을 때 부른다.
        public static void NotifyExtinguished()
        {
            EnsureExists();
            if (_instance != null)
                _instance._goneUntil = Time.time + GoneNoticeTime;
        }

        /// 없으면 GameUI 아래, 화면 위쪽 가운데에 만든다. (GameUI는 플레이 중에만 켜진다)
        public static void EnsureExists()
        {
            if (_instance != null)
                return;

            GameUI gameUI = FindObjectOfType<GameUI>(true);
            if (gameUI == null)
                return;

            var labelObject = new GameObject("Fire Alert Label", typeof(RectTransform));
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.SetParent(gameUI.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -90f);
            rect.sizeDelta = new Vector2(600f, 50f);
            _instance = labelObject.AddComponent<FireAlertLabel>();
        }

        private void Awake()
        {
            // 한글이 나와야 하므로, 이미 화면에 쓰이고 있는 글자의 글꼴을 빌려 온다.
            TMP_Text sample = transform.parent != null ? transform.parent.GetComponentInChildren<TMP_Text>(true) : null;
            _label = gameObject.AddComponent<TextMeshProUGUI>();
            if (sample != null)
                _label.font = sample.font;
            _label.fontSize = 30f;
            _label.alignment = TextAlignmentOptions.Center;
            _label.enableWordWrapping = false;
            _label.raycastTarget = false;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void Update()
        {
            Campfire fading = null;
            foreach (Campfire campfire in Campfire.All)
                if (campfire != null && campfire.IsBeingExtinguished)
                    fading = campfire;

            if (fading != null)
            {
                _label.text = $"모닥불이 꺼지고 있다 {ArrowTo(fading.transform.position)} {fading.ExtinguishProgress * 100f:0}%";
                _label.color = new Color(1f, 0.55f, 0.25f);
            }
            else if (Time.time < _goneUntil)
            {
                _label.text = "모닥불이 꺼졌다";
                _label.color = new Color(0.75f, 0.75f, 0.75f);
            }
            else
            {
                _label.text = "";
            }
        }

        // 화면 한가운데에서 본 그 자리의 방향 (8방향 화살표)
        private static string ArrowTo(Vector3 world)
        {
            Camera cam = Camera.main;
            if (cam == null)
                return "";

            Vector3 screen = cam.WorldToScreenPoint(world);
            Vector2 offset = new Vector2(screen.x - Screen.width * 0.5f, screen.y - Screen.height * 0.5f);
            // 카메라 뒤쪽이면 화면에서 반대로 보이므로 뒤집는다.
            if (screen.z < 0f)
                offset = -offset;

            float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
            int index = Mathf.RoundToInt(Mathf.Repeat(angle, 360f) / 45f) % 8;
            return Arrows[index];
        }
    }
}
