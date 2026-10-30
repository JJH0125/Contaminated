using System.Collections.Generic;
using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 모닥불. 라운드가 시작될 때는 모두 꺼져 있고, 정해 둔 수만큼 켜야 출구가 열린다.
    ///
    ///   꺼져 있을 때  Real에서만 막히는 덤불 고리 안에 있어 Real에서는 다가갈 수 없다. 연기만 피어올라 자리를 알려 준다.
    ///                 Fake에서는 고리가 없으므로 들어가서 켤 수 있다 (E를 누르고 있기, CampfireLighter)
    ///                 Fake에서만 보이는 뿌리가 이 불에서 출구까지, 그리고 이 불이 여는 관문까지 이어져 있다
    ///   켜질 때      덤불 고리와, 이 불과 이어진 관문의 덤불이 타서 열리고, 뿌리도 타서 사라진다.
    ///                불붙는 소리가 두 차원에 퍼져 적이 몰려온다
    ///   켜진 뒤      꺼지지 않는 불빛으로 길잡이가 되는 동시에, 그 옆에 선 플레이어를 적에게 드러낸다 (FireSource)
    ///
    /// 불빛과 "불빛에 드러난 자리인가" 판단은 같은 오브젝트의 FireSource가 맡는다. 꺼져 있는 동안은 FireSource를 꺼 둔다.
    ///
    /// 씬 구성:
    ///   같은 오브젝트에 FireSource를 붙이고, 모닥불 모델(FirePit)과 불꽃(LargeFlames)은 자식으로 둔다.
    ///   덤불 고리, 관문, 뿌리는 StageLayout이 만들어 Seal / LinkGate / ConnectRoot로 넘겨준다.
    /// </summary>
    public class Campfire : MonoBehaviour
    {
        // 불꽃 오브젝트의 이름. 켜져 있을 때만 보인다.
        private const string FlamesName = "LargeFlames";

        // 지금 있는 모든 모닥불
        private static readonly List<Campfire> _all = new();
        public static IReadOnlyList<Campfire> All => _all;

        public bool IsLit { get; private set; }

        private FireSource _fire;
        private GameObject _flames;
        private ParticleSystem _smoke;
        private DimensionObstacle _ring;
        // 이 불을 켜면 열리는 관문 (Real에서만 막힌 덤불)
        private readonly List<DimensionObstacle> _gates = new();
        // 이 불에서 출구와 관문으로 뻗은 뿌리
        private readonly List<RootLine> _roots = new();

        /// <summary>StageLayout이 이 불을 둘러싼 덤불 고리를 넘겨준다. 불이 붙으면 고리가 타서 열린다.</summary>
        public void Seal(DimensionObstacle ring) => _ring = ring;

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
        }

        private void OnEnable() => _all.Add(this);
        private void OnDisable() => _all.Remove(this);

        private void Start()
        {
            SetLit(false);
            if (GameManager.Instance != null)
                GameManager.Instance.AddCampfire(this);
        }

        /// <summary>불을 붙인다. 고리와 뿌리가 타고, 소리가 나고, 출구가 열릴 수 있는지 다시 센다.</summary>
        public void Light()
        {
            if (IsLit)
                return;

            SetLit(true);
            float burnTime = GameTuning.Instance.map.burnTime;
            if (_ring != null)
            {
                _ring.Open();
                BurnAway.Play(_ring.gameObject, burnTime);
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

            // 불붙는 소리는 차원을 넘어 퍼진다. 고리가 열린 불가에 두 차원의 적이 모여든다.
            SoundEmitter.Emit(transform.position, SoundList.FireLit, (1 << 8) | (1 << 9), Dimension.None, gameObject);

            if (GameManager.Instance != null)
                GameManager.Instance.OnCampfireLit();
        }

        private void SetLit(bool lit)
        {
            IsLit = lit;
            if (_fire != null)
                _fire.enabled = lit;
            if (_flames != null)
                _flames.SetActive(lit);
            if (_smoke != null)
            {
                if (lit)
                    _smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                else
                    _smoke.Play();
            }
        }

        // 꺼진 불에서 피어오르는 연기. 덤불 고리 너머 멀리서도 보이도록 높이 오른다.
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
