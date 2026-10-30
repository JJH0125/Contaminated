using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;

namespace Squad
{
    /// <summary>
    /// 플레이어와 같은 차원의 적이 가까이 오면, 화면에 감시카메라처럼 지지직거리는 노이즈를 입힌다.
    /// 다른 차원의 적은 아무리 가까워도 노이즈를 일으키지 않는다.
    ///
    /// 세기는 가장 가까운 적과의 거리(위에서 내려다본 평면 거리)로 정한다.
    ///   farDistance보다 멀면 0, nearDistance 안에 들어오면 최대. 그 사이는 멀 때 약하고 가까울수록 급하게 세진다
    /// 세기가 매끄럽게만 오르면 단순한 필터처럼 보이므로, 불규칙하게 출렁이고 가끔 확 튀게 해서
    /// 신호가 끊기는 느낌을 준다.
    ///
    /// 화면과 같은 세기로 지지직 소리도 낸다. 소리는 StaticNoiseSound가 음원 파일 없이 만든다.
    ///
    /// 화면 효과 자체는 CctvNoise(직접 만든 후처리 효과)가 그린다. 이 스크립트는 FakeDimensionEffect처럼
    /// 시작할 때 Volume을 코드로 하나 만들고, 그 효과의 세기만 조절한다.
    ///
    /// 씬 구성:
    ///   붙일 필요가 없다. 씬이 열릴 때마다 Main Camera(PostProcessLayer가 있는 카메라)에 스스로 붙는다.
    /// </summary>
    [RequireComponent(typeof(PostProcessLayer))]
    public class CctvNoiseEffect : MonoBehaviour
    {
        // 수치는 GameTuning 에셋(Resources/GameTuning)에서 읽는다.
        private float farDistance => GameTuning.Instance.cctv.farDistance;
        private float nearDistance => GameTuning.Instance.cctv.nearDistance;
        private float maxIntensity => GameTuning.Instance.cctv.maxIntensity;
        private float flickerSpeed => GameTuning.Instance.cctv.flickerSpeed;
        private float spikesPerSecond => GameTuning.Instance.cctv.spikesPerSecond;
        private float soundVolume => GameTuning.Instance.cctv.soundVolume;

        // PostProcessLayer에 Volume 레이어가 지정되어 있지 않을 때 대신 쓸 레이어 (Unity 기본 레이어인 TransparentFX)
        private const int FallbackVolumeLayer = 1;
        // 적 목록을 다시 모으는 간격(초). 적은 라운드가 바뀔 때만 생기고 사라지므로 자주 찾을 필요가 없다.
        private const float EnemyRefreshInterval = 1f;
        // 확 튄 노이즈가 가라앉는 데 걸리는 시간(초)
        private const float SpikeFadeTime = 0.15f;

        private PostProcessVolume _volume;
        private CctvNoise _noise;
        private DimensionMember[] _enemies = new DimensionMember[0];
        private float _nextRefreshTime;
        private float _spike;
        private StaticNoiseSound _sound;
        // SfxPlayer가 없을 때(테스트 씬 등) 쓰는 효과음 볼륨
        private const float FallbackSfxVolume = 0.5f;

        /// <summary>
        /// 같은 차원에서 가장 가까운 적이 얼마나 가까운가 (0: 멀거나 없음, 1: 아주 가까움).
        /// 노이즈 세기의 바탕이 되는 값으로, 출렁임과 튐은 들어 있지 않다.
        /// 숨소리(PlayerBreathing)처럼 노이즈와 함께 반응해야 하는 것들이 읽어 간다.
        /// </summary>
        public static float Closeness { get; private set; }

        // 씬이 열릴 때마다 Main Camera에 이 효과를 붙인다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            AttachToMainCamera();
            SceneManager.sceneLoaded += (_, _) => AttachToMainCamera();
        }

        private static void AttachToMainCamera()
        {
            Camera cam = Camera.main;
            if (cam == null || cam.GetComponent<PostProcessLayer>() == null || cam.GetComponent<CctvNoiseEffect>() != null)
                return;

            cam.gameObject.AddComponent<CctvNoiseEffect>();
        }

        private void Awake()
        {
            PostProcessLayer layer = GetComponent<PostProcessLayer>();
            // PostProcessLayer는 자기가 읽기로 한 레이어에 있는 Volume만 본다.
            if (layer.volumeLayer.value == 0)
                layer.volumeLayer = 1 << FallbackVolumeLayer;

            _noise = ScriptableObject.CreateInstance<CctvNoise>();
            _noise.enabled.Override(true);
            _noise.intensity.Override(0f);

            // Fake 차원 효과(우선순위 100)보다 나중에 입혀지도록 우선순위를 높게 둔다.
            _volume = PostProcessManager.instance.QuickVolume(LowestLayerIn(layer.volumeLayer), 101f, _noise);

            // 지지직 소리는 자식 오브젝트에서 낸다. (카메라 오브젝트의 AudioListener를 건드리지 않도록)
            var soundObject = new GameObject("CCTV Static Sound");
            soundObject.transform.SetParent(transform, false);
            _sound = soundObject.AddComponent<StaticNoiseSound>();
        }

        private void Update()
        {
            float closeness = ClosenessOfNearestEnemy();
            Closeness = closeness;

            // 가끔 확 튀었다가 금방 가라앉는다. 적이 가까울수록 자주 튄다.
            if (Random.value < spikesPerSecond * closeness * Time.unscaledDeltaTime)
                _spike = 1f;
            _spike = Mathf.MoveTowards(_spike, 0f, Time.unscaledDeltaTime / SpikeFadeTime);

            // 0.6~1배 사이로 불규칙하게 출렁인다.
            float flicker = 0.6f + 0.4f * Mathf.PerlinNoise(Time.unscaledTime * flickerSpeed, 0f);
            float intensity = closeness * maxIntensity * flicker + _spike * 0.4f * closeness;
            // 디버그용 깨끗한 화면(F2)에서는 화면 노이즈만 끈다. (지지직 소리는 그대로 내서 적이 가까운 걸 알 수 있게 한다)
            _noise.intensity.value = CleanViewDebug.On ? 0f : Mathf.Clamp01(intensity);

            // 소리도 화면과 같은 세기로 출렁이고 튄다.
            float sfxVolume = SfxPlayer.Instance != null ? SfxPlayer.Instance.Volume : FallbackSfxVolume;
            _sound.Level = Mathf.Clamp01(intensity) * soundVolume * sfxVolume;
        }

        // 코드로 만든 Volume은 씬과 함께 저절로 정리되지 않으므로 직접 지운다.
        private void OnDestroy()
        {
            if (_volume != null)
                RuntimeUtilities.DestroyVolume(_volume, true, true);
        }

        // 같은 차원에서 가장 가까운 적이 얼마나 가까운가 (0: farDistance 밖이거나 없음, 1: nearDistance 안)
        private float ClosenessOfNearestEnemy()
        {
            DimensionController controller = DimensionController.Instance;
            if (controller == null || controller.player == null)
                return 0f;

            if (Time.unscaledTime >= _nextRefreshTime)
            {
                _enemies = FindObjectsOfType<DimensionMember>();
                _nextRefreshTime = Time.unscaledTime + EnemyRefreshInterval;
            }

            Vector3 player = controller.player.transform.position;
            float nearest = float.PositiveInfinity;
            foreach (DimensionMember enemy in _enemies)
            {
                // 라운드가 끝나 사라진 적과 다른 차원의 적은 건너뛴다.
                if (enemy == null || !controller.CompareDimension(enemy))
                    continue;

                Vector3 offset = enemy.transform.position - player;
                offset.y = 0f;
                nearest = Mathf.Min(nearest, offset.magnitude);
            }

            // 거리에 정비례하면 적이 플레이어를 볼 수 있는 거리에서 이미 화면이 많이 가려지므로,
            // 제곱해서 멀 때는 희미하고 가까워질수록 급하게 세지게 한다.
            float linear = Mathf.Clamp01(Mathf.InverseLerp(farDistance, nearDistance, nearest));
            return linear * linear;
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
