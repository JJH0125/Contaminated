using TMPro;
using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 라운드가 시작될 때 화면 가운데에 라운드 번호와 목표를 잠깐 띄운다.
    ///   1라운드
    ///   모닥불 3개를 켜고 출구로
    /// 나타났다가(bannerFadeIn) 머문 뒤(bannerHold) 사라진다(bannerFadeOut). 수치는 GameTuning > Title.
    ///
    /// GameManager.RoundStartCount가 바뀌는 것을 보고 뜬다. 켜야 할 모닥불 수는 Stage가 배치를 마친 뒤에야
    /// 정해지므로, bannerDelay만큼 기다렸다가 그때의 값을 읽는다.
    ///
    /// 씬 구성:
    ///   Tools > 메뉴 UI 꾸미기가 GameUI 아래에 "RoundBanner"를 만들어 붙인다. (글꼴: Black Han Sans)
    ///   GameUI는 플레이 중에만 켜지므로 다른 화면에서는 보이지 않는다.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class RoundBanner : MonoBehaviour
    {
        [Tooltip("첫 줄. {0}에 라운드 번호가 들어간다")]
        [SerializeField] private string roundFormat = "{0}라운드";
        [Tooltip("마지막 라운드의 첫 줄")]
        [SerializeField] private string lastRoundText = "마지막 라운드";
        [Tooltip("둘째 줄. {0}에 켜야 할 모닥불 수가 들어간다")]
        [SerializeField] private string goalFormat = "모닥불 {0}개를 켜고 출구로";
        [Tooltip("둘째 줄의 크기(첫 줄에 대한 비율, %)")]
        [SerializeField] private int goalSizePercent = 50;

        /// 이 문구에 쓰일 수 있는 글자. 글꼴 에셋에 미리 그려 둘 때 쓴다. (MenuStyleTool)
        public string Characters => roundFormat + lastRoundText + goalFormat + "0123456789";

        private TMP_Text _text;
        private int _seenRound = -1;
        // 문구가 뜨기 시작한 시간. 뜨지 않았으면 음수.
        private float _shownAt = -1f;
        private bool _composed;

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
            _text.alpha = 0f;
        }

        private void Update()
        {
            GameManager manager = GameManager.Instance;
            if (manager == null)
                return;

            if (manager.RoundStartCount != _seenRound)
            {
                _seenRound = manager.RoundStartCount;
                _shownAt = Time.time;
                _composed = false;
                _text.alpha = 0f;
            }
            if (_shownAt < 0f)
                return;

            TitleTuning tuning = GameTuning.Instance.title;
            float elapsed = Time.time - _shownAt - tuning.bannerDelay;
            if (elapsed < 0f)
                return;
            // 기다린 뒤 처음 한 번만 글을 채운다. (그때는 모닥불 배치가 끝나 있다)
            if (!_composed)
            {
                _text.text = Compose(manager);
                _composed = true;
            }

            float fadeOutStart = tuning.bannerFadeIn + tuning.bannerHold;
            float alpha;
            if (elapsed < tuning.bannerFadeIn)
                alpha = elapsed / Mathf.Max(tuning.bannerFadeIn, 0.01f);
            else if (elapsed < fadeOutStart)
                alpha = 1f;
            else
                alpha = 1f - (elapsed - fadeOutStart) / Mathf.Max(tuning.bannerFadeOut, 0.01f);

            _text.alpha = Mathf.Clamp01(alpha);
            if (alpha <= 0f && elapsed >= fadeOutStart)
                _shownAt = -1f;
        }

        private string Compose(GameManager manager)
        {
            string round = manager.CurrentRound >= manager.TotalRounds && manager.TotalRounds > 1
                ? lastRoundText
                : string.Format(roundFormat, manager.CurrentRound);
            string goal = string.Format(goalFormat, manager.RequiredCampfires);
            return $"{round}\n<size={goalSizePercent}%>{goal}</size>";
        }
    }
}
