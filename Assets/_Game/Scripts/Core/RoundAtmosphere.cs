using System.Collections.Generic;
using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 라운드마다 숲의 분위기를 바꾼다. 1라운드가 가장 밝고, 라운드가 오를수록 어두워지고 안개가 짙어진다.
    /// (횃불과 모닥불의 빛은 건드리지 않는다. 숲이 어두워질수록 불빛이 더 중요해진다)
    ///
    /// 바꾸는 것: 씬에 놓아 둔 방향광(해·달)의 밝기, 주변광, 안개 짙기.
    /// 값은 씬에 저장된 값을 기준으로 라운드별 구성(stage.rounds)의 배율을 곱한다.
    /// 씬의 값은 처음 한 번만 읽어 기억해 두므로, 라운드를 거듭해도 배율이 겹쳐 곱해지지 않는다.
    ///
    /// StageLayout이 라운드를 시작할 때 부른다.
    /// </summary>
    public static class RoundAtmosphere
    {
        private static bool _captured;
        private static readonly Dictionary<Light, float> _sunIntensity = new();
        private static Color _ambientSky, _ambientEquator, _ambientGround;
        private static float _ambientIntensity;
        private static float _fogDensity;

        public static void Apply(RoundSetup round)
        {
            Capture();

            foreach (KeyValuePair<Light, float> sun in _sunIntensity)
                if (sun.Key != null)
                    sun.Key.intensity = sun.Value * round.lightIntensity;

            RenderSettings.ambientSkyColor = _ambientSky * round.lightIntensity;
            RenderSettings.ambientEquatorColor = _ambientEquator * round.lightIntensity;
            RenderSettings.ambientGroundColor = _ambientGround * round.lightIntensity;
            RenderSettings.ambientIntensity = _ambientIntensity * round.lightIntensity;
            RenderSettings.fogDensity = _fogDensity * round.fogDensity;
        }

        // 씬에 저장된 값을 한 번만 읽어 둔다. (씬을 다시 불러오면 Light가 바뀌므로 그때는 다시 읽는다)
        private static void Capture()
        {
            bool lightsGone = false;
            foreach (Light sun in _sunIntensity.Keys)
                if (sun == null)
                    lightsGone = true;
            if (_captured && !lightsGone)
                return;

            _sunIntensity.Clear();
            foreach (Light light in Object.FindObjectsOfType<Light>())
                if (light.type == LightType.Directional)
                    _sunIntensity[light] = light.intensity;

            if (!_captured)
            {
                _ambientSky = RenderSettings.ambientSkyColor;
                _ambientEquator = RenderSettings.ambientEquatorColor;
                _ambientGround = RenderSettings.ambientGroundColor;
                _ambientIntensity = RenderSettings.ambientIntensity;
                _fogDensity = RenderSettings.fogDensity;
            }
            _captured = true;
        }
    }
}
