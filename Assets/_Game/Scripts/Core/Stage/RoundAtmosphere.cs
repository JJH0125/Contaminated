using System.Collections.Generic;
using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 라운드마다 숲의 분위기를 바꾼다. 1라운드가 가장 밝고, 라운드가 오를수록 어두워지고 안개가 짙어진다.
    /// (횃불과 모닥불의 빛은 건드리지 않는다. 숲이 어두워질수록 불빛이 더 중요해진다)
    ///
    /// 바꾸는 것: 씬에 놓아 둔 방향광(해·달)의 밝기·색·그림자, 주변광, 안개 색과 짙기.
    /// 값은 씬에 저장된 값을 기준으로 라운드별 구성(stage.rounds)의 배율을 곱한다.
    /// 씬의 값은 처음 한 번만 읽어 기억해 두므로, 라운드를 거듭해도 배율이 겹쳐 곱해지지 않는다.
    ///
    /// 타이틀 화면의 밝은 숲(GameTuning > Title)도 여기서 만든다. TitleBlend가 1이면 타이틀 모습, 0이면 라운드 모습이고,
    /// 그 사이 값이면 둘을 섞는다. 시작을 누르면 TitleBackground가 이 값을 1에서 0으로 내려 숲이 서서히 어두워진다.
    ///
    /// 디버그용 깨끗한 화면(CleanViewDebug, F2)이 켜져 있으면 밝기는 씬 그대로 두고 안개를 끈다.
    ///
    /// StageLayout이 라운드를 시작할 때 Apply를, TitleBackground가 타이틀과 시작 전환 동안 SetTitleBlend를 부른다.
    /// </summary>
    public static class RoundAtmosphere
    {
        private struct SunBase
        {
            public float Intensity;
            public Color Color;
            public LightShadows Shadows;
        }

        private static bool _captured;
        private static readonly Dictionary<Light, SunBase> _suns = new();
        private static Color _ambientSky, _ambientEquator, _ambientGround;
        private static float _ambientIntensity;
        private static Color _fogColor;
        private static float _fogDensity;
        private static bool _fog;
        // 마지막으로 적용한 라운드의 구성. 깨끗한 화면을 켜고 끌 때 다시 적용한다. (타이틀에서는 null)
        private static RoundSetup _current;

        /// 타이틀 모습을 섞는 정도. 1이면 타이틀의 밝은 숲, 0이면 라운드 모습.
        public static float TitleBlend { get; private set; }

        public static void Apply(RoundSetup round)
        {
            _current = round;
            Update();
        }

        /// <summary>타이틀 모습을 섞는 정도를 바꾼다. (TitleBackground)</summary>
        public static void SetTitleBlend(float blend)
        {
            TitleBlend = Mathf.Clamp01(blend);
            Update();
        }

        /// <summary>지금 구성을 다시 적용한다. (깨끗한 화면을 켜고 껐을 때)</summary>
        public static void Refresh()
        {
            if (_current != null || TitleBlend > 0f)
                Update();
        }

        private static void Update()
        {
            Capture();

            bool clean = CleanViewDebug.On;
            float light = clean || _current == null ? 1f : _current.lightIntensity;
            float fog = _current == null ? 1f : _current.fogDensity;

            TitleTuning title = GameTuning.Instance.title;
            float t = TitleBlend;

            foreach (KeyValuePair<Light, SunBase> pair in _suns)
            {
                Light sun = pair.Key;
                if (sun == null)
                    continue;
                SunBase sunBase = pair.Value;
                sun.intensity = Mathf.Lerp(sunBase.Intensity * light, sunBase.Intensity * title.sunIntensity, t);
                sun.color = Color.Lerp(sunBase.Color, title.sunColor, t);
                sun.shadows = t > 0.5f && title.sunShadows ? LightShadows.Soft : sunBase.Shadows;
            }

            RenderSettings.ambientSkyColor = Color.Lerp(_ambientSky * light, title.ambientColor, t);
            RenderSettings.ambientEquatorColor = Color.Lerp(_ambientEquator * light, title.ambientColor * 0.85f, t);
            RenderSettings.ambientGroundColor = Color.Lerp(_ambientGround * light, title.ambientColor * 0.6f, t);
            RenderSettings.ambientIntensity = Mathf.Lerp(_ambientIntensity * light, 1f, t);
            RenderSettings.fogColor = Color.Lerp(_fogColor, title.fogColor, t);
            RenderSettings.fogDensity = Mathf.Lerp(_fogDensity * fog, title.fogDensity, t);
            RenderSettings.fog = t > 0f || (_fog && !clean);
        }

        // 씬에 저장된 값을 한 번만 읽어 둔다. (씬을 다시 불러오면 Light가 바뀌므로 그때는 다시 읽는다)
        private static void Capture()
        {
            bool lightsGone = false;
            foreach (Light sun in _suns.Keys)
                if (sun == null)
                    lightsGone = true;
            if (_captured && !lightsGone)
                return;

            _suns.Clear();
            foreach (Light light in Object.FindObjectsOfType<Light>())
                if (light.type == LightType.Directional)
                    _suns[light] = new SunBase { Intensity = light.intensity, Color = light.color, Shadows = light.shadows };

            if (!_captured)
            {
                _ambientSky = RenderSettings.ambientSkyColor;
                _ambientEquator = RenderSettings.ambientEquatorColor;
                _ambientGround = RenderSettings.ambientGroundColor;
                _ambientIntensity = RenderSettings.ambientIntensity;
                _fogColor = RenderSettings.fogColor;
                _fogDensity = RenderSettings.fogDensity;
                _fog = RenderSettings.fog;
            }
            _captured = true;
        }
    }
}
