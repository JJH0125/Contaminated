using UnityEngine;
using TMPro;

namespace Squad
{
    /// <summary>
    /// 차원 이동을 지금 쓸 수 있는지 화면에 알려 준다.
    ///   쓸 수 있을 때     "[F] 차원 이동"
    ///   대기 중일 때      "차원 이동 7초" (남은 시간, 흐린 색)
    ///   Fake에 있을 때    "Real로 돌아가기까지 5초" (튕겨 나오기까지 남은 시간)
    ///
    /// 값은 DimensionController에서 매 프레임 읽어 온다.
    /// 컨트롤러는 Stage와 함께 라운드마다 새로 만들어지지만 이 안내는 Canvas에 계속 남아 있으므로,
    /// 컨트롤러를 붙잡아 두지 않고 그때그때 지금 있는 것을 찾아 읽는다.
    ///
    /// 씬 구성:
    ///   따로 할 일이 없다. 씬에 이 안내가 없으면 DimensionController가 GameUI 아래에 자동으로 만든다.
    ///   위치나 색을 바꾸고 싶으면, Canvas의 GameUI 아래에 빈 UI 오브젝트를 만들어 이 스크립트를 직접 붙이면
    ///   자동으로 만들지 않고 그것을 쓴다. (위치는 그 오브젝트의 RectTransform으로 정한다)
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class DimensionShiftLabel : MonoBehaviour
    {
        [Header("○ 튜닝 값 — 자유롭게 조절")]
        [Tooltip("이동할 수 있을 때의 문구. {0}=이동 키")]
        [SerializeField] private string readyFormat = "[{0}] 차원 이동";
        [Tooltip("대기 중일 때의 문구. {0}=남은 시간(초)")]
        [SerializeField] private string cooldownFormat = "차원 이동 {0:0}초";
        [Tooltip("Fake에 있을 때의 문구. {0}=Real로 튕겨 나오기까지 남은 시간(초)")]
        [SerializeField] private string fakeStayFormat = "Real로 돌아가기까지 {0:0}초";
        [Tooltip("Fake에 있을 때의 글자 색")]
        [SerializeField] private Color fakeStayColor = new Color(0.75f, 0.85f, 1f);
        [Tooltip("이동할 수 있을 때의 글자 색")]
        [SerializeField] private Color readyColor = Color.white;
        [Tooltip("대기 중일 때의 글자 색")]
        [SerializeField] private Color cooldownColor = new Color(1f, 1f, 1f, 0.4f);
        [Tooltip("글자 크기")]
        [SerializeField] private float fontSize = 28f;

        private TextMeshProUGUI _label;

        /// <summary>
        /// 씬에 이 안내가 하나도 없으면 GameUI 아래, 화면 아래쪽 가운데에 하나 만든다.
        /// GameUI는 플레이 중에만 켜지므로, 그 아래에 두면 다른 화면에서는 보이지 않는다.
        /// </summary>
        public static void EnsureExists()
        {
            // 꺼져 있는 것까지 찾아야, 다른 화면이 떠 있는 동안에도 중복으로 만들지 않는다.
            if (FindObjectOfType<DimensionShiftLabel>(true) != null)
                return;

            GameUI gameUI = FindObjectOfType<GameUI>(true);
            if (gameUI == null)
                return;

            var labelObject = new GameObject("Dimension Shift Label", typeof(RectTransform));
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.SetParent(gameUI.transform, false);
            // 화면 아래쪽 가운데를 기준점으로 삼고, 거기서 조금 위에 둔다.
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 60f);
            rect.sizeDelta = new Vector2(400f, 50f);

            labelObject.AddComponent<DimensionShiftLabel>();
        }

        private void Awake()
        {
            _label = GetComponent<TextMeshProUGUI>();
            if (_label != null)
                return;

            // 한글이 나와야 하므로, 이미 화면에 쓰이고 있는 글자의 글꼴을 빌려 온다.
            // (TextMeshPro의 기본 글꼴에는 한글이 없다)
            TMP_Text sample = transform.parent != null ? transform.parent.GetComponentInChildren<TMP_Text>(true) : null;

            _label = gameObject.AddComponent<TextMeshProUGUI>();
            if (sample != null)
                _label.font = sample.font;
            _label.fontSize = fontSize;
            _label.alignment = TextAlignmentOptions.Center;
            _label.enableWordWrapping = false;
            // 표시만 하는 글자라 클릭을 가로채지 않게 한다.
            _label.raycastTarget = false;
        }

        private void Update()
        {
            DimensionController controller = DimensionController.Instance;
            if (controller == null)
            {
                _label.text = "";
                return;
            }

            // Fake에 있으면 언제 튕겨 나올지를 먼저 보여 준다.
            float fakeRemaining = controller.FakeTimeRemaining;
            if (fakeRemaining > 0f)
            {
                _label.text = string.Format(fakeStayFormat, Mathf.Ceil(fakeRemaining));
                _label.color = fakeStayColor;
                return;
            }

            float remaining = controller.CooldownRemaining;
            if (remaining > 0f)
            {
                // 남은 시간은 올림해서 보여 준다. 0.3초 남았는데 "0초"로 보이면 쓸 수 있는 것처럼 읽힌다.
                _label.text = string.Format(cooldownFormat, Mathf.Ceil(remaining));
                _label.color = cooldownColor;
            }
            else
            {
                _label.text = string.Format(readyFormat, controller.ShiftKey);
                _label.color = readyColor;
            }
        }
    }
}
