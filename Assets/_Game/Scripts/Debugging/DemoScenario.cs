using System.Collections;
using UnityEngine;

namespace Squad
{
    /// <summary>시연의 단계. 시연 씬마다 하나씩 정해 둔다.</summary>
    public enum DemoStep
    {
        Round1 = 1,     // 1. 실제 게임 1라운드 (작은 맵)
        GoapBasics,     // 2. GOAP 기본: 우선순위만으로 목표가 바뀐다
        GoalToggle,     // 3. 목표 켜고 끄기
        PlanFailure,    // 4. 행동이 없으면 계획 실패
        DualGrid,       // 5. 두 겹의 길찾기 격자
        Cooperation,    // 6. 협력: 포효, 합류, 수색 지점 분담, 가로막기
        Round3,         // 7. 실제 게임 3라운드
    }

    /// <summary>
    /// 시연 씬의 설정. 씬이 열리면 타이틀을 건너뛰고, 단계에 맞게 라운드를 꾸며 바로 시작한다.
    ///   - 라운드 구성(구역 수, 적 수, 모닥불 수)을 단계에 맞게 덮어쓴다. GameTuning 에셋은 바꾸지 않는다
    ///   - 시드를 고정해서, 같은 씬을 열면 매번 같은 배치와 같은 장면이 나온다
    ///   - 판단 표시(F4)나 격자 표시(F6)를 처음부터 켜 둔다
    ///   - 화면 오른쪽 위에 이 단계에서 보여 줄 것과 쓸 키를 띄운다
    /// 잡혀서 다시 시작해도(같은 라운드) 같은 준비를 다시 한다.
    ///
    /// 단계별 키 (3, 4단계):
    ///   1  모닥불 끄기 목표(ExtinguishFire) 부품 켜기/끄기 (인스펙터의 체크박스와 같다)
    ///   2  MoveToFire 행동 넣기/빼기
    ///   3  꺼진 모닥불을 모두 켜서 출구를 연다
    ///
    /// 씬 구성:
    ///   Game 씬을 복사한 시연 씬에 빈 오브젝트로 하나 둔다. 메뉴 Contaminated > 시연 씬 만들기가 7개를 한 번에 만든다.
    /// </summary>
    public class DemoScenario : MonoBehaviour
    {
        [Tooltip("이 씬이 보여 줄 시연 단계")]
        [SerializeField] private DemoStep step = DemoStep.Round1;
        [Tooltip("무작위 고정 값. 같은 값이면 매번 같은 배치가 나온다. 0이면 고정하지 않는다")]
        [SerializeField] private int seed = 1234;
        [Tooltip("2~4단계에서 적을 플레이어 앞 이 거리(m)에 세운다")]
        [SerializeField] private float enemyDistance = 30f;
        [Tooltip("2~4단계에서 적이 배회하는 영역의 반지름(m). 세운 자리 둘레만 돌아서, 건드리기 전에는 플레이어에게 오지 않는다.\n" +
                 "적 거리 - 이 값이 Real 시야(22m)보다 커야 시작하자마자 들키지 않는다")]
        [SerializeField] private float enemyTerritoryRadius = 6f;
        [Tooltip("2~4단계의 카메라 거리(m). 앞에 세운 적과 그 머리 위 판단 표시가 함께 화면에 들어오도록 원래(22m)보다 멀리 뺀다")]
        [SerializeField] private float closeStepCameraDistance = 40f;

        private RoundSetup _setup;
        private DimensionController _preparedFor;
        private GUIStyle _style;

        private bool ShowsPlans => step is DemoStep.GoapBasics or DemoStep.GoalToggle or DemoStep.PlanFailure or DemoStep.Cooperation;
        private bool UsesFireKeys => step is DemoStep.GoalToggle or DemoStep.PlanFailure;

        private void Awake()
        {
            _setup = BuildRound();
            StageTuning.RoundOverride = round => round == 1 ? _setup : null;
            StageTuning.SeedOverride = seed;
            CameraTuning.DistanceOverride = step is DemoStep.GoapBasics or DemoStep.GoalToggle or DemoStep.PlanFailure
                ? closeStepCameraDistance
                : 0f;
            HorrorChaserAgent.ResetPlanStats();
        }

        private void OnDestroy()
        {
            StageTuning.RoundOverride = null;
            StageTuning.SeedOverride = 0;
            CameraTuning.DistanceOverride = 0f;
        }

        // GameManager가 타이틀 화면을 띄운 다음 프레임에 바로 라운드를 시작한다.
        private IEnumerator Start()
        {
            yield return null;
            if (GameManager.Instance != null)
                GameManager.Instance.StartAtRound(step == DemoStep.Round3 ? 3 : 1);
        }

        // 단계에 맞는 1라운드 구성. 실제 게임 3라운드는 덮어쓰지 않는다.
        private RoundSetup BuildRound()
        {
            if (step == DemoStep.Round3)
                return null;

            RoundSetup[] rounds = GameTuning.Instance.stage.rounds;
            RoundSetup setup = rounds != null && rounds.Length > 0
                ? JsonUtility.FromJson<RoundSetup>(JsonUtility.ToJson(rounds[0]))
                : new RoundSetup();

            switch (step)
            {
                case DemoStep.Round1:
                    // 작은 맵: 남쪽 구역 하나만 연다.
                    setup.openZones = 1;
                    setup.campfires = 2;
                    setup.firesToOpenExit = 2;
                    setup.realEnemies = 2;
                    setup.fakeEnemies = 1;
                    break;
                case DemoStep.GoapBasics:
                    Set(setup, zones: 1, fires: 1, real: 1, fake: 0, pools: 0);
                    break;
                case DemoStep.GoalToggle:
                case DemoStep.PlanFailure:
                    Set(setup, zones: 1, fires: 2, real: 1, fake: 0, pools: 0);
                    break;
                case DemoStep.DualGrid:
                    // 봉인된 관문(Real에서만 막힘)이 생기도록 구역을 둘 열고, 웅덩이(Fake에서만 막힘)를 많이 둔다.
                    Set(setup, zones: 2, fires: 2, real: 0, fake: 0, pools: 8);
                    break;
                case DemoStep.Cooperation:
                    Set(setup, zones: 2, fires: 2, real: 3, fake: 0, pools: 2);
                    break;
            }
            setup.fakeExtinguish = false;
            return setup;
        }

        private static void Set(RoundSetup setup, int zones, int fires, int real, int fake, int pools)
        {
            setup.openZones = zones;
            setup.campfires = fires;
            setup.firesToOpenExit = fires;
            setup.realEnemies = real;
            setup.fakeEnemies = fake;
            setup.contaminationPools = pools;
        }

        private void Update()
        {
            // 새 라운드(Stage)가 생기면 한 번 준비한다. 잡혀서 다시 시작한 경우도 여기로 온다.
            DimensionController controller = DimensionController.Instance;
            if (controller != null && controller != _preparedFor && controller.player != null &&
                GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Playing)
            {
                _preparedFor = controller;
                Prepare(controller.player);
            }

            if (!UsesFireKeys)
                return;
            if (Input.GetKeyDown(KeyCode.Alpha1))
                foreach (ExtinguishFireGoal goal in FindObjectsOfType<ExtinguishFireGoal>(true))
                    goal.enabled = !goal.enabled;
            if (Input.GetKeyDown(KeyCode.Alpha2))
                foreach (ExtinguishFireGoal goal in FindObjectsOfType<ExtinguishFireGoal>(true))
                    goal.IncludeMoveToFire = !goal.IncludeMoveToFire;
            if (Input.GetKeyDown(KeyCode.Alpha3))
                foreach (Campfire campfire in new System.Collections.Generic.List<Campfire>(Campfire.All))
                    if (campfire != null && !campfire.IsLit)
                        campfire.Light();
        }

        private void Prepare(Player player)
        {
            GoapPlanDisplay.Showing = ShowsPlans;
            PathGridDisplay.Showing = step == DemoStep.DualGrid;
            if (step == DemoStep.Round3)
                GoapPlanDisplay.Showing = false;

            if (step is not (DemoStep.GoapBasics or DemoStep.GoalToggle or DemoStep.PlanFailure))
                return;

            // 적을 플레이어 앞, 같은 구역 안의 걸을 수 있는 자리에 세우고, 그 둘레만 배회하게 묶어 둔다.
            // (원래 무리의 영역은 처음 놓인 자리 둘레라서, 그대로 두면 그쪽으로 돌아가다 플레이어와 마주친다)
            Vector3 origin = player.transform.position;
            foreach (HorrorChaserAgent enemy in FindObjectsOfType<HorrorChaserAgent>())
                if (TryFindSpotInFront(origin, out Vector3 spot))
                {
                    enemy.Locomotion.Warp(spot);
                    var territory = new ChaserPack(spot, enemyTerritoryRadius);
                    territory.Add(enemy);
                    enemy.Pack = territory;
                }

            if (step == DemoStep.GoapBasics)
                return;

            // 플레이어에게서 가까운 모닥불 하나를 켜 둔다. 적이 끄러 갈 대상이다.
            GoapPlanDisplay.LightNearestFire();
            foreach (ExtinguishFireGoal goal in FindObjectsOfType<ExtinguishFireGoal>(true))
            {
                goal.IncludeMoveToFire = step != DemoStep.PlanFailure;
                goal.enabled = step == DemoStep.PlanFailure;
            }
        }

        // 지형 한가운데 쪽을 기준으로 좌우를 돌아보며, 플레이어에게서 길이 이어지는 자리를 찾는다.
        // (봉우리 비탈은 가파르고, 닫힌 구역은 막혀 있으므로 길이 있는지로 확인한다)
        private bool TryFindSpotInFront(Vector3 origin, out Vector3 spot)
        {
            spot = origin;
            var pathfinder = FindObjectOfType<Astar3D.Pathfinder>();
            Terrain terrain = Terrain.activeTerrain;
            Vector3 forward = Vector3.forward;
            if (terrain != null)
            {
                Vector3 center = terrain.transform.position + terrain.terrainData.size * 0.5f;
                forward = center - origin;
                forward.y = 0f;
                forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
            }

            float[] turns = { 60f, -60f, 90f, -90f, 30f, -30f, 0f, 120f, -120f };
            foreach (float turn in turns)
            {
                Vector3 candidate = origin + Quaternion.Euler(0f, turn, 0f) * forward * enemyDistance;
                if (pathfinder == null)
                {
                    spot = candidate;
                    return true;
                }
                if (pathfinder.IsWalkable(candidate, Dimension.Real) && pathfinder.FindPath(origin, candidate, Dimension.Real) != null)
                {
                    spot = candidate;
                    return true;
                }
            }
            return false;
        }

        private void OnGUI()
        {
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Playing)
                return;

            _style ??= new GUIStyle(GUI.skin.box)
            {
                fontSize = 15,
                richText = true,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
            };

            string text = Guide();
            if (UsesFireKeys)
                text += "\n\n" + FireState();
            text += $"\n<size=12><color=#909090>시드 {seed} 고정 · F7 느리게 · F8 멈춤 · F9 다음 라운드 · F11 계획 로그</color></size>";

            const float Width = 470f;
            float height = _style.CalcHeight(new GUIContent(text), Width);
            GUI.Box(new Rect(Screen.width - Width - 12f, 12f, Width, height), text, _style);
        }

        private string Guide()
        {
            return step switch
            {
                DemoStep.Round1 =>
                    "<b><color=yellow>1. 실제 게임 (1라운드, 작은 맵)</color></b>\n" +
                    "구역 1개 · Real 적 2마리(무리) · Fake 적 1마리\n" +
                    "모닥불 2개를 Fake에서 켜면(E) 출구가 열린다\n" +
                    "F4: 적의 판단 · F6: 길찾기 격자",
                DemoStep.GoapBasics =>
                    "<b><color=yellow>2. GOAP 기본: 우선순위만으로 목표가 바뀐다</color></b>\n" +
                    "가만히 있기 → Wander (1)\n" +
                    "돌을 주워(E) 던지기(E) → 들은 적이 InvestigateSound (50)\n" +
                    "적의 시야에 들어가기 → CatchPlayer (100)\n" +
                    "숨거나 멀어지기 → 경계가 Alerted → Suspicious → Calm으로 내려가며 다시 Wander",
                DemoStep.GoalToggle =>
                    "<b><color=yellow>3. 목표 켜고 끄기 (적 코드는 그대로)</color></b>\n" +
                    "가까운 모닥불 하나를 켜 두었다\n" +
                    "<b>1</b>: 모닥불 끄기 목표 켜기/끄기 (인스펙터 체크박스와 같다)\n" +
                    "   켜면 MoveToFire → Extinguish를 스스로 이어 붙여 끄러 가고, 끄면 원래 목표로\n" +
                    "<b>3</b>: 남은 불을 모두 켜 출구 열기 → 고를 이유가 없어져 목표가 저절로 꺼진다\n" +
                    "(사람이 켜고 끄는 것 / 세계 상태가 켜고 끄는 것)",
                DemoStep.PlanFailure =>
                    "<b><color=yellow>4. 행동이 없으면 계획 실패</color></b>\n" +
                    "모닥불 끄기 목표는 켜져 있고, MoveToFire 행동만 빼 두었다\n" +
                    "→ 불 곁으로 갈 방법이 없어 계획이 나오지 않는다. 적은 이 목표를 건너뛰고 다음 목표를 따른다\n" +
                    "   (머리 위 빨간 \"계획 실패\")\n" +
                    "<b>2</b>: MoveToFire 넣기/빼기 → 넣는 순간 MoveToFire → Extinguish 계획이 생긴다\n" +
                    "<b>1</b>: 목표 켜기/끄기 · <b>3</b>: 불 모두 켜기",
                DemoStep.DualGrid =>
                    "<b><color=yellow>5. 두 겹의 길찾기 격자</color></b>\n" +
                    "적 없음 · 구역 2개 · 오염 웅덩이 8개\n" +
                    "파랑: Real에서만 막힘 (봉인된 관문, 모닥불 덤불 덮개)\n" +
                    "자홍: Fake에서만 막힘 (오염 웅덩이)\n" +
                    "같은 출발점(플레이어)과 도착점(가장 먼 모닥불)인데 차원마다 길이 다르다\n" +
                    "마우스 오른쪽: 도착점 바꾸기",
                DemoStep.Cooperation =>
                    "<b><color=yellow>6. 협력</color></b>\n" +
                    "Real 적 3마리가 한 영역을 함께 쓰며 서로 떨어져 순찰한다\n" +
                    "한 마리에게 들키면 포효 → 40m 안의 적이 합류 (InvestigateSignal 75)\n" +
                    "숨으면 발견 지점 둘레의 후보(최대 36곳)에 점수를 매겨, 높은 순으로 6곳을 뽑아 나눠 맡는다\n" +
                    "   회색 숫자: 후보 점수 · [번호]: 뽑힌 지점 (노랑 빈 곳, 주황 맡음, 초록 확인)\n" +
                    "여럿이 함께 보면 가장 가까운 적은 곧장, 나머지는 1.5초 앞을 좌우로 가로막는다",
                DemoStep.Round3 =>
                    "<b><color=yellow>7. 실제 게임 3라운드</color></b>\n" +
                    "구역 4개 · Real 적 5마리 · Fake 적 3마리\n" +
                    "Fake 적은 모닥불 끄기 목표가 켜진 채 시작한다\n" +
                    "→ 3번에서 켜고 끈 목표가 그대로 실제 난이도로 쓰인다",
                _ => "",
            };
        }

        // 3, 4단계: 지금 목표 부품, 행동, 출구의 상태
        private static string FireState()
        {
            ExtinguishFireGoal goal = FindObjectOfType<ExtinguishFireGoal>(true);
            if (goal == null)
                return "";
            Exit exit = GameManager.Instance.CurrentExit;
            string On(bool value, string yes, string no) => value ? $"<color=#60ff60>{yes}</color>" : $"<color=#ff8080>{no}</color>";
            return $"모닥불 끄기 목표 {On(goal.enabled, "켜짐", "꺼짐")} · " +
                   $"MoveToFire 행동 {On(goal.IncludeMoveToFire, "있음", "없음")} · " +
                   $"출구 {On(exit != null && exit.IsOpen, "열림", "닫힘")}";
        }
    }
}
