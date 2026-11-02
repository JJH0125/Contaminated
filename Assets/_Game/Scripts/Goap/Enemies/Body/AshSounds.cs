using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 재의 적이 발각하거나 잡을 때 내는 짧은 소리를 음원 파일 없이 코드로 만든다. 처음 쓸 때 한 번 만들어 두고 계속 쓴다.
    ///   Spotted  숨을 빠르게 안으로 들이켜는 바람이 툭 끊기고, 높은 유리에 금이 가는 소리 (0.75초)
    ///   Burst    재가 한 번에 터지듯 흩어지는 소리
    ///   Shatter  눈앞에서 결정이 모여 번쩍일 때 유리가 갈라지는 소리
    ///
    /// 모두 플레이어에게만 들리는 2D 소리다. 게임 속 소리(SoundEmitter)로 내지 않으므로 다른 적은 듣지 못한다.
    /// (Real 적의 포효와 달리, 재의 적이 발각해도 다른 재의 적이 모여들지 않는다)
    /// </summary>
    public static class AshSounds
    {
        private const int Rate = 44100;

        private static AudioClip _spotted;
        private static AudioClip _burst;
        private static AudioClip _shatter;

        public static AudioClip Spotted => _spotted != null ? _spotted : _spotted = Create("Ash Spotted", BuildSpotted());
        public static AudioClip Burst => _burst != null ? _burst : _burst = Create("Ash Burst", BuildBurst());
        public static AudioClip Shatter => _shatter != null ? _shatter : _shatter = Create("Ash Shatter", BuildShatter());

        // 들이켜는 숨: 잡음을 공명 필터에 통과시키고, 그 높이와 크기를 함께 끌어올리다가 툭 끊는다.
        // 내쉬는 숨은 커졌다 잦아들고, 들이켜는 숨은 점점 커지다 멈추므로 끝을 자르는 것이 핵심이다.
        private static float[] BuildSpotted()
        {
            var data = new float[(int)(0.75f * Rate)];
            var random = new System.Random(7);
            const float InhaleEnd = 0.42f;
            const float CutTime = 0.015f;
            float low = 0f, band = 0f;

            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)Rate;
                if (t > InhaleEnd + CutTime)
                    break;

                float progress = Mathf.Clamp01(t / InhaleEnd);
                float cutoff = Mathf.Lerp(500f, 2800f, progress * progress);
                float f = 2f * Mathf.Sin(Mathf.PI * cutoff / Rate);
                float white = (float)random.NextDouble() * 2f - 1f;
                low += f * band;
                float high = white - low - 0.5f * band;
                band += f * high;

                float envelope = Mathf.Pow(progress, 2.2f);
                if (t > InhaleEnd)
                    envelope *= 1f - (t - InhaleEnd) / CutTime;
                data[i] += (band * 0.6f + high * 0.15f) * envelope;
            }

            // 숨이 끊기는 순간 높은 유리에 금이 간다.
            AddGlass(data, random, InhaleEnd - 0.02f, new[] { 3170f, 4730f, 6290f, 8110f }, 0.35f, 0.12f, 9, 0.2f);
            Normalize(data, 0.9f);
            return data;
        }

        // 터지듯 흩어짐: 짧게 치솟았다 빠르게 줄어드는 잡음. 높은 소리부터 사라져 멀어지는 느낌이 나고, 낮은 쿵이 받친다.
        private static float[] BuildBurst()
        {
            var data = new float[(int)(0.4f * Rate)];
            var random = new System.Random(11);
            float low = 0f;

            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)Rate;
                float cutoff = Mathf.Lerp(5000f, 500f, Mathf.Clamp01(t / 0.3f));
                low += (1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate)) * ((float)random.NextDouble() * 2f - 1f - low);
                float envelope = Mathf.Clamp01(t / 0.004f) * Mathf.Exp(-t / 0.1f);
                float thump = Mathf.Sin(2f * Mathf.PI * 55f * t) * Mathf.Exp(-t / 0.08f) * 0.6f;
                data[i] = low * 1.6f * envelope + thump;
            }

            Normalize(data, 0.85f);
            return data;
        }

        // 눈앞에서 갈라지는 유리: 발각 소리의 금보다 크고 길게 울린다.
        private static float[] BuildShatter()
        {
            var data = new float[(int)(0.7f * Rate)];
            var random = new System.Random(23);
            AddGlass(data, random, 0f, new[] { 2890f, 3970f, 5210f, 6650f, 8420f, 10100f }, 0.4f, 0.22f, 22, 0.18f);
            Normalize(data, 0.9f);
            return data;
        }

        // 유리에 금이 가는 소리: 날카로운 딸깍 + 서로 어울리지 않는 높이들의 울림 + 뒤따르는 잔 금들
        private static void AddGlass(float[] data, System.Random random, float start, float[] tones,
                                     float amplitude, float ring, int crackles, float crackleSpread)
        {
            AddClick(data, random, start, 0.6f, 0.003f);
            for (int k = 0; k < tones.Length; k++)
            {
                float fade = 1f - k / (float)tones.Length;
                AddPing(data, start, tones[k], amplitude * (0.4f + 0.6f * fade), ring * (0.4f + 0.6f * fade));
            }

            for (int k = 0; k < crackles; k++)
            {
                // 금은 처음에 몰려 생기고 점점 드문드문해진다.
                float r = (float)random.NextDouble();
                float t = start + crackleSpread * r * r;
                AddPing(data, t, 2500f + (float)random.NextDouble() * 6500f,
                        0.15f + (float)random.NextDouble() * 0.15f, 0.015f + (float)random.NextDouble() * 0.03f);
                AddClick(data, random, t, 0.2f, 0.0015f);
            }
        }

        // 한 높이로 울리다 잦아드는 소리
        private static void AddPing(float[] data, float start, float frequency, float amplitude, float decay)
        {
            int from = Mathf.Max(0, (int)(start * Rate));
            int to = Mathf.Min(data.Length, from + (int)(decay * 6f * Rate));
            for (int i = from; i < to; i++)
            {
                float t = (i - from) / (float)Rate;
                data[i] += amplitude * Mathf.Exp(-t / decay) * Mathf.Sin(2f * Mathf.PI * frequency * t);
            }
        }

        // 아주 짧은 잡음 한 번
        private static void AddClick(float[] data, System.Random random, float start, float amplitude, float length)
        {
            int from = Mathf.Max(0, (int)(start * Rate));
            int count = (int)(length * Rate);
            for (int i = 0; i < count && from + i < data.Length; i++)
                data[from + i] += amplitude * (1f - i / (float)count) * ((float)random.NextDouble() * 2f - 1f);
        }

        private static void Normalize(float[] data, float peak)
        {
            float max = 0.0001f;
            foreach (float sample in data)
                max = Mathf.Max(max, Mathf.Abs(sample));
            for (int i = 0; i < data.Length; i++)
                data[i] *= peak / max;
        }

        private static AudioClip Create(string clipName, float[] data)
        {
            AudioClip clip = AudioClip.Create(clipName, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            // 씬이 바뀔 때 "쓰지 않는 에셋"으로 지워지지 않게 한다.
            clip.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return clip;
        }
    }
}
