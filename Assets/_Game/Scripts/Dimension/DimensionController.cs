using UnityEngine;
using System.Collections.Generic;

/// Real 차원, Fake 차원이 존재하며
/// 발전기가 소리를 전달할 때 쓸 None 값이 따로 존재한다
public enum Dimension { None, Real, Fake }

namespace Squad
{
    /// <summary>
    /// 차원에 관한 일을 맡는다.
    ///   1. DimensionMember(적, 차원별 장애물, 뿌리)를 모아, 플레이어와 다른 차원에 있는 것의 Renderer를 끈다
    ///   2. 키 입력을 받아 플레이어의 차원을 바꾼다
    ///   3. 넘어갈 때 차원별 장애물(DimensionObstacle)이 플레이어의 몸을 막을지 다시 정하고,
    ///      넘어간 자리가 그 차원에서 막혀 있으면 가장 가까운 열린 자리로 옮긴다
    ///
    /// 차원 이동은 어디서든 할 수 있다. 대신 아무 때나 쓰는 탈출 버튼이 되지 않도록 세 가지 규칙이 붙는다.
    ///   재사용 대기   한 번 넘어가면 일정 시간(15초) 동안 다시 넘어갈 수 없다
    ///                 (쫓기다가 넘어갔다 바로 돌아오는 식으로 추격을 끊지 못하게 한다)
    ///   머무는 시간   Fake에는 일정 시간(8초)만 머물 수 있고, 지나면 자동으로 Real로 튕겨 나온다.
    ///                 재사용 대기는 넘어간 순간부터 세므로, 튕겨 나온 뒤 남은 시간(7초)을 더 기다려야 한다
    ///   도착 소리     넘어간 자리에서 소리가 나서, 도착한 차원의 적이 조사하러 온다 (이동의 대가)
    ///   위치 보정     넘어간 자리가 그 차원의 장애물 안이면(예: Fake에서 덤불 고리 위에 서 있다가 Real로 튕겨 나옴)
    ///                 장애물에 끼지 않도록 가장 가까운 열린 자리로 옮긴다
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
        private Astar3D.Pathfinder _pathfinder;

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
            _pathfinder = FindObjectOfType<Astar3D.Pathfinder>();
            // 장애물은 이 컨트롤러보다 먼저(StageLayout이 배치할 때) 만들어지므로, 여기서 한 번 충돌을 정해 준다.
            DimensionObstacle.ApplyCollisions(player);
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
        /// 이름표를 단 것(적, 차원별 장애물, 뿌리)이 자기 자신을 컨트롤러에 등록하도록 하는 함수
        /// </summary>
        public void AddMember(DimensionMember member)
        {
            if (member != null)
            {
                enemies.Add(member);
                member.SetRenderer(IsVisible(member));
            }
        }

        public void RemoveMember(DimensionMember member) => enemies.Remove(member);

        public bool CompareDimension(DimensionMember enemy)
        {
            if (player == null || enemy == null)
                return false;

            return player.myDimension == enemy.Dimension;
        }

        // 지금 플레이어에게 보이는가. 두 차원 모두에 있는 것(None)은 늘 보인다.
        private bool IsVisible(DimensionMember member)
            => member != null && (member.Dimension == Dimension.None || CompareDimension(member));

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
            MoveOutOfObstacle();

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

            foreach (var member in enemies)
                member.SetRenderer(IsVisible(member));

            // 이 차원의 장애물만 몸을 막는다.
            DimensionObstacle.ApplyCollisions(player);
        }

        /// <summary>
        /// 넘어간 자리가 그 차원의 장애물 안이면, 가장 가까운 열린 자리로 플레이어를 옮긴다.
        /// 장애물 칸 위에서 그 장애물이 몸을 막기 시작하면 끼어서 움직이지 못하기 때문이다.
        /// 덤불 고리의 안쪽처럼 막히지 않은 곳에 있으면 그대로 둔다. (고리 안에 갇히는 것은 잠깐의 안전지대가 된다)
        /// </summary>
        private void MoveOutOfObstacle()
        {
            if (_pathfinder == null || _pathfinder.Grid == null)
                return;

            Vector3 position = player.transform.position;
            int maxRing = Mathf.CeilToInt(GameTuning.Instance.map.escapeSearchDistance / 2f);
            Vector3? free = _pathfinder.Grid.EscapeBlocked(position, player.myDimension, maxRing);
            if (free == null || ((Vector3)free - position).sqrMagnitude < 0.01f)
                return;

            Vector3 target = (Vector3)free;
            Terrain terrain = Terrain.activeTerrain;
            target.y = terrain != null
                ? terrain.SampleHeight(target) + terrain.transform.position.y + GameTuning.Instance.stage.playerHeight
                : position.y;

            // CharacterController는 켜져 있는 동안 위치를 직접 바꾸면 무시할 수 있어서, 잠깐 끄고 옮긴다.
            CharacterController body = player.GetComponent<CharacterController>();
            if (body != null)
                body.enabled = false;
            player.transform.position = target;
            if (body != null)
                body.enabled = true;

            // Collider를 껐다 켜면 Unity가 충돌을 끈 설정을 잊으므로 다시 정한다.
            DimensionObstacle.ApplyCollisions(player);
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
