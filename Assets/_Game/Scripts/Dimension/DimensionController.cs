using UnityEngine;
using System.Collections.Generic;

/// Real 차원, Fake 차원이 존재하며
/// 발전기가 소리를 전달할 때 쓸 None 값이 따로 존재한다
public enum Dimension { None, Real, Fake }

namespace Squad
{
    /// <summary>
    /// 차원에 관한 일을 맡는다.
    ///   1. 적들의 DimensionMember를 모아, 플레이어와 다른 차원에 있는 적의 Renderer를 끈다
    ///   2. 키 입력을 받아 플레이어의 차원을 바꾼다
    ///
    /// 차원 이동은 어디서든 할 수 있다. 대신 아무 때나 쓰는 탈출 버튼이 되지 않도록 세 가지 규칙이 붙는다.
    ///   재사용 대기   한 번 넘어가면 일정 시간(15초) 동안 다시 넘어갈 수 없다
    ///                 (쫓기다가 넘어갔다 바로 돌아오는 식으로 추격을 끊지 못하게 한다)
    ///   머무는 시간   Fake에는 일정 시간(8초)만 머물 수 있고, 지나면 자동으로 Real로 튕겨 나온다.
    ///                 재사용 대기는 넘어간 순간부터 세므로, 튕겨 나온 뒤 남은 시간(7초)을 더 기다려야 한다
    ///   도착 소리     넘어간 자리에서 소리가 나서, 도착한 차원의 적이 조사하러 온다 (이동의 대가)
    ///   감지 유예     넘어간 직후 잠깐은 어떤 적에게도 감지되지 않고 잡히지도 않는다
    ///                 (반대편에 적이 서 있는지 알 방법이 없으므로, 넘어가자마자 잡히는 억울한 죽음을 막는다)
    ///
    /// 씬 구성:
    ///   Stage 프리팹 안의 빈 오브젝트에 붙인다. 라운드마다 Stage와 함께 새로 만들어지므로,
    ///   재사용 대기도 라운드가 바뀌면 처음부터 다시 시작한다.
    /// </summary>
    public class DimensionController : MonoBehaviour
    {
        [Header("○ 튜닝 값 — 자유롭게 조절")]
        [Tooltip("차원을 이동하는 키")]
        [SerializeField] private KeyCode shiftKey = KeyCode.F;
        private float cooldown => GameTuning.Instance.dimension.cooldown;
        private float fakeStayTime => GameTuning.Instance.dimension.fakeStayTime;
        private float undetectableTime => GameTuning.Instance.dimension.undetectableTime;
        [Tooltip("도착 소리를 들을 수 있는 적의 레이어")]
        [SerializeField] private LayerMask enemyLayer = (1 << 8) | (1 << 9);   // RealEnemy + FakeEnemy

        public Player player { get; private set; }
        private HashSet<DimensionMember> enemies;

        // 다시 이동할 수 있게 되는 시각, 감지 유예가 끝나는 시각
        private float _readyTime;
        private float _undetectableUntil;
        // Fake에서 Real로 튕겨 나오는 시각
        private float _forceReturnTime;

        public static DimensionController Instance { get; private set; }

        /// 차원을 이동하는 키 (화면 안내가 읽어 간다)
        public KeyCode ShiftKey => shiftKey;
        /// 다시 이동할 수 있을 때까지 남은 시간(초). 지금 이동할 수 있으면 0.
        public float CooldownRemaining => Mathf.Max(0f, _readyTime - Time.time);
        /// Fake에 있을 때 Real로 튕겨 나오기까지 남은 시간(초). Real에 있으면 0.
        public float FakeTimeRemaining =>
            player != null && player.myDimension == Dimension.Fake ? Mathf.Max(0f, _forceReturnTime - Time.time) : 0f;

        void Awake()
        {
            // 기존에 있던 DimensionController Instance 제거
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            player = FindObjectOfType<Player>();
            enemies = new HashSet<DimensionMember>();
        }

        void Start()
        {
            // 남은 대기 시간을 보여 줄 화면 안내가 없으면 만들어 둔다.
            DimensionShiftLabel.EnsureExists();
        }

        void Update()
        {
            // 일시정지 중이거나, 잡혀서 연출이 나오는 중에는 이동하지 않는다.
            if (Time.timeScale == 0f || player == null)
                return;
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
                return;

            // Fake에 머물 수 있는 시간이 지나면 의지와 상관없이 Real로 튕겨 나온다.
            if (player.myDimension == Dimension.Fake && Time.time >= _forceReturnTime)
            {
                ShiftPlayerDimension(false);
                return;
            }

            if (Input.GetKeyDown(shiftKey) && CooldownRemaining <= 0f)
                ShiftPlayerDimension(true);
        }

        /// <summary>
        /// 적들이 자기 자신을 컨트롤러에 등록하도록 하는 함수
        /// </summary>
        /// <param name="enemy"></param>
        public void AddEnemy(DimensionMember enemy)
        {
            if (enemy != null)
            {
                enemies.Add(enemy);
                enemy.SetRenderer(CompareDimension(enemy));
            }
        }

        public bool CompareDimension(DimensionMember enemy)
        {
            if (player == null || enemy == null)
                return false;

            return player.myDimension == enemy.Dimension;
        }

        /// <summary>
        /// enemy가 지금 플레이어를 감지하거나 잡을 수 있는가.
        /// 같은 차원에 있어야 하고, 플레이어가 막 넘어온 직후의 감지 유예 시간이 지나 있어야 한다.
        ///
        /// 적이 화면에 보이는지(CompareDimension)와는 따로 둔다.
        /// 유예 시간 동안에도 적은 화면에 보여야, 플레이어가 그 사이에 피할 수 있다.
        /// </summary>
        public bool CanDetectPlayer(DimensionMember enemy)
            => CompareDimension(enemy) && Time.time >= _undetectableUntil;

        // 차원을 넘어간다. 감지 유예를 시작하고, 도착한 차원에 소리를 낸다.
        // byPlayer: 키를 눌러 넘어간 것이면 true. 이때만 재사용 대기를 새로 시작한다.
        //           (Fake에서 튕겨 나올 때는 넘어갈 때 시작한 대기가 그대로 이어진다)
        private void ShiftPlayerDimension(bool byPlayer)
        {
            SwitchPlayerDimension();

            if (byPlayer)
                _readyTime = Time.time + cooldown;
            if (player.myDimension == Dimension.Fake)
                _forceReturnTime = Time.time + fakeStayTime;
            _undetectableUntil = Time.time + undetectableTime;

            // 도착한 차원의 적만 듣는다. (소리의 값에서 차원을 관통하지 않도록 정해 둔다)
            SoundEmitter.Emit(player.transform.position, SoundList.DimensionShift, enemyLayer, player.myDimension);
        }

        public void SwitchPlayerDimension()
        {
            player.SwitchMyDimension();

            string layerName = player.myDimension == Dimension.Real ? "RealPlayer" : "FakePlayer";
            SetLayerRecursively(player.gameObject, LayerMask.NameToLayer(layerName));

            foreach (var enemy in enemies)
                enemy.SetRenderer(CompareDimension(enemy));
        }

        /// <summary>
        /// 오브젝트와 그 모든 자식의 레이어를 재귀적으로 변경
        /// </summary>
        private void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;

            foreach (Transform child in obj.transform)
                SetLayerRecursively(child.gameObject, layer);
        }
    }
}
