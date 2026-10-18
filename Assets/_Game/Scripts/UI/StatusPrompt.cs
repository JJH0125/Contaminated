using UnityEngine;
using TMPro;

namespace Squad
{
    /// <summary>
    /// 화면 오른쪽 위에 현재 진행 상황(라운드, 켜진 발전기 수)을 상시 표시한다.
    ///
    /// 이 클래스는 "받은 숫자를 표시"하는 일만 한다. 값을 세거나 판정하는 것은
    /// GameManager의 몫이며, 값이 바뀌었을 때 GameManager가 이쪽으로 알려 준다
    /// (밀어주기 방식). 매 프레임 조회하지 않으므로 불필요한 문자열 조립이 없다.
    ///
    /// 씬 구성:
    ///   Canvas > 오른쪽 위에 Text(TMP) 두 개를 만들고(라운드용, 발전기용),
    ///   이 스크립트를 붙인 뒤 두 텍스트를 연결한다.
    ///   GameManager의 gameUI 아래에 두면 Playing 상태에서만 표시된다.
    /// </summary>
    public class StatusPrompt : MonoBehaviour
    {
        [Header("■ 필수 연결 — 비워두면 에러")]
        [Tooltip("라운드 진행 상황을 표시할 UI 텍스트")]
        [SerializeField] private TMP_Text roundText;
        [Tooltip("발전기 진행 상황을 표시할 UI 텍스트")]
        [SerializeField] private TMP_Text generatorText;

        [Header("○ 튜닝 값 — 자유롭게 조절")]
        [Tooltip("라운드 표시 형식. {0}=현재 라운드, {1}=전체 라운드")]
        [SerializeField] private string roundFormat = "라운드 {0} / {1}";
        [Tooltip("발전기 표시 형식. {0}=켜진 수, {1}=전체 수")]
        [SerializeField] private string generatorFormat = "발전기 {0} / {1}";
        [Tooltip("발전기를 모두 켰을 때 표시할 문구")]
        [SerializeField] private string exitOpenMessage = "탈출하세요!";

        [Tooltip("평소 발전기 문구 색")]
        [SerializeField] private Color normalColor = Color.white;
        [Tooltip("탈출구가 열렸을 때 발전기 문구 색")]
        [SerializeField] private Color exitOpenColor = new Color(1f, 0.85f, 0.3f);

        /// <summary>
        /// 라운드 표시를 갱신한다.
        /// </summary>
        public void SetRound(int current, int total)
        {
            if (roundText == null)
                return;

            roundText.text = string.Format(roundFormat, current, total);
        }

        /// <summary>
        /// 발전기 표시를 갱신한다. 전부 켜졌으면 탈출 안내로 바뀐다.
        /// </summary>
        public void SetGenerators(int activated, int total)
        {
            if (generatorText == null)
                return;

            // 전부 켜졌으면 숫자 대신 탈출 안내를 띄운다.
            // (숫자는 이미 다 채웠으므로 다음 목표를 알려 주는 편이 낫다)
            // 발전기가 없는 라운드(지금의 기본)에는 처음부터 탈출 안내를 띄운다.
            if (activated >= total)
            {
                generatorText.text = exitOpenMessage;
                generatorText.color = exitOpenColor;
            }
            else
            {
                generatorText.text = string.Format(generatorFormat, activated, total);
                generatorText.color = normalColor;
            }
        }
    }
}