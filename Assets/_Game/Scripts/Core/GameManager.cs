using UnityEngine;
using System.Collections.Generic;

namespace Squad
{
    public enum GameState
    {
        Title,
        Playing,
        Pause,
        Caught,
        GameOver,
        Result
    }

    /// <summary>
    /// 게임 전체의 진행 상태를 관리한다.
    ///
    /// 담당하는 것:
    ///   - 게임 상태(타이틀/플레이/일시정지/게임오버/결과) 전환
    ///   - 발전기와 출구 관리
    ///   - 라운드 수/발전기 수 표시
    ///   - 클리어 조건 판정과 탈출구 활성화
    ///
    /// UI는 상태에 맞춰 켜고 끄기만 하며, 내용 갱신은 각 UI 클래스에 맡긴다.
    ///
    /// 화면 규칙:
    ///   Playing        게임 화면만
    ///   Pause          게임 화면 위에 일시정지 화면을 겹침
    ///   Caught         모든 화면을 끔 (적에게 잡힌 연출이 화면을 가득 채운다)
    ///   그 외          게임 화면을 끄고 해당 화면만
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("■ 필수 연결 — 비워두면 에러")]
        [Tooltip("생성할 게임 무대 Prefab")]
        [SerializeField] private GameObject stage;
        [Tooltip("타이틀 화면")]
        [SerializeField] private GameObject titleUI;
        [Tooltip("게임 플레이 중 표시되는 UI 묶음")]
        [SerializeField] private GameObject gameUI;
        [Tooltip("진행 상황 표시 (라운드, 발전기)")]
        [SerializeField] private StatusPrompt statusPrompt;
        [Tooltip("일시정지 화면")]
        [SerializeField] private GameObject pauseUI;
        [Tooltip("게임 오버 화면")]
        [SerializeField] private GameObject gameOverUI;
        [Tooltip("라운드 클리어 화면")]
        [SerializeField] private GameObject resultUI;

        // 라운드 수와 출구는 GameTuning 에셋의 stage 항목에서 읽는다.
        private bool openExitAtStart => GameTuning.Instance.stage.openExitAtStart;
        private int totalRounds => GameTuning.Instance.stage.totalRounds;

        /// 현재 존재하는 스테이지 오브젝트. 게임 시작 시 생성되고, 라운드 종료 시 제거된다.
        private GameObject currentStage;
        /// 현재 게임 상태
        public GameState CurrentState { get; private set; }

        /// 현재 라운드 (1부터 시작)
        public int CurrentRound { get; private set; } = 1;
        /// 전체 라운드 수
        public int TotalRounds => totalRounds;

        /// 켜진 발전기의 수
        public int ActivatedGenerators { get; private set; }

        /// 클리어 조건을 검사하기 위한 발전기 집합.
        /// 각 발전기가 시작할 때 스스로 등록한다.
        private readonly HashSet<Generator> generators = new();

        /// 출구
        private Exit _exit;
        /// 지금 라운드의 출구. 아직 등록되지 않았거나 라운드가 끝났으면 null. (출구 방향 표시가 읽어 간다)
        public Exit CurrentExit => _exit;

        private void Awake()
        {
            // 기존에 있던 GameManager Instance 제거
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            // 다른 오브젝트들의 Awake가 끝난 뒤에 화면을 구성하도록
            // Awake가 아니라 Start에서 초기 상태를 설정한다.
            // 발전기 수 표시는 발전기가 등록될 때마다(AddGenerator) 갱신된다.
            ChangeState(GameState.Title);
            RefreshStatus();
        }

        // ── 상태 전환 ────────────────────────────────────────────────

        public void StartGame()
        {
            ChangeState(GameState.Playing);
            ClearExitAndGenerators();
            currentStage = Instantiate(stage, Vector3.zero, Quaternion.identity);
        }

        /// <summary>
        /// 결과 화면의 다음 라운드 버튼에서 호출.
        /// 라운드를 하나 올리고 새 Stage로 시작한다.
        /// 마지막 라운드까지 클리어했다면 라운드를 1로 되돌리고 타이틀로 돌아간다.
        /// </summary>
        public void NextRound()
        {
            if (CurrentRound >= totalRounds)
            {
                CurrentRound = 1;
                RefreshStatus();
                ChangeState(GameState.Title);
                return;
            }

            /// 라운드 증가 후 새 Stage를 시작한다.
            CurrentRound++;
            StartGame();
        }

        /// <summary>
        /// 일시정지, 게임 오버, 라운드 클리어 화면의 "타이틀로 돌아가기" 버튼에서 호출.
        /// 진행 중인 Stage가 있으면 치우고, 라운드를 1로 되돌린 뒤 타이틀 화면으로 간다.
        /// 타이틀은 같은 씬의 화면이라 씬을 다시 불러오지 않는다.
        /// </summary>
        public void ReturnToTitle()
        {
            if (currentStage != null)
                Destroy(currentStage);
            currentStage = null;

            ClearExitAndGenerators();
            CurrentRound = 1;
            RefreshStatus();
            ChangeState(GameState.Title);
        }

        public void PauseGame()
        {
            ChangeState(GameState.Pause);
            /// 게임 일시정지 구현 필요
        }

        public void ResumeGame()
        {
            ChangeState(GameState.Playing);
            /// 게임으로 돌아가기 구현 필요
        }

        /// 탈출구에 도달하면 호출
        public void RoundClear()
        {
            // 같은 프레임에 여러 번 호출되어도 한 번만 처리한다.
            if (CurrentState != GameState.Playing)
                return;

            ChangeState(GameState.Result);
            if (currentStage != null)
                Destroy(currentStage);
                
            ClearExitAndGenerators();
        }

        /// <summary>
        /// 적에게 잡힌 연출(Jumpscare)을 시작할 때 호출한다.
        /// 연출이 끝나면 연출 쪽에서 GameOver를 불러 마무리한다.
        /// 이미 잡혔거나 플레이 중이 아니면 false를 돌려주므로, 연출을 시작하면 안 된다.
        /// </summary>
        public bool BeginCaught()
        {
            if (CurrentState != GameState.Playing)
                return false;

            ChangeState(GameState.Caught);
            return true;
        }

        /// <summary>적에게 잡히면(연출이 있다면 연출이 끝난 뒤) 호출</summary>
        public void GameOver()
        {
            if (CurrentState != GameState.Playing && CurrentState != GameState.Caught)
                return;

            ChangeState(GameState.GameOver);
            if (currentStage != null)
                Destroy(currentStage);

            ClearExitAndGenerators();
        }

        private void ChangeState(GameState newState)
        {
            CurrentState = newState;

            // 일시정지 중에는 게임 속 모든 소리(배경음, 효과음)를 한 번에 멈춘다.
            // ignoreListenerPause가 켜진 AudioSource(SfxPlayer의 UI 소리)만 계속 들린다.
            AudioListener.pause = newState == GameState.Pause;

            switch (CurrentState)
            {
                case GameState.Title:
                    ShowOnly(titleUI);
                    Time.timeScale = 0f;
                    break;

                case GameState.Playing:
                    ShowOnly(gameUI);
                    Time.timeScale = 1f;
                    break;

                // 일시정지만 게임 화면 위에 겹친다.
                case GameState.Pause:
                    SetActiveSafe(pauseUI, true);
                    Time.timeScale = 0f;
                    break;

                // 연출이 재생되어야 하므로 시간은 멈추지 않는다.
                case GameState.Caught:
                    ShowOnly(null);
                    Time.timeScale = 1f;
                    break;

                case GameState.GameOver:
                    ShowOnly(gameOverUI);
                    Time.timeScale = 0f;
                    break;

                case GameState.Result:
                    ShowOnly(resultUI);
                    Time.timeScale = 0f;
                    break;
            }
        }

        /// <summary>
        /// 모든 화면을 끄고 지정한 화면만 켠다.
        /// 각 상태마다 모든 UI를 일일이 지정하면 빠뜨리기 쉬우므로,
        /// "전부 끄고 하나만 켠다"로 단순화한다.
        /// </summary>
        private void ShowOnly(GameObject target)
        {
            SetActiveSafe(titleUI, false);
            SetActiveSafe(gameUI, false);
            SetActiveSafe(pauseUI, false);
            SetActiveSafe(gameOverUI, false);
            SetActiveSafe(resultUI, false);

            SetActiveSafe(target, true);
        }

        // 연결이 비어 있어도 예외 없이 넘어가도록 감싼다.
        private static void SetActiveSafe(GameObject target, bool value)
        {
            if (target != null)
                target.SetActive(value);
        }

        // ── 발전기 ───────────────────────────────────────────────────

        /// <summary>
        /// 생성된 발전기가 스스로를 등록한다.
        /// Stage는 게임 시작 후에 생성되므로, 등록될 때마다 표시를 갱신한다.
        /// </summary>
        public void AddGenerator(Generator generator)
        {
            if (generator != null)
            {
                generators.Add(generator);
                RefreshStatus();
            }
        }

        /// 생성된 출구가 스스로를 등록한다.
        public void AddExit(Exit exit)
        {
            if (exit == null)
                return;

            _exit = exit;
            if (openExitAtStart)
                _exit.Activate();
        }

        /// <summary>
        /// 발전기가 켜질 때 호출한다.
        /// 켜진 수를 다시 세어 표시를 갱신하고, 전부 켜졌으면 탈출구를 연다.
        /// </summary>
        public void CheckExit()
        {
            RefreshStatus();

            if (generators.Count > 0 && ActivatedGenerators >= generators.Count)
            {
                if (_exit != null)
                    _exit.Activate();
            }
        }

        /// <summary>
        /// 켜진 발전기 수를 다시 세어, 화면에 표시될 ActivatedGenerators 값을 갱신한다.
        /// 호출할 때마다 0부터 세므로 여러 번 불러도 값이 누적되지 않는다.
        /// </summary>
        private void RefreshStatus()
        {
            int count = 0;
            foreach (Generator generator in generators)
                if (generator != null && generator.IsActive)
                    count++;

            // 값 갱신
            ActivatedGenerators = count;

            if (statusPrompt != null)
            {
                statusPrompt.SetGenerators(ActivatedGenerators, generators.Count);
                statusPrompt.SetRound(CurrentRound, totalRounds);
            }
        }

        /// <summary>
        /// 라운드가 끝나거나 게임이 종료될 때, 현재 존재하는 출구와 발전기들을 제거한다.
        /// 매니저는 Stage의 존재와 관계없이 발전기와 출구를 관리하므로
        /// Stage가 제거될 때도 이 메서드를 호출해야 한다.
        /// </summary>
        private void ClearExitAndGenerators()
        {
            generators.Clear();
            _exit = null;

            // Stage가 파괴되면 OnTriggerExit이 호출되지 않으므로
            // 범위 안에서 띄웠던 안내 문구를 여기서 직접 내린다.
            if (InteractionPrompt.Instance != null)
                InteractionPrompt.Instance.HideAll();

            // 이전 라운드의 숫자가 화면에 남지 않도록 비운 상태로 갱신
            RefreshStatus();
        }
    }
}