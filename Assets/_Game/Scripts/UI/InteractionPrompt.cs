using UnityEngine;
using TMPro;

namespace Squad
{
    /// <summary>
    /// 화면에 뜨는 공용 상호작용 안내 문구("[E] 돌 줍기" 등)를 관리한다.
    ///
    /// 씬에 하나만 두고, 상호작용 가능한 오브젝트들(돌, 모닥불, 출구 등)이
    /// Show()/Hide()를 호출해서 함께 쓴다. 안내 문구는 한 번에 하나만
    /// 보이면 되므로 블랙보드처럼 싱글톤으로 둔다.
    ///
    /// 씬 구성:
    ///   Canvas > Text(TMP) 를 만들고, 이 스크립트를 아무 오브젝트(또는
    ///   Canvas 자신)에 붙인 뒤 promptText에 그 텍스트를 연결한다.
    /// </summary>
    public class InteractionPrompt : MonoBehaviour
    {
        public static InteractionPrompt Instance { get; private set; }

        [Header("■ 필수 연결 — 비워두면 에러")]
        [Tooltip("화면에 안내 문구를 표시할 UI 텍스트")]
        [SerializeField] private TMP_Text promptText;

        // 지금 이 문구를 띄운 주인. 여러 오브젝트가 겹칠 때
        // "내가 띄운 문구만 내가 내린다"를 보장하기 위해 기억한다.
        private object _owner;

        private void Awake()
        {
            Instance = this;
            HideImmediate();
        }

        /// <summary>안내 문구를 띄운다. owner는 호출한 쪽(보통 this).</summary>
        public void Show(object owner, string message)
        {
            _owner = owner;
            if (promptText == null)
                return;

            promptText.text = message;
            promptText.gameObject.SetActive(true);
        }

        /// <summary>
        /// 안내 문구를 내린다. 다른 오브젝트가 이미 문구를 바꿔 띄웠다면
        /// 무시한다(내가 띄운 게 아니면 내리지 않는다).
        /// </summary>
        public void Hide(object owner)
        {
            if (_owner != owner)
                return;

            HideImmediate();
        }

        /// <summary>
        /// 누가 띄웠는지와 관계없이 안내 문구를 내린다.
        /// Stage가 통째로 파괴될 때처럼 주인이 Hide를 부를 수 없는 경우에 쓴다.
        /// </summary>
        public void HideAll() => HideImmediate();

        // owner를 검사하지 않고 즉시 프롬프트를 비활성화
        private void HideImmediate()
        {
            _owner = null;
            if (promptText != null)
                promptText.gameObject.SetActive(false);
        }
    }
}
