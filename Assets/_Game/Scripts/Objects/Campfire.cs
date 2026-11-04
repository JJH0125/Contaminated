using System.Collections.Generic;
using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 모닥불. 라운드가 시작될 때는 모두 꺼져 있고, 정해 둔 수만큼 켜야 출구가 열린다.
    ///
    ///   꺼져 있을 때  Real에서는 오염 덤불에 덮여 있어 켤 수 없다. 주황 불씨가 남아 있고, 연기가 피어올라 자리를 알려 준다.
    ///                 Fake에서는 덤불이 걷혀 있어 켤 수 있다 (E를 누르고 있기, CampfireLighter)
    ///                 Fake에서만 보이는 뿌리가 이 불에서 출구까지, 그리고 이 불이 여는 관문까지 이어져 있다
    ///   켜질 때      덮고 있던 덤불과, 이 불과 이어진 관문의 덤불이 타서 열리고, 뿌리도 타서 사라진다.
    ///                불붙는 소리가 두 차원에 퍼져 적이 몰려온다
    ///   켜진 뒤      꺼지지 않는 불빛으로 길잡이가 되는 동시에, 그 옆에 선 플레이어를 적에게 드러낸다 (FireSource)
    ///
    /// 불빛과 "불빛에 드러난 자리인가" 판단은 같은 오브젝트의 FireSource가 맡는다. 꺼져 있는 동안은 FireSource를 꺼 둔다.
    ///
    /// 씬 구성:
    ///   같은 오브젝트에 FireSource를 붙이고, 모닥불 모델(FirePit)과 불꽃(LargeFlames)은 자식으로 둔다.
    ///   덤불 덮개, 관문, 뿌리는 StageLayout이 만들어 Seal / LinkGate / ConnectRoot로 넘겨준다.
    /// </summary>
    public class Campfire : MonoBehaviour
    {
        // 불꽃 오브젝트의 이름. 켜져 있을 때만 보인다.
        private const string FlamesName = "LargeFlames";

        // 지금 있는 모든 모닥불
        private static readonly List<Campfire> _all = new();
        public static IReadOnlyList<Campfire> All => _all;

        public bool IsLit { get; private set; }

        /// 한 번이라도 켜진 적이 있는가. 다시 켤 때는 불씨가 남아 있어 더 빨리 붙는다.
        public bool HasBeenLit { get; private set; }

        /// 재의 적이 끈 정도 (0~1). 끊겨도 남아서, 같은 적이든 다른 적이든 이어서 끈다.
        public float ExtinguishProgress { get; private set; }

        /// 지금 누군가 끄고 있는가. (화면 알림이 읽는다)
        public bool IsBeingExtinguished => IsLit && Time.time - _lastExtinguishTime < 0.5f;

        private float _lastExtinguishTime = float.NegativeInfinity;
        private ParticleSystem _ashSwirl;
        private Vector3 _flameScale = Vector3.one;

        private FireSource _fire;
        private GameObject _flames;
        private ParticleSystem _smoke;
        // 꺼진 불에 남은 불씨. 켜지면 불꽃이 대신한다. (FireSource와 달리 플레이어를 드러내지 않는다)
        private Light _ember;
        private ParticleSystem _emberSparks;
        // 이 불을 덮은 덤불 덮개 (Real에서만 막음)
        private DimensionObstacle _cover;
        // 이 불을 켜면 열리는 관문 (Real에서만 막힌 덤불)
        private readonly List<DimensionObstacle> _gates = new();
        // 이 불에서 출구와 관문으로 뻗은 뿌리
        private readonly List<RootLine> _roots = new();

        /// <summary>StageLayout이 이 불을 덮은 덤불을 넘겨준다. 불이 붙으면 덤불이 타서 사라진다.</summary>
        public void Seal(DimensionObstacle cover) => _cover = cover;

        /// <summary>StageLayout이 이 불을 켜면 열릴 관문을 넘겨준다.</summary>
        public void LinkGate(DimensionObstacle gate)
        {
            if (gate != null && !_gates.Contains(gate))
                _gates.Add(gate);
        }

        /// <summary>StageLayout이 이 불에서 뻗은 뿌리(출구까지, 관문까지)를 넘겨준다. 불이 붙으면 뿌리가 타서 사라진다.</summary>
        public void ConnectRoot(RootLine root)
        {
            if (root != null)
                _roots.Add(root);
        }

        private void Awake()
        {
            _fire = GetComponent<FireSource>();
            Transform flames = transform.Find(FlamesName);
            _flames = flames != null ? flames.gameObject : null;
            _smoke = CreateSmoke();
            if (_flames != null)
                _flameScale = _flames.transform.localScale;
            _ashSwirl = CreateAshSwirl();
            CreateEmber();
        }

        private void OnEnable() => _all.Add(this);
        private void OnDisable() => _all.Remove(this);

        private void Start()
        {
            SetLit(false);
            if (GameManager.Instance != null)
                GameManager.Instance.AddCampfire(this);
        }

        /// <summary>불을 붙인다. 덮개와 뿌리가 타고, 소리가 나고, 출구가 열릴 수 있는지 다시 센다.</summary>
        public void Light()
        {
            if (IsLit)
                return;

            SetLit(true);
            HasBeenLit = true;
            ExtinguishProgress = 0f;
            float burnTime = GameTuning.Instance.map.burnTime;
            if (_cover != null)
            {
                _cover.Open();
                BurnAway.Play(_cover.gameObject, burnTime);
            }
            foreach (DimensionObstacle gate in _gates)
            {
                if (gate == null)
                    continue;
                gate.Open();
                BurnAway.Play(gate.gameObject, burnTime);
            }
            foreach (RootLine root in _roots)
                if (root != null)
                    root.Burn();

            // 불붙는 소리는 차원을 넘어 퍼진다. 덮개가 걷힌 불가에 두 차원의 적이 모여든다.
            SoundEmitter.Emit(transform.position, SoundList.FireLit, (1 << 8) | (1 << 9), Dimension.None, gameObject);

            if (GameManager.Instance != null)
                GameManager.Instance.OnCampfireLit();
        }

        /// <summary>
        /// 불을 seconds초만큼 끈다. 다 끄면 true. (재의 적의 "모닥불 끄기" 목표 부품이 매 프레임 부른다)
        /// 꺼져 가는 동안 불꽃과 불빛이 줄어들고 검은 재가 맴돌아서, Real에서도 멀리서 보인다.
        /// </summary>
        public bool AddExtinguish(float seconds, float totalTime)
        {
            if (!IsLit)
                return true;

            _lastExtinguishTime = Time.time;
            ExtinguishProgress = Mathf.Clamp01(ExtinguishProgress + seconds / Mathf.Max(totalTime, 0.01f));
            if (ExtinguishProgress >= 1f)
            {
                Extinguish();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 켜진 불을 바로 끈다.
        /// 한 번 타서 사라진 덤불과 뿌리는 되살아나지 않으므로, 끈 불은 Real에서도 다시 켤 수 있다.
        /// </summary>
        public void Extinguish()
        {
            if (!IsLit)
                return;

            SetLit(false);
            ExtinguishProgress = 0f;
            FireAlertLabel.NotifyExtinguished();
            if (GameManager.Instance != null)
                GameManager.Instance.CheckExit();
        }

        private void Update()
        {
            // 꺼져 가는 만큼 불꽃과 불빛이 작아진다. 다시 켜면 되살아난다.
            float strength = IsLit ? 1f - ExtinguishProgress * 0.8f : 1f;
            if (_flames != null)
                _flames.transform.localScale = _flameScale * strength;
            if (_fire != null)
                _fire.Strength = strength;

            if (_ashSwirl != null)
            {
                bool swirling = IsBeingExtinguished;
                if (swirling && !_ashSwirl.isEmitting)
                {
                    _ashSwirl.Play();
                    // 화면 위쪽에 "모닥불이 꺼지고 있다"를 띄울 글자가 없으면 만든다.
                    FireAlertLabel.EnsureExists();
                }
                else if (!swirling && _ashSwirl.isEmitting)
                    _ashSwirl.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        // 꺼져 가는 불 둘레를 맴돌며 높이 오르는 검은 재. 두 차원 모두에서 보인다.
        private ParticleSystem CreateAshSwirl()
        {
            var swirlObject = new GameObject("Ash Swirl");
            swirlObject.transform.SetParent(transform, false);
            swirlObject.transform.localPosition = Vector3.up * 0.5f;
            swirlObject.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

            var swirl = swirlObject.AddComponent<ParticleSystem>();
            swirl.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = swirl.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            main.startColor = new Color(0.03f, 0.03f, 0.035f, 0.85f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;

            ParticleSystem.EmissionModule emission = swirl.emission;
            emission.rateOverTime = 20f;

            ParticleSystem.ShapeModule shape = swirl.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 3f;

            // 불 둘레를 돌면서 오른다.
            ParticleSystem.VelocityOverLifetimeModule velocity = swirl.velocityOverLifetime;
            velocity.enabled = true;
            velocity.orbitalX = 0f;
            velocity.orbitalY = 0f;
            velocity.orbitalZ = 1.2f;

            ParticleSystem.ColorOverLifetimeModule color = swirl.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;

            var swirlRenderer = swirlObject.GetComponent<ParticleSystemRenderer>();
            swirlRenderer.sharedMaterial = AshEnemyBody.AshMaterial();
            swirlRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return swirl;
        }

        private void SetLit(bool lit)
        {
            IsLit = lit;
            if (_fire != null)
                _fire.enabled = lit;
            if (_flames != null)
                _flames.SetActive(lit);
            if (_ember != null)
                _ember.enabled = !lit;
            if (_emberSparks != null)
            {
                if (lit)
                    _emberSparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                else
                    _emberSparks.Play();
            }
            if (_smoke != null)
            {
                if (lit)
                    _smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                else
                    _smoke.Play();
            }
        }

        // 꺼진 불에 남은 주황 불씨: 약한 빛과, 낮게 떠오르는 작은 불티. 재질은 연기처럼 불꽃의 것을 빌려 쓴다.
        private void CreateEmber()
        {
            MapTuning tuning = GameTuning.Instance.map;
            var lightObject = new GameObject("Ember Light");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.position = transform.position + Vector3.up * 0.6f;
            _ember = lightObject.AddComponent<Light>();
            _ember.type = LightType.Point;
            _ember.color = tuning.emberColor;
            _ember.intensity = tuning.emberIntensity;
            _ember.range = tuning.emberRange;

            ParticleSystemRenderer flameRenderer = _flames != null ? _flames.GetComponentInChildren<ParticleSystemRenderer>(true) : null;
            if (flameRenderer == null || flameRenderer.sharedMaterial == null)
                return;

            var sparkObject = new GameObject("Ember Sparks");
            sparkObject.transform.SetParent(transform, false);
            sparkObject.transform.position = transform.position + Vector3.up * 0.3f;
            sparkObject.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

            _emberSparks = sparkObject.AddComponent<ParticleSystem>();
            _emberSparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = _emberSparks.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
            main.startColor = tuning.emberColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 30;
            ParticleSystem.EmissionModule emission = _emberSparks.emission;
            emission.rateOverTime = 8f;
            ParticleSystem.ShapeModule shape = _emberSparks.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15f;
            shape.radius = 0.7f;

            var sparkRenderer = sparkObject.GetComponent<ParticleSystemRenderer>();
            sparkRenderer.sharedMaterial = flameRenderer.sharedMaterial;
            sparkRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // 꺼진 불에서 피어오르는 연기. 덮은 덤불 너머 멀리서도 보이도록 높이 오른다.
        // 재질은 불꽃의 것을 빌려 쓴다. (따로 연결하지 않아도 되고, 빌드에서 셰이더가 빠지지 않는다)
        private ParticleSystem CreateSmoke()
        {
            ParticleSystemRenderer flameRenderer = _flames != null ? _flames.GetComponentInChildren<ParticleSystemRenderer>(true) : null;
            if (flameRenderer == null || flameRenderer.sharedMaterial == null)
                return null;

            var smokeObject = new GameObject("Smoke");
            smokeObject.transform.SetParent(transform, false);
            smokeObject.transform.localPosition = Vector3.up * 1f;
            smokeObject.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

            var smoke = smokeObject.AddComponent<ParticleSystem>();
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = smoke.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.55f, 0.55f, 0.55f, 0.18f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;

            ParticleSystem.EmissionModule emission = smoke.emission;
            emission.rateOverTime = 4f;

            ParticleSystem.ShapeModule shape = smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.6f;

            // 위로 오를수록 퍼지고 옅어진다.
            ParticleSystem.SizeOverLifetimeModule size = smoke.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 2.5f)));
            ParticleSystem.ColorOverLifetimeModule color = smoke.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;

            var smokeRenderer = smokeObject.GetComponent<ParticleSystemRenderer>();
            smokeRenderer.sharedMaterial = flameRenderer.sharedMaterial;
            smokeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return smoke;
        }
    }
}
