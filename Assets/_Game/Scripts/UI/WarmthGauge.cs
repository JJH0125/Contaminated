using UnityEngine;
using UnityEngine.UI;

namespace Squad
{
    /// <summary>
    /// 횃불을 숨기는 동안 줄어드는 온기 게이지(TorchHolder.Warmth)를 화면에 막대로 보여 준다.
    ///   숨기는 동안, 그리고 게이지가 다 차기 전까지만 보이고, 가득 차면 서서히 사라진다
    ///   절반 아래로 떨어지면 막대가 붉어진다 (화면 가장자리에 오염 결정이 자라기 시작하는 때와 같다)
    ///
    /// 씬 구성:
    ///   따로 할 일이 없다. 씬에 이 게이지가 없으면 TorchHolder가 GameUI 아래에 자동으로 만든다.
    ///   위치를 바꾸고 싶으면 플레이 중에 만들어진 "Warmth Gauge"의 RectTransform을 참고해서,
    ///   GameUI 아래에 같은 이름의 오브젝트를 만들어 이 스크립트를 붙여 두면 그것을 쓴다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class WarmthGauge : MonoBehaviour
    {
        [Header("○ 튜닝 값 — 자유롭게 조절")]
        [Tooltip("게이지가 절반 이상일 때의 색")]
        [SerializeField] private Color warmColor = new Color(0.85f, 0.93f, 1f, 0.9f);
        [Tooltip("게이지가 절반 아래일 때의 색")]
        [SerializeField] private Color coldColor = new Color(0.85f, 0.15f, 0.12f, 0.95f);
        [Tooltip("막대 바탕의 색")]
        [SerializeField] private Color backColor = new Color(0f, 0f, 0f, 0.5f);
        [Tooltip("나타나고 사라지는 데 걸리는 시간(초)")]
        [SerializeField] private float fadeTime = 0.3f;

        // 이 아래로 떨어지면 막대가 붉어진다.
        private const float ColdThreshold = 0.5f;

        private CanvasGroup _group;
        private RectTransform _fill;
        private Image _fillImage;

        /// <summary>
        /// 씬에 게이지가 하나도 없으면 GameUI 아래, 화면 아래쪽 가운데(차원 이동 안내 위)에 하나 만든다.
        /// </summary>
        public static void EnsureExists()
        {
            if (FindObjectOfType<WarmthGauge>(true) != null)
                return;

            GameUI gameUI = FindObjectOfType<GameUI>(true);
            if (gameUI == null)
                return;

            var gaugeObject = new GameObject("Warmth Gauge", typeof(RectTransform));
            RectTransform rect = gaugeObject.GetComponent<RectTransform>();
            rect.SetParent(gameUI.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 120f);
            rect.sizeDelta = new Vector2(360f, 12f);

            gaugeObject.AddComponent<WarmthGauge>();
        }

        private void Awake()
        {
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;

            Image back = gameObject.AddComponent<Image>();
            back.color = backColor;
            back.raycastTarget = false;

            // 채워진 부분은 오른쪽 끝(anchorMax.x)을 게이지 값만큼 당겨서 줄인다.
            var fillObject = new GameObject("Fill", typeof(RectTransform));
            _fill = fillObject.GetComponent<RectTransform>();
            _fill.SetParent(transform, false);
            _fill.anchorMin = Vector2.zero;
            _fill.anchorMax = Vector2.one;
            _fill.offsetMin = new Vector2(2f, 2f);
            _fill.offsetMax = new Vector2(-2f, -2f);
            _fillImage = fillObject.AddComponent<Image>();
            _fillImage.raycastTarget = false;
        }

        private void Update()
        {
            float warmth = TorchHolder.Warmth;
            bool show = TorchHolder.IsHiding || warmth < 1f;

            float step = fadeTime > 0f ? Time.unscaledDeltaTime / fadeTime : 1f;
            _group.alpha = Mathf.MoveTowards(_group.alpha, show ? 1f : 0f, step);

            _fill.anchorMax = new Vector2(warmth, 1f);
            _fillImage.color = warmth < ColdThreshold ? coldColor : warmColor;
        }
    }
}
