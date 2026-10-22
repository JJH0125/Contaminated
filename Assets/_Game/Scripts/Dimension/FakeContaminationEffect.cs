using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;

namespace Squad
{
    /// <summary>
    /// Fake 차원에 들어가는 순간, 플레이어 자리에서 오염의 물결이 퍼져 나가게 한다.
    /// 물결이 지나간 땅, 나무, 바위에는 Real에서는 보이지 않던 오염의 얼룩이 드러난다.
    /// 알고 보니 이곳은 Real보다 훨씬 오염되어 있었다는 것을 보여 준다.
    ///   들어갈 때    플레이어 자리에서 물결이 빠르게 퍼지고, 앞머리가 희미하게 빛난다.
    ///                물결이 지나간 자리에는 진한 얼룩부터 드러나 둘레로 번진다 (보호복이 오염되는 방식과 같다)
    ///   머무는 동안  물결은 계속 퍼져 맵 전체가 얼룩진다
    ///   돌아올 때    얼룩이 빠르게 걷힌다
    ///
    /// 화면 효과 자체는 FakeContamination(직접 만든 후처리 효과)이 그린다. 이 스크립트는 ContaminationEdgeEffect처럼
    /// 시작할 때 Volume을 코드로 하나 만들고, 물결의 자리와 퍼진 거리, 세기만 정해 준다.
    /// 수치는 GameTuning 에셋의 fakeContamination 항목에서 읽는다.
    ///
    /// 화면 처리로 그리므로 움직이는 캐릭터의 몸에도 얼룩이 묻어 미끄러져 보일 수 있다.
    /// 그래서 플레이어와 Fake 적의 몸 둘레(원기둥)는 얼룩을 지운다.
    ///
    /// 씬 구성:
    ///   붙일 필요가 없다. 씬이 열릴 때마다 Main Camera(PostProcessLayer가 있는 카메라)에 스스로 붙는다.
    /// </summary>
    [RequireComponent(typeof(PostProcessLayer))]
    public class FakeContaminationEffect : MonoBehaviour
    {
        // 얼룩을 지울 몸의 최대 개수 (셰이더의 배열 크기와 같다)
        private const int MaxClear = 8;
        // 몸 목록을 다시 찾는 간격(초). 적은 라운드가 바뀔 때만 생기고 사라지므로 자주 찾을 필요가 없다.
        private const float RefreshInterval = 1f;
        // 플레이어 오브젝트의 위치는 발에서 이만큼(m) 위에 있다.
        private const float PlayerPivotHeight = 1f;
        // PostProcessLayer에 Volume 레이어가 지정되어 있지 않을 때 대신 쓸 레이어 (Unity 기본 레이어인 TransparentFX)
        private const int FallbackVolumeLayer = 1;

        /// 얼룩을 지울 몸: (x, 발 높이, z, 반경)과 발에서 머리까지의 높이. FakeContaminationRenderer가 읽어 간다.
        public static readonly Vector4[] ClearCylinders = new Vector4[MaxClear];
        public static readonly float[] ClearHeights = new float[MaxClear];
        public static int ClearCount { get; private set; }

        private PostProcessVolume _volume;
        private FakeContamination _effect;
        private bool _wasInFake;
        private float _radius;
        private float _refreshTimer;
        private readonly List<ChaserLocomotion> _enemies = new();

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
            if (cam == null || cam.GetComponent<PostProcessLayer>() == null || cam.GetComponent<FakeContaminationEffect>() != null)
                return;

            cam.gameObject.AddComponent<FakeContaminationEffect>();
        }

        private void Awake()
        {
            PostProcessLayer layer = GetComponent<PostProcessLayer>();
            if (layer.volumeLayer.value == 0)
                layer.volumeLayer = 1 << FallbackVolumeLayer;

            _effect = ScriptableObject.CreateInstance<FakeContamination>();
            _effect.enabled.Override(true);
            _effect.presence.Override(0f);
            _effect.origin.Override(Vector3.zero);
            _effect.radius.Override(0f);

            _volume = PostProcessManager.instance.QuickVolume(LowestLayerIn(layer.volumeLayer), 100.2f, _effect);
        }

        private void Update()
        {
            FakeContaminationTuning tuning = GameTuning.Instance.fakeContamination;
            Player player = DimensionController.Instance != null ? DimensionController.Instance.player : null;
            bool inFake = player != null && player.myDimension == Dimension.Fake;

            if (inFake)
            {
                // 막 넘어온 순간: 플레이어 자리에서 물결을 새로 시작한다.
                if (!_wasInFake)
                {
                    _effect.origin.value = player.transform.position;
                    _radius = 0f;
                    _refreshTimer = 0f;
                }

                // 일시정지 중에는 물결도 멈춘다.
                _radius += tuning.spreadSpeed * Time.deltaTime;
                _effect.presence.value = 1f;
            }
            else
            {
                // 돌아오면 얼룩이 걷힌다. (일시정지와 상관없이 자연스럽게 걷히도록 실제 시간을 쓴다)
                float step = tuning.fadeOutTime > 0f ? Time.unscaledDeltaTime / tuning.fadeOutTime : 1f;
                _effect.presence.value = Mathf.MoveTowards(_effect.presence.value, 0f, step);
            }

            _effect.radius.value = _radius;
            _wasInFake = inFake;

            if (_effect.presence.value > 0f)
                UpdateClearCylinders(player, tuning);
        }

        // 얼룩을 지울 몸(플레이어와 Fake 적)의 자리를 모은다.
        private void UpdateClearCylinders(Player player, FakeContaminationTuning tuning)
        {
            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer <= 0f)
            {
                _refreshTimer = RefreshInterval;
                _enemies.Clear();
                foreach (ChaserLocomotion enemy in FindObjectsOfType<ChaserLocomotion>())
                {
                    DimensionMember member = enemy.GetComponent<DimensionMember>();
                    if (member != null && member.Dimension == Dimension.Fake)
                        _enemies.Add(enemy);
                }
            }

            int count = 0;
            if (player != null)
            {
                Vector3 p = player.transform.position;
                ClearCylinders[count] = new Vector4(p.x, p.y - PlayerPivotHeight, p.z, tuning.playerClearRadius);
                ClearHeights[count] = tuning.playerClearHeight;
                count++;
            }

            foreach (ChaserLocomotion enemy in _enemies)
            {
                if (count >= MaxClear)
                    break;
                if (enemy == null || !enemy.isActiveAndEnabled)
                    continue;

                Vector3 p = enemy.transform.position;
                float foot = p.y - GameTuning.Instance.fakeEnemy.heightAboveGround;
                ClearCylinders[count] = new Vector4(p.x, foot, p.z, tuning.enemyClearRadius);
                ClearHeights[count] = tuning.enemyClearHeight;
                count++;
            }

            ClearCount = count;
        }

        // 코드로 만든 Volume은 씬과 함께 저절로 정리되지 않으므로 직접 지운다.
        private void OnDestroy()
        {
            if (_volume != null)
                RuntimeUtilities.DestroyVolume(_volume, true, true);
            ClearCount = 0;
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
