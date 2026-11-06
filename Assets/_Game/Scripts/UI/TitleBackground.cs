using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace Squad
{
    /// <summary>
    /// 타이틀 화면의 배경. 게임과 같은 숲을 밝고 생기 없는 모습으로 보여 주며 카메라가 천천히 돈다.
    ///
    /// 타이틀에 있는 동안:
    ///   카메라   봉우리 둘레를 한 바퀴 loopTime초에 돈다. 다가갔다 멀어지고 높이도 오르내리며, 한 바퀴가 끝나면 처음 자리로 이어진다
    ///   빛       흐린 낮빛, 옅은 잿빛 안개 (RoundAtmosphere.SetTitleBlend)
    ///   화면     색을 조금 뺀다 (Post Processing의 Color Grading)
    ///   연기     꺼진 모닥불의 연기 기둥 하나 (Campfire.CreateSmokeColumn)
    ///   깜빡임   몇십 초에 한 번, 1초쯤 재의 차원처럼 흑백·노이즈·일렁임으로 바뀌었다 돌아온다
    /// 시작을 누르면 startTransitionTime초에 걸쳐 빛이 빠져 라운드 모습이 되고, 카메라도 타이틀 자리에서 플레이어 쪽으로 옮겨 간다.
    ///
    /// 타이틀에는 Stage가 없으므로 적과 AI는 처음부터 없다. 숲만 그린다.
    /// 수치는 GameTuning > Title.
    ///
    /// 씬 구성: 따로 붙이지 않는다. GameManager가 시작할 때 Main Camera에 붙인다. (Follow 다음에 돌도록 실행 순서를 늦춘다)
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class TitleBackground : MonoBehaviour
    {
        private static TitleTuning Tuning => GameTuning.Instance.title;

        // FakeDimensionEffect(100)보다 위에 둔다. 깜빡임이 Fake 화면 위에 덮이게.
        private const float TitlePriority = 90f;
        private const float FlashPriority = 110f;

        private PostProcessVolume _titleVolume;
        private PostProcessVolume _flashVolume;
        private ColorGrading _titleGrading;
        private ColorGrading _flashGrading;
        private LensDistortion _flashLens;
        private ParticleSystem _smoke;

        private Terrain _terrain;
        private Vector3 _peak;
        private Vector3 _smokeSpot;

        private bool _wasTitle;
        private float _orbitTime;
        // 시작 전환의 진행도(0~1). 전환 중이 아니면 -1.
        private float _transition = -1f;
        private Vector3 _titlePosition;
        private Quaternion _titleRotation;
        private float _nextFlash;
        private float _flashStart = -1f;

        /// <summary>카메라에 이 컴포넌트가 없으면 붙인다. (GameManager)</summary>
        public static void Ensure(Camera camera)
        {
            if (camera != null && camera.GetComponent<TitleBackground>() == null)
                camera.gameObject.AddComponent<TitleBackground>();
        }

        private void Awake()
        {
            CreateVolumes();
        }

        private void OnDestroy()
        {
            if (_titleVolume != null)
                RuntimeUtilities.DestroyVolume(_titleVolume, true, true);
            if (_flashVolume != null)
                RuntimeUtilities.DestroyVolume(_flashVolume, true, true);
        }

        private void LateUpdate()
        {
            GameManager manager = GameManager.Instance;
            if (manager == null)
                return;

            bool title = manager.CurrentState == GameState.Title;
            if (title && !_wasTitle)
                EnterTitle();
            else if (!title && _wasTitle)
                BeginTransition();
            _wasTitle = title;

            // 타이틀은 시간이 멈춘 화면일 수도 있으므로 실제 시간으로 움직인다.
            float deltaTime = Time.unscaledDeltaTime;
            if (title)
            {
                MoveCamera(deltaTime);
                UpdateFlash();
            }
            else if (_transition >= 0f)
            {
                UpdateTransition(deltaTime);
            }
        }

        // ── 타이틀 ───────────────────────────────────────────────────

        private void EnterTitle()
        {
            _transition = -1f;
            FindMap();
            RoundAtmosphere.SetTitleBlend(1f);
            SetTitleScreen(1f);
            ScheduleFlash();
            _flashStart = -1f;

            if (Tuning.smoke)
            {
                if (_smoke == null)
                    _smoke = CreateSmoke();
                if (_smoke != null)
                {
                    _smoke.transform.position = _smokeSpot + Vector3.up;
                    _smoke.Play();
                }
            }
        }

        // 봉우리(지형 한가운데)와 연기 기둥의 자리를 정한다. 지형이 없으면 카메라를 움직이지 않는다.
        private void FindMap()
        {
            _terrain = Terrain.activeTerrain;
            if (_terrain == null)
                return;

            Vector3 size = _terrain.terrainData.size;
            _peak = Ground(_terrain.transform.position + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f));
            _smokeSpot = Ground(_peak + ZoneMap.Direction(Tuning.smokeAngle) * Tuning.smokeDistance);
        }

        private Vector3 Ground(Vector3 position)
        {
            position.y = _terrain.SampleHeight(position) + _terrain.transform.position.y;
            return position;
        }

        private void MoveCamera(float deltaTime)
        {
            if (_terrain == null)
                return;

            TitleTuning tuning = Tuning;
            _orbitTime += deltaTime;
            float phase = _orbitTime / Mathf.Max(tuning.loopTime, 1f) * Mathf.PI * 2f;

            // 거리와 높이도 한 바퀴의 정수배 주기로 바꿔야 한 바퀴가 끝날 때 처음 자리와 이어진다.
            float radius = tuning.orbitRadius + tuning.radiusSwing * Mathf.Sin(phase * 2f);
            Vector3 position = Ground(_peak + new Vector3(Mathf.Sin(phase), 0f, Mathf.Cos(phase)) * radius);
            position.y += tuning.height + tuning.heightSwing * Mathf.Sin(phase * 3f);

            Vector3 focus = Vector3.Lerp(_peak, _smokeSpot, tuning.lookAtSmoke) + Vector3.up * tuning.lookHeight;
            Quaternion rotation = Quaternion.LookRotation(focus - position);

            transform.SetPositionAndRotation(position, rotation);
            _titlePosition = position;
            _titleRotation = rotation;
        }

        // ── 재의 차원 깜빡임 ─────────────────────────────────────────

        private void ScheduleFlash()
        {
            TitleTuning tuning = Tuning;
            _nextFlash = Time.unscaledTime + tuning.flashInterval + Random.Range(-1f, 1f) * tuning.flashIntervalRandom;
        }

        private void UpdateFlash()
        {
            if (_flashVolume == null)
                return;

            TitleTuning tuning = Tuning;
            float now = Time.unscaledTime;
            if (_flashStart < 0f && tuning.flashInterval > 0f && now >= _nextFlash)
                _flashStart = now;

            float weight = 0f;
            if (_flashStart >= 0f)
            {
                float elapsed = now - _flashStart;
                if (elapsed >= tuning.flashDuration)
                {
                    _flashStart = -1f;
                    ScheduleFlash();
                }
                else
                {
                    // 들어갈 때와 나올 때만 flashFade초에 걸쳐 바뀌고, 그 사이는 완전히 재의 차원이다.
                    float edge = Mathf.Min(elapsed, tuning.flashDuration - elapsed);
                    weight = Mathf.Clamp01(edge / Mathf.Max(tuning.flashFade, 0.01f));
                }
            }

            _flashVolume.weight = weight;
            if (weight > 0f)
            {
                FakeScreenTuning fake = GameTuning.Instance.fakeScreen;
                float time = now * fake.wobbleSpeed;
                _flashLens.intensity.value = Mathf.Sin(time) * fake.wobbleAmount;
                _flashGrading.saturation.value = tuning.flashSaturation;
                _flashGrading.brightness.value = tuning.flashBrightness;
            }
        }

        // ── 시작 전환 ────────────────────────────────────────────────

        private void BeginTransition()
        {
            if (_smoke != null)
                _smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (_flashVolume != null)
                _flashVolume.weight = 0f;
            _flashStart = -1f;

            // 시연 씬은 타이틀을 건너뛰므로 바로 라운드 모습으로 간다.
            if (Tuning.startTransitionTime <= 0f || DemoScenario.InDemoScene)
            {
                FinishTransition();
                return;
            }
            _transition = 0f;
        }

        private void UpdateTransition(float deltaTime)
        {
            _transition = Mathf.Min(1f, _transition + deltaTime / Tuning.startTransitionTime);
            float blend = Mathf.SmoothStep(0f, 1f, _transition);

            RoundAtmosphere.SetTitleBlend(1f - blend);
            SetTitleScreen(1f - blend);

            // Follow가 이번 프레임에 놓은 자리(플레이어 쪽)로 타이틀 자리에서 옮겨 간다.
            transform.SetPositionAndRotation(
                Vector3.Lerp(_titlePosition, transform.position, blend),
                Quaternion.Slerp(_titleRotation, transform.rotation, blend));

            if (_transition >= 1f)
                FinishTransition();
        }

        private void FinishTransition()
        {
            _transition = -1f;
            RoundAtmosphere.SetTitleBlend(0f);
            SetTitleScreen(0f);
        }

        // ── 화면 효과 ────────────────────────────────────────────────

        private void SetTitleScreen(float weight)
        {
            if (_titleVolume == null)
                return;
            _titleVolume.weight = weight;
            _titleGrading.saturation.value = Tuning.saturation;
        }

        // FakeDimensionEffect와 같은 방식으로, 화면 어디서나 적용되는 Volume을 코드로 만든다.
        private void CreateVolumes()
        {
            PostProcessLayer layer = GetComponent<PostProcessLayer>();
            if (layer == null)
                return;
            int volumeLayer = LowestLayerIn(layer.volumeLayer);

            _titleGrading = ScriptableObject.CreateInstance<ColorGrading>();
            _titleGrading.enabled.Override(true);
            _titleGrading.gradingMode.Override(GradingMode.LowDefinitionRange);
            _titleGrading.saturation.Override(Tuning.saturation);
            _titleVolume = PostProcessManager.instance.QuickVolume(volumeLayer, TitlePriority, _titleGrading);
            _titleVolume.weight = 0f;

            FakeScreenTuning fake = GameTuning.Instance.fakeScreen;
            var grain = ScriptableObject.CreateInstance<Grain>();
            grain.enabled.Override(true);
            grain.intensity.Override(fake.grainIntensity);
            grain.size.Override(fake.grainSize);
            grain.colored.Override(false);

            _flashLens = ScriptableObject.CreateInstance<LensDistortion>();
            _flashLens.enabled.Override(true);
            _flashLens.intensity.Override(0f);

            var fringe = ScriptableObject.CreateInstance<ChromaticAberration>();
            fringe.enabled.Override(true);
            fringe.intensity.Override(fake.colorFringe);

            var edge = ScriptableObject.CreateInstance<Vignette>();
            edge.enabled.Override(true);
            edge.intensity.Override(fake.vignette);

            _flashGrading = ScriptableObject.CreateInstance<ColorGrading>();
            _flashGrading.enabled.Override(true);
            _flashGrading.gradingMode.Override(GradingMode.LowDefinitionRange);
            _flashGrading.saturation.Override(Tuning.flashSaturation);
            _flashGrading.brightness.Override(Tuning.flashBrightness);
            _flashGrading.colorFilter.Override(fake.tint);

            _flashVolume = PostProcessManager.instance.QuickVolume(
                volumeLayer, FlashPriority, grain, _flashLens, fringe, edge, _flashGrading);
            _flashVolume.weight = 0f;
        }

        // 연기의 재질은 Stage 프리팹 속 모닥불의 불꽃 재질을 빌린다. (Campfire와 같은 이유)
        private ParticleSystem CreateSmoke()
        {
            GameManager manager = GameManager.Instance;
            Material material = Campfire.FindFlameMaterial(manager != null ? manager.StagePrefab : null);
            if (material == null)
                return null;

            ParticleSystem smoke = Campfire.CreateSmokeColumn(null, Vector3.zero, material);
            smoke.gameObject.name = "Title Smoke";
            return smoke;
        }

        // mask에 켜져 있는 레이어 중 번호가 가장 작은 것. 하나도 없으면 TransparentFX(1).
        // (FakeDimensionEffect가 같은 기준으로 Volume 레이어를 정한다)
        private static int LowestLayerIn(LayerMask mask)
        {
            for (int i = 0; i < 32; i++)
                if ((mask.value & (1 << i)) != 0)
                    return i;
            return 1;
        }
    }
}
