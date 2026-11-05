using UnityEngine;
using TMPro;

namespace Squad
{
    /// <summary>
    /// 적의 머리 위에 현재 판단 상태를 기호로 띄운다.
    ///   조사 중(InvestigateSound, InvestigateSignal) → ?
    ///   추격 중(CatchPlayer)                         → !
    ///   배회 중(Wander) 등 그 밖의 목표               → 표시 없음
    ///
    /// 경계 상태(ChaserAlert)가 아니라 "이 적의 현재 목표"를 기준으로 한다.
    /// 경계는 소리만 들어도 오르므로, 기호가 실제로 하고 있는 일과 어긋나지 않게 목표를 본다.
    ///
    /// 씬 구성:
    ///   적 오브젝트 아래에 GameObject → 3D Object → Text - TextMeshPro 를 만들고
    ///   (UI용이 아니라 3D용) 머리 위 높이에 둔 뒤, 이 스크립트를 그 텍스트에 붙인다.
    ///   적(HorrorChaserAgent)과 몸 렌더러는 부모에서 자동으로 찾으므로 따로 연결하지 않는다.
    ///   디버그 표시라 실제 게임(Game 씬)에서는 숨기고, 시연 씬(DemoScenario가 있는 씬)에서만 보인다.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class AlertIcon : MonoBehaviour
    {
        [Header("○ 튜닝 값 — 자유롭게 조절")]
        [SerializeField] private string investigateSymbol = "?";
        [SerializeField] private string chaseSymbol = "!";
        [SerializeField] private Color investigateColor = new Color(0.91f, 0.64f, 0.24f);   // 주황
        [SerializeField] private Color chaseColor = new Color(0.78f, 0.28f, 0.31f);         // 빨강
        [Tooltip("기호가 바뀌는 순간 커졌다가 돌아오는 배율")]
        [SerializeField] private float popScale = 1.8f;
        [Tooltip("튀는 효과가 원래 크기로 돌아오는 시간(초)")]
        [SerializeField] private float popDuration = 0.25f;

        // 판단 상태를 읽어올 적. 부모에서 찾는다.
        private HorrorChaserAgent agent;
        // 적의 몸 렌더러. 이 렌더러가 꺼지면(다른 차원) 기호도 숨긴다.
        private Renderer bodyRenderer;

        private TMP_Text _label;
        private Renderer _labelRenderer;
        private Transform _camera;
        private Vector3 _baseScale;

        private string _lastGoal;     // 직전 프레임의 목표 (바뀔 때만 갱신하기 위함)
        private float _popTimer;

        private void Awake()
        {
            _label = GetComponent<TMP_Text>();
            _labelRenderer = GetComponent<Renderer>();
            _baseScale = transform.localScale;

            agent = GetComponentInParent<HorrorChaserAgent>();

            if (agent != null)
                bodyRenderer = FindBodyRenderer();

            if (agent == null)
                Debug.LogError($"[{name}] HorrorChaserAgent를 찾지 못했습니다", this);

            _label.text = "";
        }

        private void Start()
        {
            if (Camera.main != null)
                _camera = Camera.main.transform;
        }

        private void Update()
        {
            if (agent == null) return;

            string goal = agent.CurrentGoalName;
            if (goal == _lastGoal) return;     // 바뀌지 않았으면 할 일 없음
            _lastGoal = goal;

            switch (goal)
            {
                case "InvestigateSound":
                case "InvestigateSignal":
                    _label.text = investigateSymbol;
                    _label.color = investigateColor;
                    _popTimer = popDuration;
                    break;

                case "CatchPlayer":
                    _label.text = chaseSymbol;
                    _label.color = chaseColor;
                    _popTimer = popDuration;
                    break;

                default:   // Wander 등
                    _label.text = "";
                    break;
            }
        }

        // 적이 회전과 이동을 모두 마친 뒤에 방향과 크기를 맞춘다.
        private void LateUpdate()
        {
            // 쿼터뷰 카메라는 회전하지 않으므로, 카메라와 같은 방향을 보면
            // 적이 어느 쪽을 향하든 글자가 항상 정면으로 보인다.
            if (_camera != null)
                transform.rotation = _camera.rotation;

            // 기호가 바뀐 순간 크게 튀었다가 원래 크기로 돌아온다.
            if (_popTimer > 0f)
            {
                _popTimer -= Time.deltaTime;
                float t = 1f - Mathf.Clamp01(_popTimer / popDuration);   // 0 → 1
                float s = Mathf.Lerp(popScale, 1f, t);
                transform.localScale = _baseScale * s;
            }
            else
            {
                transform.localScale = _baseScale;
            }

            // 다른 차원에 있어 몸이 안 보이면 기호도 숨긴다.
            // (그대로 두면 보이지 않는 적의 위치가 기호로 드러난다)
            // 실제 게임에서는 늘 숨긴다.
            if (_labelRenderer != null)
                _labelRenderer.enabled = DemoScenario.InDemoScene && (bodyRenderer == null || bodyRenderer.enabled);
        }

        // 적 아래에서 자기 자신이 아닌 첫 번째 몸 렌더러를 찾는다.
        // (DimensionMember가 켜고 끄는 적의 MeshRenderer)
        private Renderer FindBodyRenderer()
        {
            foreach (Renderer r in agent.GetComponentsInChildren<Renderer>(true))
            {
                if (r.gameObject == gameObject) continue;
                if (r is MeshRenderer || r is SkinnedMeshRenderer)
                    return r;
            }
            return null;
        }
    }
}