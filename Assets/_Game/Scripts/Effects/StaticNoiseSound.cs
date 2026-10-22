using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 음원 파일 없이 코드로 지지직거리는 잡음 소리를 만든다. 크기는 Level로 정한다(0이면 무음).
    ///
    /// Unity는 AudioSource가 소리를 내보내기 직전에 OnAudioFilterRead를 불러 소리 데이터를 건네준다.
    /// 여기서 그 데이터를 무작위 값으로 채워 잡음을 만든다. 이 함수는 게임과 다른 스레드(소리 전용)에서
    /// 아주 자주 불리므로, Unity API를 쓰지 않고 계산만 한다.
    ///
    /// 소리를 다듬는 방법:
    ///   무작위 값을 그대로 내면 날카로운 "쉬익" 소리라서, 높은 소리를 깎아 탁한 "치직" 소리로 만든다
    ///   아주 낮은 웅웅거림도 깎아서, 작은 스피커에서 새어 나오는 소리처럼 들리게 한다
    ///   가끔 "틱" 하는 짧은 튐을 섞어 전파가 끊기는 느낌을 준다
    ///
    /// CctvNoiseEffect가 자식 오브젝트에 붙여 쓴다. 카메라 오브젝트에는 AudioListener가 있어서,
    /// 거기에 붙이면 게임의 모든 소리가 이 잡음으로 덮이므로 반드시 따로 둔다.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class StaticNoiseSound : MonoBehaviour
    {
        // 게임 스레드에서 정한 목표 크기. 소리 스레드가 읽는다.
        private volatile float _targetLevel;
        // 소리 스레드에서 쓰는 값들
        private float _level;
        private float _muffled;
        private float _rumble;
        private float _click;
        private uint _seed = 2463534242;

        // 크기가 바뀔 때 툭 끊기는 소리가 나지 않도록 샘플마다 조금씩 따라간다. (약 40ms에 걸쳐 따라감)
        private const float LevelFollow = 0.0005f;
        // 높은 소리를 깎는 정도. 작을수록 탁하다.
        private const float Muffle = 0.35f;
        // 낮은 웅웅거림을 깎는 정도
        private const float RumbleCut = 0.02f;
        // 샘플 하나마다 "틱"이 날 확률 (크기가 최대일 때). 48000Hz 기준 1초에 약 30번
        private const float ClickChance = 0.0006f;

        /// <summary>잡음의 크기 (0~1). 게임 스레드에서 매 프레임 정해 준다.</summary>
        public float Level
        {
            set => _targetLevel = Mathf.Clamp01(value);
        }

        private void Awake()
        {
            AudioSource source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            // 화면 노이즈처럼 위치와 상관없이 같은 크기로 들린다.
            source.spatialBlend = 0f;
            // 음원 없이 재생하면 OnAudioFilterRead가 빈 데이터를 받아 채우게 된다.
            source.Play();
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            float target = _targetLevel;

            for (int i = 0; i < data.Length; i += channels)
            {
                _level += (target - _level) * LevelFollow;

                float white = NextRandom() * 2f - 1f;
                _muffled += (white - _muffled) * Muffle;
                _rumble += (_muffled - _rumble) * RumbleCut;
                float hiss = _muffled - _rumble;

                // 가끔 짧은 튐. 크기가 클수록 자주 나고, 금방 사라진다.
                if (NextRandom() < ClickChance * _level)
                    _click = NextRandom() * 2f - 1f;
                _click *= 0.9f;

                float sample = (hiss * 0.8f + _click) * _level;
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
