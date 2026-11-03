using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 디버그·시연용. F4를 누르면 적의 머리 위에 지금의 판단을 띄운다. 다시 누르면 끈다.
    ///   목표    지금 추구하는 목표와 우선순위
    ///   계획    플래너가 세운 행동의 순서. 지금 실행 중인 행동은 노란색
    ///   실패    우선순위는 더 높았지만 계획을 세울 수 없어 건너뛴 목표 (빨간색)
    ///   경계    Calm / Suspicious / Alerted
    ///   시간    이 적이 마지막으로 계획을 세우는 데 걸린 시간과 살펴본 노드 수
    /// 미리 짜 놓은 동작이 아니라 적이 스스로 계획을 세운다는 것을 눈으로 보여 주기 위한 것이다.
    ///
    /// 수색 지도가 있으면 땅에 후보(최대 36곳)의 점수를 띄운다.
    ///   회색 숫자        뽑히지 않은 후보의 점수
    ///   노랑 [번호]       뽑힌 지점, 아직 아무도 맡지 않음
    ///   주황 [번호] 이름  그 적이 맡아 가는 중
    ///   초록 [번호]       확인을 마침
    ///
    /// F5를 누르면 플레이어에게서 가장 가까운 꺼진 모닥불을 바로 켠다.
    /// (모닥불 끄기 목표를 시연할 때 켜진 불이 필요하므로)
    ///
    /// 씬이나 프리팹에 붙일 필요가 없다. 게임이 시작될 때 스스로 만들어진다.
    /// </summary>
    public class GoapPlanDisplay : MonoBehaviour
    {
        private const KeyCode ToggleKey = KeyCode.F4;
        private const KeyCode LightFireKey = KeyCode.F5;
        // 적 오브젝트 위치에서 글을 띄울 높이(m)
        private const float LabelHeight = 9f;
        // 화면 가장자리와 적 판단 상자 사이의 여백(px)
        private const float ScreenMargin = 8f;

        /// 지금 판단을 띄우고 있는가. (시연 씬이 처음부터 켜 둘 때 쓴다)
        public static bool Showing { get; set; }

        private GUIStyle _style;
        private GUIStyle _smallStyle;
        private GUIStyle _agentStyle;
        private readonly StringBuilder _text = new();
        private readonly Dictionary<int, string> _names = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            Showing = false;
            var displayObject = new GameObject("GOAP Plan Display");
            displayObject.AddComponent<GoapPlanDisplay>();
            DontDestroyOnLoad(displayObject);
        }

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey))
                Showing = !Showing;
            if (Input.GetKeyDown(LightFireKey))
                LightNearestFire();
        }

        /// 플레이어에게서 가장 가까운 꺼진 모닥불을 켠다. (없으면 아무 일도 하지 않는다)
        public static void LightNearestFire()
        {
            Player player = DimensionController.Instance != null ? DimensionController.Instance.player : null;
            if (player == null)
                return;

            Campfire best = null;
            float bestDistance = float.MaxValue;
            foreach (Campfire campfire in Campfire.All)
            {
                if (campfire == null || campfire.IsLit)
                    continue;
                float distance = (campfire.transform.position - player.transform.position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = campfire;
                }
            }

            if (best != null)
                best.Light();
        }

        private void OnGUI()
        {
            if (!Showing || Camera.main == null)
                return;

            _style ??= new GUIStyle(GUI.skin.box)
            {
                fontSize = 15,
                richText = true,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = Color.white },
            };
            _smallStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                richText = true,
                alignment = TextAnchor.MiddleCenter,
            };

            // 적 판단 상자: 짙은 바탕에 큰 글씨. 밝은 땅 위에서도 읽히게 한다.
            if (_agentStyle == null)
            {
                var background = new Texture2D(1, 1);
                background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.8f));
                background.Apply();
                _agentStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 17,
                    richText = true,
                    alignment = TextAnchor.UpperLeft,
                    padding = new RectOffset(10, 10, 6, 8),
                    normal = { textColor = Color.white, background = background },
                };
            }

            double average = HorrorChaserAgent.PlanCount > 0
                ? HorrorChaserAgent.TotalPlanMilliseconds / HorrorChaserAgent.PlanCount
                : 0;
            // 오른쪽 위의 시연 안내와 겹치지 않도록 글 길이만큼만 차지한다.
            string summary =
                "<color=yellow>GOAP 판단 표시 (F4로 끄기, F5: 가까운 불 켜기)</color>\n" +
                $"계획 {HorrorChaserAgent.PlanCount}번 · 평균 {average:0.000}ms · 최대 {HorrorChaserAgent.MaxPlanMilliseconds:0.000}ms";
            Vector2 summarySize = _style.CalcSize(new GUIContent(summary));
            GUI.Label(new Rect(12f, 60f, summarySize.x, summarySize.y), summary, _style);

            Camera cam = Camera.main;
            HorrorChaserAgent[] agents = FindObjectsOfType<HorrorChaserAgent>();
            _names.Clear();
            foreach (HorrorChaserAgent agent in agents)
                _names[agent.transform.GetInstanceID()] = agent.name;

            foreach (HorrorChaserAgent agent in agents)
                DrawAgent(cam, agent);

            DrawSearchCandidates(cam);
        }

        private void DrawAgent(Camera cam, HorrorChaserAgent agent)
        {
            Vector3 screen = cam.WorldToScreenPoint(agent.transform.position + Vector3.up * LabelHeight);
            if (screen.z <= 0f)
                return;

            // 목표 줄은 경계에 따라 색을 바꾸고 크게 쓴다. 멀리서도 "지금 무엇을 하려는지"가 먼저 보이게.
            ChaserAlert alert = agent.GetComponent<ChaserAlert>();
            string alertColor = alert == null ? "#ffffff"
                : alert.Level == AlertLevel.Alerted ? "#ff5050"
                : alert.Level == AlertLevel.Suspicious ? "#ffd040"
                : "#70e070";

            _text.Clear();
            _text.Append("<size=13><color=#b0b0b0>").Append(agent.name).Append("</color></size>\n");
            _text.Append("<size=22><b><color=").Append(alertColor).Append('>').Append(agent.CurrentGoalName)
                 .Append("</color></b></size> <size=15>(").Append(agent.CurrentGoalPriority).Append(")</size>\n");
            _text.Append("계획: ");
            bool first = true;
            foreach ((string step, bool current) in agent.PlanSteps)
            {
                if (!first)
                    _text.Append(" → ");
                _text.Append(current ? "<b><color=yellow>" + step + "</color></b>" : step);
                first = false;
            }
            if (first)
                _text.Append("-");

            if (agent.FailedGoals.Count > 0)
                _text.Append("\n<b><color=#ff6060>계획 실패: ").Append(string.Join(", ", agent.FailedGoals)).Append("</color></b>");

            if (alert != null)
                _text.Append("\n경계: <color=").Append(alertColor).Append('>').Append(alert.Level).Append("</color>");
            _text.Append($"\n<size=13><color=#b0b0b0>계획 {agent.LastPlanMilliseconds:0.000}ms · 노드 {agent.LastPlanExpanded}</color></size>");

            string label = _text.ToString();
            Vector2 size = _agentStyle.CalcSize(new GUIContent(label));
            // 화면 좌표는 아래가 0이고, GUI 좌표는 위가 0이다.
            var rect = new Rect(screen.x - size.x * 0.5f, Screen.height - screen.y - size.y, size.x, size.y);

            // 적이 화면 밖으로 나가도 상자는 가장자리에 붙여 계속 보여 준다.
            float x = Mathf.Clamp(rect.x, ScreenMargin, Mathf.Max(ScreenMargin, Screen.width - size.x - ScreenMargin));
            float y = Mathf.Clamp(rect.y, ScreenMargin, Mathf.Max(ScreenMargin, Screen.height - size.y - ScreenMargin));
            if (!Mathf.Approximately(x, rect.x) || !Mathf.Approximately(y, rect.y))
                label = "<size=13><color=#b0b0b0>(화면 밖)</color></size> " + label;
            rect.position = new Vector2(x, y);
            GUI.Box(rect, label, _agentStyle);
        }

        // 수색 후보와 점수, 뽑힌 지점을 누가 맡았는지
        private void DrawSearchCandidates(Camera cam)
        {
            SquadBlackboard bb = SquadBlackboard.Instance;
            if (bb == null || bb.SearchCandidates.Count == 0)
                return;

            foreach (SquadBlackboard.SearchCandidate candidate in bb.SearchCandidates)
            {
                if (candidate.Chosen)
                    continue;
                DrawWorldLabel(cam, candidate.Position, $"<color=#a0a0a0>{candidate.Score:0.00}</color>");
            }

            int index = 1;
            foreach (SquadBlackboard.SearchPoint point in bb.SearchPoints)
            {
                float score = 0f;
                foreach (SquadBlackboard.SearchCandidate candidate in bb.SearchCandidates)
                    if (candidate.Chosen && candidate.Position == point.Position)
                        score = candidate.Score;

                string color = point.Checked ? "#60ff60" : point.Owner != 0 ? "#ffa030" : "#ffff40";
                string owner = point.Owner != 0 && _names.TryGetValue(point.Owner, out string ownerName) ? " " + ownerName : "";
                string state = point.Checked ? " 확인" : "";
                DrawWorldLabel(cam, point.Position, $"<b><color={color}>[{index}] {score:0.00}{owner}{state}</color></b>");
                index++;
            }
        }

        private void DrawWorldLabel(Camera cam, Vector3 world, string text)
        {
            Vector3 screen = cam.WorldToScreenPoint(world + Vector3.up * 1.5f);
            if (screen.z <= 0f)
                return;
            Vector2 size = _smallStyle.CalcSize(new GUIContent(text));
            GUI.Label(new Rect(screen.x - size.x * 0.5f, Screen.height - screen.y - size.y * 0.5f, size.x, size.y), text, _smallStyle);
        }
    }
}
