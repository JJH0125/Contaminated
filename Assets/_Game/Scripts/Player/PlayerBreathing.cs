using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 보호복 마스크 안에서 들리는 플레이어의 숨소리. 긴장할수록 숨이 가빠진다.
    ///
    /// 계속 반복 재생하지 않고, 숨 한 번을 재생한 뒤 잠깐 쉬었다가 다시 재생한다.
    /// 쉬는 간격과 음높이, 크기를 긴장도에 따라 바꾼다.
    ///   평소          1.5~2초 쉬고, 원래 음높이, 작게
    ///   적이 가까움   0.5초쯤 쉬고, 음높이 조금 올림, 조금 크게
    ///   쫓기는 중     거의 쉬지 않고 연달아, 음높이 더 올림, 크게
    /// 빠른 호흡은 주로 간격을 줄여서 표현한다. 음높이를 너무 올리면 우스꽝스러워지므로 1.15를 넘기지 않는다.
    ///
    /// 긴장도는 적이 가까울 때의 화면 노이즈와 같은 값(CctvNoiseEffect.Closeness)을 쓴다.
    /// 그래서 노이즈와 숨소리가 한 몸처럼 같이 반응한다.
    /// 적이 플레이어를 보고 있으면(블랙보드) 쫓기는 중으로 본다.
    ///
    /// 매번 음높이, 크기, 간격을 조금씩 흔들어서 파일 하나로도 기계적으로 들리지 않게 한다.
    /// 숨소리 파일의 뒷부분이 거의 조용하면 그 부분은 숨 길이에서 빼고 간격을 잰다.
    /// (그래야 쫓기는 중에 숨이 정말로 연달아 이어진다)
    ///
    /// 그 밖의 규칙:
    ///   - 내 숨소리라서 위치와 상관없이 같은 크기로 들린다(2D)
    ///   - 다른 효과음보다 한 단계 작게 해서 발소리나 적 소리를 가리지 않는다
    ///   - 적에게 잡히는 순간 뚝 끊긴다. 갑자기 오는 침묵이 연출이 된다
    ///   - 후반 라운드로 갈수록 음높이가 아주 조금 낮아진다. 보호복처럼 숨소리도 변해 간다
    ///   - 일시정지 중에는 다른 소리처럼 멈춘다
    ///
    /// 씬 구성:
    ///   Player가 붙은 오브젝트에 붙이고, breathClip에 숨소리 파일을 연결한다.
    /// </summary>
    public class PlayerBreathing : MonoBehaviour
    {
        [Header("■ 필수 연결 — 비워두면 에러")]
        [Tooltip("숨 한 번의 소리")]
        [SerializeField] private AudioClip breathClip;

        // 수치는 GameTuning 에셋의 breathing 항목에서 읽는다.
        private float volume => GameTuning.Instance.breathing.volume;
        private float calmVolume => GameTuning.Instance.breathing.calmVolume;
        private float nearVolume => GameTuning.Instance.breathing.nearVolume;
        private float chasedVolume => GameTuning.Instance.breathing.chasedVolume;

        // 숨 사이의 간격(초)
        private float calmGap => GameTuning.Instance.breathing.calmGap;
        private float nearGap => GameTuning.Instance.breathing.nearGap;
        private float chasedGap => GameTuning.Instance.breathing.chasedGap;

        // 음높이 (1이 원래 소리, 1.15를 넘기지 않는다)
        private float calmPitch => GameTuning.Instance.breathing.calmPitch;
        private float nearPitch => GameTuning.Instance.breathing.nearPitch;
        private float chasedPitch => GameTuning.Instance.breathing.chasedPitch;
        private float lastRoundPitchDrop => GameTuning.Instance.breathing.lastRoundPitchDrop;

        // 매번 흔드는 정도
        private float pitchJitter => GameTuning.Instance.breathing.pitchJitter;
        private float volumeJitter => GameTuning.Instance.breathing.volumeJitter;
        private float gapJitter => GameTuning.Instance.breathing.gapJitter;

        // 숨소리 파일에서 이 크기보다 작은 소리만 남은 뒷부분은 숨 길이에서 뺀다. (가장 큰 구간에 대한 비율)
        private const float TailThreshold = 0.1f;
        // 숨 길이를 잴 때 평균 크기를 내는 구간의 길이(초)
        private const float WindowSeconds = 0.02f;
        // SfxPlayer가 없을 때(테스트 씬 등) 쓰는 효과음 볼륨
        private const float FallbackSfxVolume = 0.5f;

        private AudioSource _source;
        // 숨 한 번의 실제 길이(초, 음높이 1 기준). 파일 뒤쪽의 조용한 부분은 뺀다.
        private float _breathLength;
        // 마지막으로 숨을 시작한 시각과, 그때 정한 음높이와 간격 흔들림
        private float _lastBreathTime = float.NegativeInfinity;
        private float _lastPitch = 1f;
        private float _gapOffset;

        private void Awake()
        {
            // 플레이어 오브젝트의 다른 소리와 섞이지 않도록 자식 오브젝트에서 낸다.
            var soundObject = new GameObject("Breath Sound");
            soundObject.transform.SetParent(transform, false);
            _source = soundObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0f;

            _breathLength = breathClip != null ? MeasureBreathLength(breathClip) : 0f;
        }

        private void Update()
        {
            if (breathClip == null)
                return;

            // 잡히는 연출이 시작되면 숨을 뚝 끊고, 플레이 중이 아닐 때는 숨 쉬지 않는다.
            GameManager manager = GameManager.Instance;
            if (manager != null && manager.CurrentState != GameState.Playing)
            {
                if (manager.CurrentState == GameState.Caught && _source.isPlaying)
                    _source.Stop();
                return;
            }

            bool chased = SquadBlackboard.Instance != null && SquadBlackboard.Instance.PlayerCurrentlyVisible;
            float tension = CctvNoiseEffect.Closeness;

            float gap = chased ? chasedGap : Mathf.Lerp(calmGap, nearGap, tension);
            // 지난 숨이 끝나고 간격만큼 쉬었으면 다음 숨을 쉰다.
            // 매 프레임 다시 계산하므로, 숨을 쉬는 도중에 적이 나타나면 다음 숨이 바로 앞당겨진다.
            float nextBreathTime = _lastBreathTime + _breathLength / _lastPitch + Mathf.Max(0f, gap + _gapOffset);
            if (Time.time < nextBreathTime)
                return;

            Breathe(chased, tension);
        }

        private void Breathe(bool chased, float tension)
        {
            float pitch = chased ? chasedPitch : Mathf.Lerp(calmPitch, nearPitch, tension);
            pitch *= 1f - lastRoundPitchDrop * RoundProgress();
            pitch *= 1f + Random.Range(-pitchJitter, pitchJitter);

            float loudness = chased ? chasedVolume : Mathf.Lerp(calmVolume, nearVolume, tension);
            loudness *= 1f + Random.Range(-volumeJitter, volumeJitter);
            float sfxVolume = SfxPlayer.Instance != null ? SfxPlayer.Instance.Volume : FallbackSfxVolume;

            _source.pitch = pitch;
            _source.volume = Mathf.Clamp01(loudness * volume * sfxVolume);
            _source.clip = breathClip;
            _source.Play();

            _lastBreathTime = Time.time;
            _lastPitch = pitch;
            _gapOffset = Random.Range(-gapJitter, gapJitter);
        }

        // 첫 라운드를 0, 마지막 라운드를 1로 본 진행도
        private static float RoundProgress()
        {
            GameManager manager = GameManager.Instance;
            if (manager == null || manager.TotalRounds <= 1)
                return 0f;

            return Mathf.Clamp01((manager.CurrentRound - 1f) / (manager.TotalRounds - 1f));
        }

        /// <summary>
        /// 숨 한 번의 실제 길이(초). 파일을 짧은 구간(WindowSeconds)으로 나눠 구간마다 평균 크기를 재고,
        /// 가장 큰 구간의 TailThreshold보다 큰 마지막 구간까지를 숨으로 본다.
        /// (낱개의 샘플로 재면 파일에 섞인 잔잡음 때문에 값이 크게 흔들린다)
        /// 소리 데이터를 읽을 수 없으면 파일 전체 길이를 쓴다.
        /// </summary>
        private static float MeasureBreathLength(AudioClip clip)
        {
            var samples = new float[clip.samples * clip.channels];
            if (!clip.GetData(samples, 0))
                return clip.length;

            int window = Mathf.Max(1, Mathf.RoundToInt(clip.frequency * WindowSeconds)) * clip.channels;
            int windowCount = Mathf.CeilToInt(samples.Length / (float)window);
            var loudness = new float[windowCount];
            float loudest = 0f;
            for (int w = 0; w < windowCount; w++)
            {
                int start = w * window;
                int end = Mathf.Min(start + window, samples.Length);
                float sum = 0f;
                for (int i = start; i < end; i++)
                    sum += samples[i] * samples[i];
                loudness[w] = Mathf.Sqrt(sum / (end - start));
                loudest = Mathf.Max(loudest, loudness[w]);
            }

            if (loudest <= 0f)
                return clip.length;

            for (int w = windowCount - 1; w >= 0; w--)
                if (loudness[w] > loudest * TailThreshold)
                    return Mathf.Min(clip.length, (w + 1) * WindowSeconds);

            return clip.length;
        }
    }
}
