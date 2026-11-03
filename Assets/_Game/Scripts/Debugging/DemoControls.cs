using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 디버그·시연용 조작. "방금 그거 다시"에 바로 대응하고, 성능 질문에 숫자로 답하기 위한 것이다.
    ///   F7          느리게 (1배 ↔ 0.5배)
    ///   F8          멈추기 / 다시 움직이기
    ///   F9          다음 라운드로 바로 가기 (Shift+F9: 지금 라운드를 처음부터)
    ///   F11         계획 로그: 적이 고른 목표나 계획이 바뀔 때마다 콘솔에 남긴다 (걸린 시간, 살펴본 노드 수 포함)
    ///   Shift+F11   계획 통계를 0부터 다시 센다
    ///   (F10은 Unity Recorder의 Quick Recording 키라서 쓰지 않는다)
    /// 켜져 있는 것은 화면 왼쪽 아래에 표시한다.
    ///
    /// 느리게와 멈추기는 플레이 중에만 적용한다. (일시정지 화면, 잡히는 연출은 GameManager와 연출이 시간을 정한다)
    ///
    /// 씬이나 프리팹에 붙일 필요가 없다. 게임이 시작될 때 스스로 만들어진다.
    /// </summary>
    public class DemoControls : MonoBehaviour
    {
        private const float SlowScale = 0.5f;

        private bool _slow;
        private bool _frozen;
        private GUIStyle _style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            HorrorChaserAgent.LogPlans = false;
            var controlObject = new GameObject("Demo Controls");
            controlObject.AddComponent<DemoControls>();
            DontDestroyOnLoad(controlObject);
        }

        private void Update()
        {
            GameManager game = GameManager.Instance;
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (Input.GetKeyDown(KeyCode.F7))
                _slow = !_slow;
            if (Input.GetKeyDown(KeyCode.F8))
                _frozen = !_frozen;
            if (Input.GetKeyDown(KeyCode.F11))
            {
                if (shift)
                    HorrorChaserAgent.ResetPlanStats();
                else
                    HorrorChaserAgent.LogPlans = !HorrorChaserAgent.LogPlans;
            }
            if (Input.GetKeyDown(KeyCode.F9) && game != null)
            {
                _frozen = false;
                int round = shift ? game.CurrentRound : game.CurrentRound % game.TotalRounds + 1;
                game.StartAtRound(round);
            }

            if (game != null && game.CurrentState == GameState.Playing)
                Time.timeScale = _frozen ? 0f : _slow ? SlowScale : 1f;
        }

        private void OnGUI()
        {
            if (!_slow && !_frozen && !HorrorChaserAgent.LogPlans)
                return;

            _style ??= new GUIStyle(GUI.skin.box)
            {
                fontSize = 15,
                richText = true,
                alignment = TextAnchor.MiddleLeft,
            };

            string text = "";
            if (_frozen)
                text += "<color=#ff6060>■ 멈춤 (F8)</color>  ";
            if (_slow)
                text += "<color=#60c0ff>▶ 0.5배 (F7)</color>  ";
            if (HorrorChaserAgent.LogPlans)
                text += "<color=#ffff60>● 계획 로그 (F11)</color>";
            GUI.Label(new Rect(12f, Screen.height - 44f, 460f, 32f), text, _style);
        }
    }
}
