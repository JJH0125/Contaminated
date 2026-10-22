using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 효과음을 재생한다.
    ///   PlayUI  위치 없는 2D 소리 (버튼 클릭, 라운드 클리어 등). 일시정지 중에도 들린다
    ///   PlayAt  월드의 한 위치에서 나는 3D 소리 (발소리, 미끼 등). 일시정지 중에는 멈춘다
    ///
    /// 적이 듣는 소리(Sound)는 SoundEmitter.Emit이 PlayAt을 대신 호출하므로
    /// 소리를 내는 쪽에서 따로 부를 필요가 없다. 적이 듣지 않는 소리만 직접 부른다.
    ///
    /// 발전기처럼 계속 나는 소리는 여기서 재생하지 않는다.
    /// 그 오브젝트에 AudioSource를 붙여 반복 재생해야 오브젝트와 함께 켜지고 사라진다.
    ///
    /// 발소리는 짧은 간격으로 계속 나므로 AudioSource를 매번 만들고 지우지 않고,
    /// 미리 만들어 둔 AudioSource들을 돌려 쓴다(풀링).
    ///
    /// 씬 구성:
    ///   빈 오브젝트에 이 스크립트를 붙인다. 씬에 하나만 둔다.
    /// </summary>
    public class SfxPlayer : MonoBehaviour
    {
        public static SfxPlayer Instance { get; private set; }

        // 수치는 GameTuning 에셋(Resources/GameTuning)에서 읽는다.
        private float volume => GameTuning.Instance.audio.sfxVolume;
        private float minDistance => GameTuning.Instance.audio.minDistance;
        private float maxDistance => GameTuning.Instance.audio.maxDistance;
        private int poolSize => GameTuning.Instance.audio.poolSize;

        /// <summary>효과음 볼륨. 따로 소리를 내는 스크립트(CctvNoiseEffect 등)도 이 값에 맞춘다.</summary>
        public float Volume => volume;

        // UI용 AudioSource
        private AudioSource _uiSource;
        // 월드용 AudioSource
        private AudioSource[] _worldSources;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            /// UI용 AudioSource의 설정.
            /// 하나밖에 필요하지 않으며, 아래의 줄을 통해 직접 생성되어 추가된다.
            _uiSource = gameObject.AddComponent<AudioSource>();
            /// 씬이 시작되어도 저절로 재생하지 않음
            _uiSource.playOnAwake = false;
            /// 2D 상의 소리이기 때문에 발생 위치에 상관없이 같은 크기로 들린다.
            _uiSource.spatialBlend = 0f;
            /// UI용 소리는 Manager가 일시정지를 통해 재생을 막아도 재생되어야 하는 유일한 소리
            /// 따라서 Manager의 정지 명령을 무시한다.
            _uiSource.ignoreListenerPause = true;

            /// 월드용 또한 아래의 코드를 통해 직접 생성되어 추가된다.
            _worldSources = new AudioSource[poolSize];
            for (int i = 0; i < poolSize; i++)
                _worldSources[i] = CreateWorldSource(i);
        }

        /// <summary>
        /// 월드용 AudioSource를 만드는 함수.
        /// </summary>
        private AudioSource CreateWorldSource(int index)
        {
            /// SfxSource_0 ~ SfxSource_7까지 총 8개의 오브젝트가 생성됨
            var sourceObject = new GameObject("SfxSource_" + index);
            /// 8개의 오브젝트들이 SfxPlayer의 자식으로 들어옴
            sourceObject.transform.SetParent(transform, false);

            var source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;   // 완전한 3D: 위치에 따라 좌우와 크기가 달라진다
            // 쿼터뷰라 카메라(AudioListener)가 캐릭터와 늘 떨어져 있으므로,
            // 기본값(로그 감쇠, 1m부터 줄어듦) 대신 거리를 넉넉히 잡은 직선 감쇠를 쓴다.
            source.rolloffMode = AudioRolloffMode.Linear;
            return source;
        }

        /// <summary>위치 없는 2D 효과음을 재생한다. 일시정지 중에도 들린다.</summary>
        public void PlayUI(AudioClip clip)
        {
            if (clip == null)
                return;

            _uiSource.PlayOneShot(clip, volume);
        }

        /// <summary>position에서 나는 3D 효과음을 재생한다.</summary>
        public void PlayAt(AudioClip clip, Vector3 position)
        {
            if (clip == null || _worldSources == null)
                return;

            AudioSource source = GetFreeSource();
            source.transform.position = position;
            source.clip = clip;
            source.volume = volume;
            // 인스펙터에서 바꾼 거리가 바로 반영되도록 재생할 때마다 넣는다.
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.Play();
        }

        // 쉬고 있는 AudioSource를 꺼내고, 모두 사용 중이면 가장 오래 재생된 것을 끊고 재사용한다.
        private AudioSource GetFreeSource()
        {
            AudioSource oldest = _worldSources[0];
            foreach (AudioSource source in _worldSources)
            {
                if (!source.isPlaying)
                    return source;
                if (source.time > oldest.time)
                    oldest = source;
            }
            return oldest;
        }
    }
}
