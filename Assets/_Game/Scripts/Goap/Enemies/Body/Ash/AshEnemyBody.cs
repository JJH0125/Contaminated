using System.Collections.Generic;
using UnityEngine;

namespace Squad
{
    /// <summary>
    /// Fake 적의 몸. 정해진 모양이 없는 재의 입자 떼가, 다가올수록 모여 사람 모양을 이룬다.
    ///
    /// 모든 모습과 소리는 "응집도" 값 하나(0: 흩어짐, 1: 다 모임)를 따라간다.
    ///   0 (멀리)    입자가 넓게 흩어지고, 중심 쪽으로 천천히 흘러든다. 재가 바스러지는 소리만 띄엄띄엄 난다
    ///   0.5         모이기 시작한다. 낮은 바람 소리가 깔린다. 결정이 머리 쪽으로 몰린다
    ///   1 (눈앞)    걷는 사람 모양이 또렷하다. 바람이 울부짖는다. 결정이 머리 앞에 모여 얼굴 자리가 된다
    ///
    /// 응집도는 플레이어와의 거리로 정하되, 경계 상태가 그 상한을 정한다.
    ///   Calm        가까워도 반쯤만 모인다 (조용히 지나칠 수 있다는 신호)
    ///   Suspicious  조금 더 모인다
    ///   Alerted     끝까지 모이고, 모이는 속도도 빠르다 (모여드는 중 = 나를 쫓고 있다)
    /// 다 모인 채 쫓다가 가까워지면 재가 몸보다 먼저 플레이어 쪽으로 쏟아진다.
    ///
    /// 발각되는 순간(OnSpotted): 몸을 플레이어 쪽으로 홱 돌리고, 주변의 재가 적 쪽으로 확 빨려 들며, 결정이 번쩍인다.
    ///   숨을 들이켜는 바람과 유리에 금 가는 소리가 1초 안쪽으로 난다. 이 소리는 다른 재의 적을 부르지 않는다
    /// 잡을 때의 연출은 AshJumpscare가 이 몸을 직접 움직인다(BeginScripted 이후).
    ///
    /// 재를 끌어당기는 힘은 Particle System Force Field 하나로 낸다. 발각 때는 적의 몸 쪽으로, 잡을 때는 플레이어 둘레로 옮겨 쓴다.
    ///
    /// 사람 모양은 원래의 적 모델에서 나온다. 모델은 그리지 않을 뿐 그대로 걷고 있어서, 그 몸 표면에서 입자를 뿜으면
    /// 걷는 실루엣이 입자로 드러난다. (모델의 Read/Write가 꺼져 있으면 상자 모양에서 뿜는다)
    /// 판정(잡기, 길찾기, 시야)은 지금처럼 적의 중심점 하나로 한다. 흩어진 입자는 연출일 뿐이다.
    ///
    /// 수치는 GameTuning의 ashEnemy 항목에서 읽는다.
    ///
    /// 씬 구성:
    ///   붙일 필요가 없다. StageLayout이 Fake 적을 놓을 때 붙인다.
    /// </summary>
    public class AshEnemyBody : MonoBehaviour
    {
        private const string CrystalShaderName = "Hidden/Custom/AshCrystal";
        // 중심의 옆모습: (높이 비율, 폭 비율). 아래는 가늘고, 어깨에서 넓어졌다가 목에서 좁아져 머리가 된다.
        private static readonly Vector2[] Profile =
        {
            new Vector2(0f, 0.04f), new Vector2(0.12f, 0.16f), new Vector2(0.35f, 0.3f), new Vector2(0.55f, 0.36f),
            new Vector2(0.7f, 0.5f), new Vector2(0.76f, 0.42f), new Vector2(0.82f, 0.15f), new Vector2(0.87f, 0.23f),
            new Vector2(0.95f, 0.2f), new Vector2(1f, 0.02f),
        };
        private const int Spokes = 16;
        // FullReveal(Real 적의 잡는 연출 등)로 드러날 때 한 번에 퍼지는 입자의 수
        private const int ScareBurst = 150;
        private static readonly int CrystalGlowId = Shader.PropertyToID("_AshCrystalGlow");
        // 잡을 때 카메라 앞에 맺히는 얼굴 자리: 두 눈(셋씩), 이마(둘), 길게 벌린 입(넷). 단위는 m, 크기 배율 1 기준
        private static readonly Vector3[] FaceLayout =
        {
            new Vector3(-0.35f, 0.2f), new Vector3(-0.22f, 0.26f), new Vector3(-0.28f, 0.1f),
            new Vector3(0.35f, 0.2f), new Vector3(0.22f, 0.26f), new Vector3(0.28f, 0.1f),
            new Vector3(-0.08f, 0.5f), new Vector3(0.08f, 0.45f),
            new Vector3(-0.26f, -0.3f), new Vector3(-0.09f, -0.4f), new Vector3(0.09f, -0.4f), new Vector3(0.26f, -0.3f),
        };

        private static Material _ashMaterial;
        private static Material _crystalMaterial;
        private static Mesh _crystalMesh;

        /// 지금의 응집도 (0: 흩어짐, 1: 다 모임). 디버그 표시 등에서 읽는다.
        public float Coherence { get; private set; }
        /// 잡을 때의 연출(AshJumpscare)이 몸을 직접 움직이는 중인가. 이때는 응집도와 소리를 스스로 정하지 않는다.
        public bool Scripted { get; private set; }

        private Transform _body;
        private Transform _core;
        private ParticleSystem _ash;
        private ChaserAlert _alert;
        private DimensionMember _member;
        private AshWindSound _wind;
        private AudioSource _windSource;
        private Vector3 _restPosition;
        private float _phase;
        private bool _wasRevealed;

        // 재를 끌어당기는 힘. 발각 때 잠깐(_pullUntil까지), 잡을 때는 연출이 끝날 때까지 켠다.
        private ParticleSystemForceField _field;
        private float _pullUntil;
        private float _lastSpotTime = float.NegativeInfinity;

        // 결정의 번쩍임 (0~1, 시간이 지나면 줄어든다)
        private float _flash;
        private float _appliedGlow = -1f;
        private MaterialPropertyBlock _glowBlock;
        private readonly List<Renderer> _crystalRenderers = new();

        // 잡을 때: 흩어지며 날아가는 결정의 속도와, 얼굴로 모이기 시작한 자리
        private Vector3[] _crystalVelocity;
        private Vector3[] _faceStart;

        // 결정 하나: 흩어졌을 때의 자리와 모였을 때의 자리
        private readonly List<(Transform crystal, Vector3 scattered, Vector3 gathered)> _crystals = new();

        private static AshEnemyTuning Tuning => GameTuning.Instance.ashEnemy;

        private void Awake()
        {
            AshEnemyTuning tuning = Tuning;
            _phase = Random.value * 10f;
            _alert = GetComponent<ChaserAlert>();
            _member = GetComponent<DimensionMember>();

            // 원래 모델은 그리지 않는다. 가장 큰 몸을 입자를 뿜는 자리로 쓴다.
            Material bodyMaterial = null;
            SkinnedMeshRenderer skin = null;
            foreach (SkinnedMeshRenderer part in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                part.forceRenderingOff = true;
                if (skin == null || (part.sharedMesh != null && skin.sharedMesh != null &&
                                     part.sharedMesh.vertexCount > skin.sharedMesh.vertexCount))
                    skin = part;
            }
            if (skin != null)
                bodyMaterial = skin.sharedMaterial;

            // 적의 크기 배율과 상관없이 미터 단위로 만들도록, 배율을 지운 자식을 둔다.
            _body = new GameObject("Ash Body").transform;
            _body.SetParent(transform, false);
            Vector3 lossy = transform.lossyScale;
            _body.localScale = new Vector3(1f / lossy.x, 1f / lossy.y, 1f / lossy.z);
            float footOffset = -GameTuning.Instance.fakeEnemy.heightAboveGround;
            _body.localPosition = Vector3.up * (footOffset / Mathf.Max(lossy.y, 0.001f));
            _restPosition = _body.localPosition;

            // 흩어져 있어도 "저쪽에 뭔가 있다"가 보이도록, 중심에 검은 덩어리를 둔다. 모일수록 커진다.
            var core = new GameObject("Core");
            core.transform.SetParent(_body, false);
            core.AddComponent<MeshFilter>().sharedMesh = BuildCore(tuning.bodyHeight, tuning.bodyWidth);
            core.AddComponent<MeshRenderer>().sharedMaterial = bodyMaterial;
            _core = core.transform;

            BuildCrystals(tuning);
            _ash = BuildBodyAsh(tuning, skin);
            BuildGroundAsh(tuning);
            _field = BuildPullField(_ash);
            _glowBlock = new MaterialPropertyBlock();

            var windObject = new GameObject("Ash Wind");
            windObject.transform.SetParent(_body, false);
            _windSource = windObject.AddComponent<AudioSource>();
            _wind = windObject.AddComponent<AshWindSound>();

            // 플레이어와 다른 차원에 있으면 함께 숨도록 이름표에 알려 준다.
            if (_member != null)
                _member.AddRenderers(_body.GetComponentsInChildren<Renderer>(true));

            Apply(0f, Vector3.forward);

            // 잡을 때의 연출을 맡는다. (잡는 행동이 Jumpscare 대신 이것을 부른다)
            if (GetComponent<AshJumpscare>() == null)
                gameObject.AddComponent<AshJumpscare>();
        }

        private void Update()
        {
            UpdateGlow();
            if (Scripted)
            {
                UpdateScriptedCrystals();
                return;
            }
            if (_field.enabled && Time.time >= _pullUntil)
                ReleasePull();

            AshEnemyTuning tuning = Tuning;
            Player player = DimensionController.Instance != null ? DimensionController.Instance.player : null;

            Vector3 toPlayer = player != null ? player.transform.position - transform.position : Vector3.forward * 1000f;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;

            // 거리로 정한 응집도에, 경계 상태가 상한을 둔다.
            AlertLevel level = _alert != null ? _alert.Level : AlertLevel.Calm;
            float cap = level switch
            {
                AlertLevel.Alerted => 1f,
                AlertLevel.Suspicious => tuning.suspiciousCoherence,
                _ => tuning.calmCoherence,
            };
            float byDistance = 1f - Mathf.InverseLerp(tuning.coherenceNear, tuning.coherenceFar, distance);
            float target = Mathf.Min(cap, byDistance);

            bool revealed = AshEnemyLight.FullReveal;
            if (revealed)
                target = 1f;

            float speed = target > Coherence
                ? tuning.gatherSpeed * (level == AlertLevel.Alerted ? 2f : 1f)
                : tuning.scatterSpeed;
            Coherence = Mathf.MoveTowards(Coherence, target, speed * Time.deltaTime);

            // 다 모인 채 쫓다가 가까워지면, 재가 몸보다 먼저 플레이어 쪽으로 쏟아진다.
            float lunge = level == AlertLevel.Alerted && Coherence > 0.8f
                ? 1f - Mathf.Clamp01(distance / Mathf.Max(tuning.lungeDistance, 0.01f))
                : 0f;
            Vector3 lungeDirection = distance > 0.01f ? toPlayer / distance : transform.forward;
            Apply(lunge, lungeDirection);

            // 잡히는 순간 한 번 확 퍼진다.
            if (revealed && !_wasRevealed)
                _ash.Emit(ScareBurst);
            _wasRevealed = revealed;

            UpdateSound(player, distance);

            // 떠 있는 것처럼 천천히 오르내린다.
            float time = Time.time + _phase;
            _body.localPosition = _restPosition + Vector3.up * (Mathf.Sin(time * 1.1f) * 0.25f);
        }

        /// <summary>
        /// 발각되는 순간: 몸을 플레이어 쪽으로 홱 돌리고, 주변의 재가 확 빨려 들며, 결정이 번쩍이고, 숨을 들이켜는 소리가 난다.
        /// 나무 사이를 지나며 보였다 안 보였다 해도 되풀이되지 않게, 한 번 난 뒤 잠깐은 다시 하지 않는다.
        /// </summary>
        public void OnSpotted(Vector3 playerPosition)
        {
            if (Scripted || Time.time - _lastSpotTime < GameTuning.Instance.fakeEnemy.spottedSoundCooldown)
                return;
            _lastSpotTime = Time.time;
            AshEnemyTuning tuning = Tuning;

            Vector3 toPlayer = playerPosition - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.01f)
            {
                Quaternion facing = Quaternion.LookRotation(toPlayer, Vector3.up);
                transform.rotation = facing;
                if (TryGetComponent(out Rigidbody body))
                    body.rotation = facing;
            }
            PlayAnimation(tuning.spotAnimation, false);

            Pull(transform.position + Vector3.up * (tuning.bodyHeight * 0.5f), tuning.spotPullStrength, 0f, 0f,
                 tuning.scatterRadius * 2f, 3f);
            _pullUntil = Time.time + tuning.spotPullTime;
            Coherence = Mathf.Min(1f, Coherence + 0.3f);
            Flash(1f);

            if (SfxPlayer.Instance != null)
                SfxPlayer.Instance.PlayUI(AshSounds.Spotted);
        }

        /// 결정을 번쩍이게 한다 (0~1). flashTime에 걸쳐 원래 밝기로 돌아온다.
        public void Flash(float amount) => _flash = Mathf.Max(_flash, amount);

        /// <summary>
        /// 재를 center 쪽으로 끌어당긴다. swirl을 주면 그 둘레를 돈다.
        /// inner 반지름 안에서는 힘이 없어서, 재가 한 점에 뭉치지 않고 그 안을 지나쳐 다시 끌려온다.
        /// </summary>
        public void Pull(Vector3 center, float strength, float swirl, float inner, float reach, float drag)
        {
            _field.transform.SetPositionAndRotation(center, Quaternion.identity);
            _field.startRange = inner;
            _field.endRange = reach;
            _field.gravity = strength;
            _field.rotationSpeed = swirl;
            _field.rotationAttraction = swirl > 0f ? 0.7f : 0f;
            _field.drag = drag;
            _field.enabled = true;
        }

        public void ReleasePull() => _field.enabled = false;

        /// 숨겨 둔 몸에 Animator 상태를 재생한다. 이름이 비었거나 그런 상태가 없으면 아무 일도 하지 않는다.
        /// once면 걷기 애니메이션으로 돌아가지 않는다(잡을 때). 아니면 그 상태의 전환을 따라 돌아간다(발각 때).
        public void PlayAnimation(string stateName, bool once)
        {
            if (string.IsNullOrEmpty(stateName))
                return;
            Animator animator = GetComponentInChildren<Animator>();
            if (animator == null || !(animator.HasState(0, Animator.StringToHash(stateName)) ||
                                      animator.HasState(0, Animator.StringToHash("Base Layer." + stateName))))
                return;

            ChaserAnimator chaserAnimator = GetComponent<ChaserAnimator>();
            if (once && chaserAnimator != null)
                chaserAnimator.PlayOnce(stateName);
            else
                animator.CrossFadeInFixedTime(stateName, 0.05f, 0);
        }

        // ── 잡을 때의 연출(AshJumpscare)이 부르는 것들 ──

        /// 이제부터 연출이 몸을 직접 움직인다. 떠 있는 움직임과 바람 소리, 응집도 계산이 멈춘다.
        public void BeginScripted()
        {
            Scripted = true;
            ReleasePull();
            // 모든 소리를 멈춘(AudioListener.pause) 뒤에도 연출의 바람 소리는 들려야 한다.
            _windSource.ignoreListenerPause = true;
            _windSource.panStereo = 0f;
            _wind.Set(0f, 300f, 0f, 0f);
        }

        /// 한 번에 터지듯 흩어진다. 중심 덩어리가 사라지고, 재와 결정이 사방으로 날아간다.
        public void Scatter()
        {
            AshEnemyTuning tuning = Tuning;
            Coherence = 0f;
            Apply(0f, transform.forward);
            _core.gameObject.SetActive(false);

            Vector3 center = transform.position + Vector3.up * (tuning.bodyHeight * 0.5f);
            var emit = new ParticleSystem.EmitParams();
            for (int i = 0; i < tuning.burstCount; i++)
            {
                Vector3 offset = Vector3.Scale(Random.insideUnitSphere,
                                               new Vector3(tuning.bodyWidth * 0.5f, tuning.bodyHeight * 0.4f, tuning.bodyWidth * 0.5f));
                emit.position = center + offset;
                emit.velocity = (offset.sqrMagnitude > 0.0001f ? offset.normalized : Random.onUnitSphere)
                                * (tuning.burstSpeed * Random.Range(0.6f, 1.2f));
                emit.startLifetime = Random.Range(1.2f, 2f);
                _ash.Emit(emit, 1);
            }

            _crystalVelocity = new Vector3[_crystals.Count];
            for (int i = 0; i < _crystals.Count; i++)
            {
                Vector3 away = _crystals[i].crystal.position - center;
                away.y = Mathf.Max(away.y, 0f);
                _crystalVelocity[i] = (away.sqrMagnitude > 0.0001f ? away.normalized : Random.onUnitSphere) * (tuning.burstSpeed * 0.8f);
            }
        }

        /// 재를 center 둘레(반지름 radius)에 count개 더 만든다. 처음부터 둘레를 따라 돌도록 옆으로 밀어 준다.
        public void EmitAround(Vector3 center, float radius, int count, float spinSpeed)
        {
            var emit = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                Vector3 direction = ZoneMap.Direction(Random.value * 360f);
                emit.position = center + direction * (radius * Random.Range(0.7f, 1.3f)) + Vector3.up * Random.Range(-2.5f, 2.5f);
                emit.velocity = Vector3.Cross(Vector3.up, direction) * spinSpeed - direction * (spinSpeed * 0.3f);
                emit.startLifetime = Random.Range(0.8f, 1.4f);
                _ash.Emit(emit, 1);
            }
        }

        /// 결정이 얼굴로 모이기 시작한다. 지금 자리에서 출발한다.
        public void BeginFace()
        {
            _faceStart = new Vector3[_crystals.Count];
            for (int i = 0; i < _crystals.Count; i++)
                _faceStart[i] = _crystals[i].crystal.position;
            _crystalVelocity = null;
        }

        /// 결정을 center(카메라 앞)에 얼굴 모양으로 놓는다. t: 0이면 출발한 자리, 1이면 다 모인 얼굴
        public void SetFace(Vector3 center, Quaternion facing, float size, float t)
        {
            if (_faceStart == null)
                return;
            for (int i = 0; i < _crystals.Count; i++)
            {
                Vector3 target = center + facing * (FaceLayout[i % FaceLayout.Length] * size);
                _crystals[i].crystal.position = Vector3.Lerp(_faceStart[i], target, t);
            }
        }

        /// 바람 소리를 직접 정한다. (연출 중에는 거리로 정하지 않는다)
        public void SetWind(float level, float pitch, float howl, float rustle) => _wind.Set(level, pitch, howl, rustle);

        // 흩어지며 날아가는 결정: 점점 느려진다.
        private void UpdateScriptedCrystals()
        {
            if (_crystalVelocity == null)
                return;
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < _crystals.Count; i++)
            {
                _crystals[i].crystal.position += _crystalVelocity[i] * dt;
                _crystalVelocity[i] *= Mathf.Exp(-3f * dt);
            }
        }

        // 결정의 밝기: 평소 밝기와 번쩍임 중 큰 쪽. 적마다 따로 번쩍이도록 결정마다 값을 덮어쓴다.
        private void UpdateGlow()
        {
            _flash = Mathf.MoveTowards(_flash, 0f, Time.unscaledDeltaTime / Mathf.Max(Tuning.flashTime, 0.01f));
            float glow = Mathf.Max(Tuning.crystalGlow, _flash);
            if (Mathf.Approximately(glow, _appliedGlow))
                return;

            _appliedGlow = glow;
            _glowBlock.SetFloat(CrystalGlowId, glow);
            foreach (Renderer crystalRenderer in _crystalRenderers)
                crystalRenderer.SetPropertyBlock(_glowBlock);
        }

        // 응집도에 맞춰 입자, 중심, 결정을 바꾼다.
        private void Apply(float lunge, Vector3 lungeDirection)
        {
            AshEnemyTuning tuning = Tuning;
            float c = Coherence;

            ParticleSystem.EmissionModule emission = _ash.emission;
            emission.rateOverTime = Mathf.Lerp(tuning.ashRateScattered, tuning.ashRateGathered, c);

            // 흩어질수록 몸에서 멀리 떨어진 자리에서 나고, 크게 일렁이며, 중심으로 천천히 흘러든다.
            ParticleSystem.ShapeModule shape = _ash.shape;
            shape.randomPositionAmount = Mathf.Lerp(tuning.scatterRadius, 0.15f, c);
            ParticleSystem.NoiseModule noise = _ash.noise;
            noise.strength = Mathf.Lerp(3f, 0.35f, c);

            ParticleSystem.VelocityOverLifetimeModule velocity = _ash.velocityOverLifetime;
            velocity.radial = Mathf.Lerp(-1.2f, 0f, c);
            Vector3 pour = lungeDirection * (tuning.lungeSpeed * lunge);
            velocity.x = pour.x;
            velocity.y = Mathf.Lerp(0.3f, 0.6f, c);
            velocity.z = pour.z;

            ParticleSystem.MainModule main = _ash.main;
            Color color = tuning.ashColor;
            color.a *= Mathf.Lerp(0.45f, 1f, c);
            main.startColor = color;

            // 중심은 흩어져 있을 때 가늘고 낮다가, 모일수록 사람 크기가 된다.
            float width = Mathf.Lerp(0.25f, 1f, c);
            _core.localScale = new Vector3(width, Mathf.Lerp(0.55f, 1f, c), width);

            // 결정은 흩어졌던 자리에서 얼굴 자리로 모인다.
            float pull = Mathf.SmoothStep(0f, 1f, c);
            float spin = (Time.time + _phase) * 20f;
            foreach ((Transform crystal, Vector3 scattered, Vector3 gathered) in _crystals)
            {
                Vector3 around = Quaternion.Euler(0f, spin, 0f) * scattered;
                crystal.localPosition = Vector3.Lerp(around, gathered, pull);
            }
        }

        // 바람 소리: 모일수록 크고 높고 날카롭다. 거리에 따라 작아지고, 화면 기준 좌우로 들린다.
        private void UpdateSound(Player player, float distance)
        {
            AshEnemyTuning tuning = Tuning;
            bool sameDimension = DimensionController.Instance != null && _member != null &&
                                 DimensionController.Instance.CompareDimension(_member);
            bool audible = player != null && sameDimension && Time.timeScale > 0f;

            float attenuation = 1f - Mathf.InverseLerp(tuning.windMinDistance, tuning.windMaxDistance, distance);
            float sfx = SfxPlayer.Instance != null ? SfxPlayer.Instance.Volume : 0.5f;
            float c = Coherence;
            float level = audible ? tuning.windVolume * sfx * attenuation * attenuation * Mathf.Lerp(0.15f, 1f, c * c) : 0f;
            _wind.Set(level, Mathf.Lerp(220f, 900f, c), c, (1f - c) * 0.8f);

            // 화면에서 적이 플레이어의 왼쪽이면 왼쪽에서 들린다.
            Camera cam = Camera.main;
            if (cam != null && player != null)
            {
                Vector3 offset = transform.position - player.transform.position;
                _windSource.panStereo = Mathf.Clamp(Vector3.Dot(offset, cam.transform.right) / Mathf.Max(tuning.windMaxDistance * 0.5f, 1f), -0.8f, 0.8f);
            }
        }

        // 옆모습(Profile)을 돌려 깎은 중심 덩어리. 둘레를 들쭉날쭉하게 해서 매끈한 원기둥으로 보이지 않게 한다.
        private static Mesh BuildCore(float height, float width)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            float seed = Random.value * 100f;
            // 입자가 몸을 이루므로 중심은 가늘게 둔다.
            width *= 0.6f;

            for (int ring = 0; ring < Profile.Length; ring++)
            {
                for (int spoke = 0; spoke <= Spokes; spoke++)
                {
                    float angle = spoke % Spokes * 360f / Spokes;
                    float rough = 0.8f + 0.4f * Mathf.PerlinNoise(seed + spoke % Spokes * 0.7f, ring * 0.6f);
                    Vector3 around = ZoneMap.Direction(angle) * (Profile[ring].y * width * rough);
                    around.z *= 0.7f;
                    vertices.Add(around + Vector3.up * (Profile[ring].x * height));
                    uvs.Add(new Vector2(spoke / (float)Spokes, Profile[ring].x * 3f));
                }
            }

            int stride = Spokes + 1;
            for (int ring = 0; ring < Profile.Length - 1; ring++)
            {
                for (int spoke = 0; spoke < Spokes; spoke++)
                {
                    int a = ring * stride + spoke;
                    int b = a + stride;
                    triangles.AddRange(new[] { a, b, a + 1 });
                    triangles.AddRange(new[] { a + 1, b, b + 1 });
                }
            }

            var mesh = new Mesh { name = "Ash Core" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // 결정 12개: 흩어졌을 때는 몸 둘레를 돌고, 모이면 머리 앞쪽의 얼굴 자리에 모인다.
        private void BuildCrystals(AshEnemyTuning tuning)
        {
            // 얼굴 자리의 높이: 잡힐 때 카메라가 바라보는 높이에 맞춘다. (몸의 기준점은 발밑이다)
            float face = GameTuning.Instance.enemyBody.faceHeight + GameTuning.Instance.fakeEnemy.heightAboveGround;
            float width = tuning.bodyWidth;

            for (int i = 0; i < 12; i++)
            {
                Vector3 scattered = ZoneMap.Direction(i * 30f + Random.Range(-10f, 10f)) * (width * Random.Range(0.8f, 1.4f))
                                    + Vector3.up * (tuning.bodyHeight * Random.Range(0.15f, 0.85f));
                Vector3 gathered = new Vector3(Random.Range(-0.12f, 0.12f) * width,
                                               face + Random.Range(-0.6f, 0.6f),
                                               width * Random.Range(0.12f, 0.2f));
                Transform crystal = AddCrystal(_body, scattered, Random.Range(0.25f, 0.5f));
                _crystals.Add((crystal, scattered, gathered));
                _crystalRenderers.Add(crystal.GetComponent<Renderer>());
            }
        }

        private static Transform AddCrystal(Transform parent, Vector3 localPosition, float size)
        {
            if (_crystalMaterial == null)
                _crystalMaterial = new Material(Shader.Find(CrystalShaderName));
            if (_crystalMesh == null)
                _crystalMesh = BuildCrystalMesh();

            var crystal = new GameObject("Crystal");
            crystal.transform.SetParent(parent, false);
            crystal.transform.localPosition = localPosition;
            crystal.transform.localRotation = Random.rotation;
            crystal.transform.localScale = new Vector3(size, size * 1.8f, size);
            crystal.AddComponent<MeshFilter>().sharedMesh = _crystalMesh;
            var crystalRenderer = crystal.AddComponent<MeshRenderer>();
            crystalRenderer.sharedMaterial = _crystalMaterial;
            crystalRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return crystal.transform;
        }

        // 위아래로 뾰족한 팔면체
        private static Mesh BuildCrystalMesh()
        {
            var mesh = new Mesh { name = "Ash Crystal" };
            mesh.vertices = new[]
            {
                Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back,
            };
            mesh.triangles = new[]
            {
                0, 4, 3,  0, 3, 5,  0, 5, 2,  0, 2, 4,
                1, 3, 4,  1, 5, 3,  1, 2, 5,  1, 4, 2,
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        // 몸을 이루는 재. 원래 모델의 걷는 몸 표면에서 뿜어서 사람 실루엣을 만든다.
        private ParticleSystem BuildBodyAsh(AshEnemyTuning tuning, SkinnedMeshRenderer skin)
        {
            ParticleSystem ash = NewParticles("Body Ash", tuning);
            ParticleSystem.MainModule main = ash.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            // 평소에는 수백 개지만, 잡을 때 한 번에 터지고 플레이어를 감싸는 재까지 담을 수 있게 넉넉히 둔다.
            main.maxParticles = 1200;

            ParticleSystem.ShapeModule shape = ash.shape;
            if (skin != null && skin.sharedMesh != null && skin.sharedMesh.isReadable)
            {
                shape.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
                shape.skinnedMeshRenderer = skin;
                shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;
            }
            else
            {
                // 모델을 읽을 수 없으면 몸 크기의 상자에서 뿜는다.
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(tuning.bodyWidth * 0.55f, tuning.bodyWidth * 0.4f, tuning.bodyHeight * 0.85f);
                ash.transform.localPosition = Vector3.up * (tuning.bodyHeight * 0.5f);
                ash.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            }

            ParticleSystem.NoiseModule noise = ash.noise;
            noise.enabled = true;
            noise.frequency = 0.5f;
            noise.scrollSpeed = 0.4f;

            ParticleSystem.VelocityOverLifetimeModule velocity = ash.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            // 모든 축을 같은 방식(상수)으로 맞춰 둔다. (Unity가 축마다 방식이 다르면 경고를 낸다)
            velocity.x = 0f;
            velocity.y = 0f;
            velocity.z = 0f;
            velocity.radial = 0f;
            // 흩어진 재가 흘러드는 중심은 발밑이 아니라 몸 한가운데다.
            velocity.orbitalOffsetY = tuning.bodyHeight * 0.5f;

            FadeInOut(ash, 0.2f);
            return ash;
        }

        // 몸의 재만 끌어당기는 힘. 평소에는 꺼 둔다. (다른 적의 재는 끌어당기지 않도록 목록으로 연결한다)
        private ParticleSystemForceField BuildPullField(ParticleSystem ash)
        {
            var fieldObject = new GameObject("Ash Pull");
            fieldObject.transform.SetParent(_body, false);
            var field = fieldObject.AddComponent<ParticleSystemForceField>();
            field.shape = ParticleSystemForceFieldShape.Sphere;
            field.directionX = 0f;
            field.directionY = 0f;
            field.directionZ = 0f;
            field.gravityFocus = 0f;
            field.enabled = false;

            ParticleSystem.ExternalForcesModule forces = ash.externalForces;
            forces.enabled = true;
            forces.influenceFilter = ParticleSystemGameObjectFilter.List;
            forces.AddInfluence(field);
            return field;
        }

        // 발밑에 깔리고, 지나간 자리에 잠깐 남는 재. 땅에 머물게 월드 좌표로 뿌린다.
        private void BuildGroundAsh(AshEnemyTuning tuning)
        {
            ParticleSystem ash = NewParticles("Ground Ash", tuning);
            ParticleSystem.MainModule main = ash.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(tuning.trailTime * 0.6f, tuning.trailTime);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 3f);
            main.maxParticles = 120;

            ParticleSystem.EmissionModule emission = ash.emission;
            emission.rateOverTime = tuning.trailRate;

            ParticleSystem.ShapeModule shape = ash.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = tuning.bodyWidth * 0.7f;
            ash.transform.localPosition = Vector3.up * 0.3f;
            ash.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            FadeInOut(ash, 0.1f);
        }

        // 나타나고 사라질 때 뚝 끊기지 않게 한다.
        private static void FadeInOut(ParticleSystem particles, float fadeIn)
        {
            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn), new GradientAlphaKey(0f, 1f) });
            color.color = fade;
        }

        private ParticleSystem NewParticles(string particleName, AshEnemyTuning tuning)
        {
            var particleObject = new GameObject(particleName);
            particleObject.transform.SetParent(_body, false);
            var particles = particleObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = tuning.ashColor;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            var particleRenderer = particleObject.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = AshMaterial();
            particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            particles.Play();
            return particles;
        }

        /// 부드러운 원 모양의 재 알갱이. (모닥불이 꺼져 갈 때 맴도는 재에도 쓴다)
        // 부드러운 원 모양의 재 알갱이. 빛을 더하지 않고 어둡게 덮어야 하므로 알파 혼합 셰이더를 쓴다.
        public static Material AshMaterial()
        {
            if (_ashMaterial != null)
                return _ashMaterial;

            const int Size = 64;
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "Ash Puff" };
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = (x + 0.5f) / Size * 2f - 1f;
                    float dy = (y + 0.5f) / Size * 2f - 1f;
                    float edge = 1f - Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float grain = 0.6f + 0.4f * Mathf.PerlinNoise(x * 0.25f, y * 0.25f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, edge * edge * grain));
                }
            }
            texture.Apply();

            _ashMaterial = new Material(Shader.Find("Sprites/Default")) { name = "Ash Puff", mainTexture = texture };
            return _ashMaterial;
        }
    }
}
