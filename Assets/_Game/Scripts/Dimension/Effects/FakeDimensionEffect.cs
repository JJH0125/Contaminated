using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace Squad
{
    /// <summary>
    /// 플레이어가 Fake 차원에 있는 동안 화면에 일렁임과 노이즈를 입힌다.
    /// 차원을 넘는 순간 뚝 바뀌지 않고 서서히 나타나고 사라진다.
    ///
    /// 화면 효과는 Unity의 Post Processing 패키지로 만든다.
    /// 이 패키지는 "어떤 효과를 얼마나 줄지"를 담은 Volume을 씬에 두고, 카메라의 PostProcessLayer가 그것을 읽어
    /// 화면에 입히는 구조다. 여기서는 Volume을 시작할 때 코드로 하나 만들고, 그 세기(weight)만 0과 1 사이로 조절한다.
    ///
    /// 쓰는 효과:
    ///   Grain                 화면 전체에 자글자글한 노이즈
    ///   Lens Distortion       화면이 볼록·오목하게 휘는 왜곡. 세기와 중심을 계속 흔들어 일렁이게 한다
    ///   Chromatic Aberration  가장자리에서 색이 번져 어긋나 보인다
    ///   Vignette              가장자리가 어두워진다
    ///   Color Grading         색을 빼고 한 가지 색조를 입힌다
    ///
    /// 씬 구성:
    ///   Main Camera에 붙인다. PostProcessLayer가 없으면 함께 붙는다.
    /// </summary>
    [RequireComponent(typeof(PostProcessLayer))]
    public class FakeDimensionEffect : MonoBehaviour
    {
        // 수치는 GameTuning 에셋(Resources/GameTuning)에서 읽는다.
        private float fadeTime => GameTuning.Instance.fakeScreen.fadeTime;
        private float grainIntensity => GameTuning.Instance.fakeScreen.grainIntensity;
        private float grainSize => GameTuning.Instance.fakeScreen.grainSize;
        private float wobbleAmount => GameTuning.Instance.fakeScreen.wobbleAmount;
        private float wobbleSpeed => GameTuning.Instance.fakeScreen.wobbleSpeed;
        private float colorFringe => GameTuning.Instance.fakeScreen.colorFringe;
        private float vignette => GameTuning.Instance.fakeScreen.vignette;
        private float saturation => GameTuning.Instance.fakeScreen.saturation;
        private Color tint => GameTuning.Instance.fakeScreen.tint;

        // PostProcessLayer에 Volume 레이어가 지정되어 있지 않을 때 대신 쓸 레이어 (Unity 기본 레이어인 TransparentFX)
        private const int FallbackVolumeLayer = 1;

        private PostProcessVolume _volume;
        private LensDistortion _lens;

        private void Awake()
        {
            PostProcessLayer layer = GetComponent<PostProcessLayer>();
            // PostProcessLayer는 자기가 읽기로 한 레이어에 있는 Volume만 본다.
            // 아무 레이어도 지정되어 있지 않으면 어떤 효과도 나오지 않으므로 하나를 지정해 준다.
            if (layer.volumeLayer.value == 0)
                layer.volumeLayer = 1 << FallbackVolumeLayer;

            var grain = ScriptableObject.CreateInstance<Grain>();
            grain.enabled.Override(true);
            grain.intensity.Override(grainIntensity);
            grain.size.Override(grainSize);
            grain.colored.Override(false);

            _lens = ScriptableObject.CreateInstance<LensDistortion>();
            _lens.enabled.Override(true);
            _lens.intensity.Override(0f);
            _lens.centerX.Override(0f);
            _lens.centerY.Override(0f);

            var fringe = ScriptableObject.CreateInstance<ChromaticAberration>();
            fringe.enabled.Override(true);
            fringe.intensity.Override(colorFringe);

            var edge = ScriptableObject.CreateInstance<Vignette>();
            edge.enabled.Override(true);
            edge.intensity.Override(vignette);

            var grading = ScriptableObject.CreateInstance<ColorGrading>();
            grading.enabled.Override(true);
            // 이 프로젝트의 색 공간(Gamma)에서도 그대로 동작하는 방식으로 색을 조절한다.
            grading.gradingMode.Override(GradingMode.LowDefinitionRange);
            grading.saturation.Override(saturation);
            grading.colorFilter.Override(tint);

            // 화면 어디에 있든 적용되는 Volume을, PostProcessLayer가 읽는 레이어 중 하나에 만든다.
            _volume = PostProcessManager.instance.QuickVolume(
                LowestLayerIn(layer.volumeLayer), 100f, grain, _lens, fringe, edge, grading);
            _volume.weight = 0f;
        }

        private void Update()
        {
            // 일시정지 중에도 효과가 자연스럽게 이어지도록, 게임 시간이 아니라 실제 시간을 쓴다.
            float target = IsPlayerInFake() ? 1f : 0f;
            float step = fadeTime > 0f ? Time.unscaledDeltaTime / fadeTime : 1f;
            _volume.weight = Mathf.MoveTowards(_volume.weight, target, step);

            // 효과가 꺼져 있는 동안에는 흔들 필요가 없다.
            if (_volume.weight <= 0f)
                return;

            // 휘는 세기를 볼록과 오목 사이로 오가게 하고, 휘는 중심을 원을 그리며 돌려서 화면이 출렁이게 한다.
            // 세 값의 빠르기를 서로 다르게 해야 같은 모양이 반복되는 느낌이 덜하다.
            float time = Time.unscaledTime * wobbleSpeed;
            _lens.intensity.value = Mathf.Sin(time) * wobbleAmount;
            _lens.centerX.value = Mathf.Sin(time * 0.7f) * 0.15f;
            _lens.centerY.value = Mathf.Cos(time * 0.9f) * 0.15f;
        }

        // 코드로 만든 Volume은 씬과 함께 저절로 정리되지 않으므로 직접 지운다.
        private void OnDestroy()
        {
            if (_volume != null)
                RuntimeUtilities.DestroyVolume(_volume, true, true);
        }

        // 플레이어가 지금 Fake 차원에 있는가. 라운드가 진행 중이 아니면(Stage가 없으면) false.
        private static bool IsPlayerInFake()
        {
            DimensionController controller = DimensionController.Instance;
            if (controller == null || controller.player == null)
                return false;

            return controller.player.myDimension == Dimension.Fake;
        }

        // mask에 켜져 있는 레이어 중 번호가 가장 작은 것
        private static int LowestLayerIn(LayerMask mask)
        {
            for (int i = 0; i < 32; i++)
                if ((mask.value & (1 << i)) != 0)
                    return i;

            return FallbackVolumeLayer;
        }
    }
}
