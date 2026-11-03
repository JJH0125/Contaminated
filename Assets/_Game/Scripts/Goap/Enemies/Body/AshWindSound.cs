using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 재의 적이 내는 소리를 음원 파일 없이 코드로 만든다. (StaticNoiseSound와 같은 방식)
    ///   흩어져 있을 때  재가 바스러지는 사락거림이 띄엄띄엄 들린다
    ///   모여들수록      낮게 깔리던 바람이 점점 커지고, 높고 날카롭게 울부짖는 쪽으로 바뀐다
    ///
    /// 바람은 잡음을 "공명하는 필터"에 통과시켜 만든다. 공명이 셀수록 특정 높이의 소리가 휘파람처럼 울리고,
    /// 그 높이를 천천히 흔들어 바람이 몰아치는 느낌을 낸다.
    ///
    /// 거리와 방향은 직접 계산해서 크기와 좌우로 넣는다. 음원 없이 만든 소리라 Unity의 3D 처리에 맡기지 않는다.
    /// 값은 AshEnemyBody가 매 프레임 정해 준다. 이 함수는 소리 전용 스레드에서 불리므로 Unity API를 쓰지 않는다.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AshWindSound : MonoBehaviour
    {
        // 게임 스레드에서 정한 목표 값. 소리 스레드가 읽는다.
        private volatile float _targetLevel;
        private volatile float _targetPitch;
        private volatile float _targetHowl;
        private volatile float _targetRustle;

        // 소리 스레드에서 쓰는 값들
        private float _level, _pitch, _howl, _rustle;
        private float _low, _band, _crackle, _gustPhase;
        private float _sampleRate = 48000f;
        private uint _seed = 2891336453;

        // 값이 바뀔 때 툭 끊기지 않도록 샘플마다 조금씩 따라간다.
        private const float Follow = 0.0004f;

        /// <summary>
        /// 소리를 정한다.
        /// level: 크기(0~1, 거리와 볼륨까지 곱한 값) / pitch: 바람의 높이(Hz) / howl: 울부짖는 정도(0~1) /
        /// rustle: 재가 바스러지는 소리의 크기(0~1)
        /// </summary>
        public void Set(float level, float pitch, float howl, float rustle)
        {
            _targetLevel = Mathf.Clamp01(level);
            _targetPitch = Mathf.Max(pitch, 40f);
            _targetHowl = Mathf.Clamp01(howl);
            _targetRustle = Mathf.Clamp01(rustle);
        }

        private void Awake()
        {
            _sampleRate = AudioSettings.outputSampleRate;
            AudioSource source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            // 거리와 좌우는 AshEnemyBody가 계산해 넣는다.
            source.spatialBlend = 0f;
            source.Play();
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            float targetLevel = _targetLevel;
            float targetPitch = _targetPitch;
            float targetHowl = _targetHowl;
            float targetRustle = _targetRustle;

            for (int i = 0; i < data.Length; i += channels)
            {
                _level += (targetLevel - _level) * Follow;
                _pitch += (targetPitch - _pitch) * Follow;
                _howl += (targetHowl - _howl) * Follow;
                _rustle += (targetRustle - _rustle) * Follow;

                // 바람이 몰아치듯 높이가 천천히 오르내린다.
                _gustPhase += 0.35f / _sampleRate;
                if (_gustPhase > 1f)
                    _gustPhase -= 1f;
                float gust = 1f + 0.25f * Mathf.Sin(_gustPhase * 6.2831853f) + 0.1f * Mathf.Sin(_gustPhase * 17.3f);

                // 공명하는 필터(상태 변수 필터). 울부짖을수록 공명이 세져 휘파람처럼 울린다.
                float white = NextRandom() * 2f - 1f;
                float f = 2f * Mathf.Sin(3.14159265f * Mathf.Min(_pitch * gust, _sampleRate * 0.2f) / _sampleRate);
                float damping = Mathf.Lerp(1.2f, 0.12f, _howl);
                _low += f * _band;
                float high = white - _low - damping * _band;
                _band += f * high;
                float wind = _band * Mathf.Lerp(0.35f, 0.18f, _howl);

                // 재가 바스러지는 소리: 띄엄띄엄 생기는 짧은 바삭거림
                if (NextRandom() < 0.0015f * _rustle)
                    _crackle = NextRandom() * 2f - 1f;
                _crackle *= 0.93f;

                float sample = (wind + _crackle * _rustle * 0.6f) * _level;
                for (int c = 0; c < channels; c++)
                    data[i + c] = sample;
            }
        }

        // 0~1 사이의 빠른 의사 난수 (xorshift). 소리 스레드에서는 UnityEngine.Random을 쓸 수 없다.
        private float NextRandom()
        {
            _seed ^= _seed << 13;
            _seed ^= _seed >> 17;
            _seed ^= _seed << 5;
            return (_seed & 0xFFFFFF) / 16777216f;
        }
    }
}
