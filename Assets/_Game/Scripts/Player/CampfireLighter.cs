using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 꺼진 모닥불 가까이에서 E를 누르고 있으면 불을 붙인다.
    /// 누르는 동안은 제자리에 멈추고, 정해 둔 시간(1.5초)을 다 채우면 불이 붙는다. 중간에 떼면 처음부터 다시 누른다.
    ///
    /// 꺼진 모닥불은 Real에서만 막히는 덤불 고리 안에 있으므로, 실제로는 Fake에 들어가서 켜게 된다.
    /// Fake에는 8초만 머물 수 있으므로 "고리 바깥에서 불까지 걷는 시간 + 누르는 시간"이 8초 안에 들어와야 한다.
    ///
    /// E는 돌을 줍고 던지는 키이기도 하다. 꺼진 모닥불 곁에서는 불 붙이기가 먼저이고, 돌은 줍거나 던지지 않는다.
    /// 수치(거리, 누르는 시간)는 GameTuning 에셋의 map 항목에서 읽는다.
    ///
    /// 씬 구성:
    ///   붙일 필요가 없다. StageLayout이 라운드를 시작할 때 플레이어에게 없으면 붙인다.
    /// </summary>
    [RequireComponent(typeof(Player))]
    public class CampfireLighter : MonoBehaviour
    {
        [Tooltip("불을 붙이는 키 (누르고 있기)")]
        [SerializeField] private KeyCode lightKey = KeyCode.E;
        [Tooltip("꺼진 모닥불 곁에서 띄울 안내 문구")]
        [SerializeField] private string promptMessage = "[E] 길게 눌러 불 붙이기";
        [Tooltip("누르고 있는 동안의 문구. {0}=진행도(%)")]
        [SerializeField] private string progressFormat = "불 붙이는 중... {0:0}%";

        /// 지금 플레이어 곁에 켤 수 있는 모닥불이 있는가. 있으면 돌을 줍고 던지지 않는다. (StoneHolder가 읽는다)
        public static bool HasTarget { get; private set; }

        private Player _player;
        private float _held;
        private bool _locked;
        private bool _promptShown;
        private string _shownMessage;

        private void Awake() => _player = GetComponent<Player>();

        private void Update()
        {
            MapTuning tuning = GameTuning.Instance.map;
            bool playing = Time.timeScale > 0f &&
                           (GameManager.Instance == null || GameManager.Instance.CurrentState == GameState.Playing);
            Campfire target = playing ? Nearest(tuning.lightRadius) : null;
            HasTarget = target != null;

            if (target == null)
            {
                Release();
                SetPrompt(null);
                return;
            }

            if (!Input.GetKey(lightKey))
            {
                Release();
                SetPrompt(promptMessage);
                return;
            }

            // 누르고 있는 동안은 제자리에 멈춘다.
            _held += Time.deltaTime;
            if (!_locked)
            {
                _player.MovementLocked = true;
                _locked = true;
            }

            float holdTime = Mathf.Max(tuning.lightHoldTime, 0.01f);
            if (_held >= holdTime)
            {
                target.Light();
                Release();
                SetPrompt(null);
                return;
            }

            SetPrompt(string.Format(progressFormat, _held / holdTime * 100f));
        }

        // 누르던 것을 처음으로 되돌리고, 멈춰 두었던 몸을 풀어 준다.
        private void Release()
        {
            _held = 0f;
            if (_locked)
            {
                _player.MovementLocked = false;
                _locked = false;
            }
        }

        // radius(m, 평면) 안의 가장 가까운 꺼진 모닥불
        private Campfire Nearest(float radius)
        {
            Campfire best = null;
            float bestDistance = radius * radius;
            foreach (Campfire campfire in Campfire.All)
            {
                if (campfire == null || campfire.IsLit)
                    continue;

                Vector3 offset = campfire.transform.position - transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude <= bestDistance)
                {
                    bestDistance = offset.sqrMagnitude;
                    best = campfire;
                }
            }
            return best;
        }

        // message가 null이면 안내를 내린다. 같은 문구를 매 프레임 다시 띄우지 않는다.
        private void SetPrompt(string message)
        {
            if (InteractionPrompt.Instance == null)
                return;

            if (message == null)
            {
                if (_promptShown)
                    InteractionPrompt.Instance.Hide(this);
                _promptShown = false;
                _shownMessage = null;
                return;
            }

            if (_promptShown && message == _shownMessage)
                return;

            InteractionPrompt.Instance.Show(this, message);
            _promptShown = true;
            _shownMessage = message;
        }

        // Stage가 사라질 때 안내와 멈춘 몸이 남지 않게 한다.
        private void OnDisable()
        {
            Release();
            SetPrompt(null);
            HasTarget = false;
        }
    }
}
