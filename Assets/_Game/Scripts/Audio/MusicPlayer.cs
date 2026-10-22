using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 게임 상태에 맞춰 배경음을 바꿔 튼다.
    ///   Title          타이틀 배경음
    ///   Playing        게임 배경음 (일시정지에서 돌아오면 멈춘 곳부터 이어서)
    ///   Pause          배경음 일시정지 (GameManager가 AudioListener.pause로 처리)
    ///   GameOver/Result 배경음을 서서히 끈다
    ///
    /// GameManager의 currentState를 매 프레임 확인하면서, currentState가 바뀌었을 때만 반응한다.
    /// 곡을 바꿀 때는 이전 곡을 서서히 줄인 뒤 새 곡을 서서히 키운다.
    /// 타이틀·일시정지 중에는 timeScale이 0이므로 페이드는 실제 시간(unscaled)으로 계산한다.
    ///
    /// 씬 구성:
    ///   씬에 빈 오브젝트를 만들어 이 스크립트를 붙이고(AudioSource는 자동 추가),
    ///   두 배경음 클립을 연결한다. 씬에 하나만 둔다.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class MusicPlayer : MonoBehaviour
    {
        [Header("■ 필수 연결 — 비워두면 에러")]
        [Tooltip("타이틀 화면 배경음")]
        [SerializeField] private AudioClip titleMusic;
        [Tooltip("게임 플레이 중 배경음")]
        [SerializeField] private AudioClip playingMusic;

        // 볼륨과 바뀌는 시간은 GameTuning 에셋의 audio 항목에서 읽는다.
        private float volume => GameTuning.Instance.audio.musicVolume;
        private float fadeDuration => GameTuning.Instance.audio.musicFadeDuration;

        private AudioSource _source;
        /// 비교할 이전 상태가 존재하지 않는, 게임 시작 첫 프레임만을 위한 bool 값.
        private bool _hasState;
        private GameState _lastState;

        // 곡을 바꾸려고 이전 곡을 줄이는 중인지, 다 줄인 뒤 틀 곡 (null이면 끄기)
        // 새 곡의 페이드 인은 여기 포함되지 않는다. (UpdateFade에서 항상 목표 볼륨을 따라감)
        private bool _fadingOut;
        private AudioClip _nextClip;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            /// 씬이 시작되어도 저절로 재생하지 않음
            _source.playOnAwake = false;
            /// 배경음을 반복 재생
            _source.loop = true;
            /// 첫 곡도 0에서 서서히 커지도록 볼륨을 0으로 시작
            _source.volume = 0f;
        }

        private void Update()
        {
            if (GameManager.Instance == null)
                return;

            GameState state = GameManager.Instance.CurrentState;
            if (!_hasState || state != _lastState)
            {
                _hasState = true;
                _lastState = state;
                OnStateChanged(state);
            }

            UpdateFade();
        }

        /// <summary>
        /// 상태별 처리. 실제 곡 전환은 UpdateFade에서 서서히 이뤄진다.
        /// </summary>
        private void OnStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.Title:
                    SwitchTo(titleMusic);
                    break;

                case GameState.Playing:
                    // 라운드를 새로 시작한 경우 처음부터 새 곡을 튼다.
                    // 일시정지에서 돌아온 경우엔 게임 곡이 이미 재생 중이므로 SwitchTo가 그대로 이어서 튼다.
                    SwitchTo(playingMusic);
                    break;

                case GameState.Pause:
                    // 배경음 정지와 재개는 GameManager가 AudioListener.pause로 처리한다.
                    break;

                case GameState.Caught:
                case GameState.GameOver:
                case GameState.Result:
                    SwitchTo(null);
                    break;
            }
        }

        /// 다음에 틀 곡을 정한다. 실제 전환은 UpdateFade에서 서서히 이뤄진다.
        /// 같은 곡이 이미 재생 중이면 새로 틀지 않고 이어서 재생한다.
        private void SwitchTo(AudioClip clip)
        {
            // 이미 그 곡이 나오고 있으면 패스
            // (페이드 아웃 도중 같은 곡으로 돌아온 경우: 페이드 아웃을 취소해 다시 키운다)
            if (clip != null && _source.clip == clip && _source.isPlaying)
            {
                _fadingOut = false;
                return;
            }

            _nextClip = clip;
            _fadingOut = true;
        }

        private void UpdateFade()
        {
            // 일시정지 중에는 페이드를 멈춘다.
            // 소리는 멈췄는데 페이드만 진행되면, 돌아왔을 때 곡이 이미 바뀌어 있게 된다.
            if (AudioListener.pause)
                return;

            /// unscaledDeltaTime을 사용하여 timeScale이 0인 상태에서도 페이드가 정상적으로 작동하도록 한다.
            float step = fadeDuration > 0f ? volume * Time.unscaledDeltaTime / fadeDuration : volume;

            if (_fadingOut)
            {
                // 1) 이전 곡이 나오고 있으면 먼저 소리를 0까지 줄인다.
                if (_source.isPlaying && _source.volume > 0f)
                {
                    _source.volume = Mathf.MoveTowards(_source.volume, 0f, step);
                    return;
                }

                // 2) 다 줄었으면 곡을 바꾸고, 새 곡은 0부터 시작한다.
                _source.Stop();
                _source.clip = _nextClip;
                _fadingOut = false;

                if (_nextClip != null)
                {
                    _source.volume = 0f;
                    _source.Play();
                }
                return;
            }

            // 3) 새 곡은 목표 볼륨까지 서서히 키운다.
            // 또한 인스펙터에서 볼륨을 움직이면 그에 맞춰 실제 볼륨을 서서히 조절한다.
            if (_source.isPlaying)
                _source.volume = Mathf.MoveTowards(_source.volume, volume, step);
        }
    }
}
