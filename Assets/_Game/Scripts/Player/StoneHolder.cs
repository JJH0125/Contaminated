using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 플레이어가 돌을 줍고 던지는 것을 관리한다. 던진 돌은 미끼가 된다(Stone).
    ///   줍기    근처(pickupRadius)에 떨어진 돌이 있으면 E로 바로 줍는다. 한 번에 하나만 들 수 있다
    ///   던지기  돌을 든 채로 다시 E를 누르면 바라보는 방향으로 바로 던진다
    /// 줍고 던질 때 따로 동작(애니메이션)은 없다. 걷거나 뛰는 중에도 그대로 줍고 던진다.
    ///
    /// 오른손에는 횃불이 있으므로 돌은 왼손에 든다.
    /// 수치(줍는 거리, 던지는 속도 등)는 GameTuning 에셋의 stone 항목에서 읽는다.
    ///
    /// 씬 구성:
    ///   Player가 붙은 오브젝트에 붙인다. 돌은 StageLayout이 맵 곳곳에 놓는다.
    /// </summary>
    [RequireComponent(typeof(Player))]
    public class StoneHolder : MonoBehaviour
    {
        [Header("○ 설정")]
        [Tooltip("돌을 줍고 던지는 키")]
        [SerializeField] private KeyCode useKey = KeyCode.E;
        [Tooltip("돌 근처에서 화면에 띄울 안내 문구")]
        [SerializeField] private string pickupMessage = "[E] 돌 줍기";
        [Tooltip("돌을 쥘 손뼈의 이름. 켜져 있는 모델에서 찾는다")]
        [SerializeField] private string handBoneName = "mixamorig:LeftHand";
        [Tooltip("던진 돌이 떨어지는 소리를 들을 수 있는 적의 레이어")]
        [SerializeField] private LayerMask enemyLayer = (1 << 8) | (1 << 9);   // RealEnemy + FakeEnemy

        private static StoneTuning Tuning => GameTuning.Instance.stone;

        private Player _player;
        private Transform _hand;

        // 지금 든 돌. 빈손이면 null.
        private Stone _held;
        // 지금 줍기 안내를 띄워 두었는가
        private bool _promptShown;

        public bool HasStone => _held != null;

        private void Awake()
        {
            _player = GetComponent<Player>();
            foreach (Transform child in GetComponentsInChildren<Transform>())
                if (child.name == handBoneName)
                    _hand = child;
        }

        private void Update()
        {
            // 일시정지 중에는 줍거나 던지지 않는다.
            if (Time.timeScale == 0f)
                return;

            // 꺼진 모닥불 곁에서는 E가 불 붙이기다. (CampfireLighter)
            if (CampfireLighter.HasTarget)
            {
                SetPrompt(false);
                return;
            }

            Stone nearby = HasStone ? null : Stone.Nearest(transform.position, Tuning.pickupRadius);
            SetPrompt(nearby != null);

            if (!Input.GetKeyDown(useKey))
                return;

            if (HasStone)
                Throw();
            else if (nearby != null)
                PickUp(nearby);
        }

        // 든 돌을 손에 붙여 둔다. (애니메이션이 손을 움직인 뒤에 맞춰야 하므로 LateUpdate에서 한다)
        private void LateUpdate()
        {
            if (_held == null)
                return;

            if (_hand != null)
                _held.transform.position = _hand.position + _hand.rotation * (Tuning.handOffset * ModelScale());
            else
                _held.transform.position = transform.position + transform.right * -0.6f + transform.forward * 0.3f;
        }

        private void PickUp(Stone stone)
        {
            _held = stone;
            stone.OnPickedUp();
            SetPrompt(false);
        }

        // 든 돌을 손에서 놓아 바라보는 방향으로 던진다.
        private void Throw()
        {
            Stone stone = _held;
            _held = null;

            // 비탈에 서 있으면 그 기울기를 따라 던진다. 수평으로만 던지면 오르막에서 발 앞의 땅에 바로 박힌다.
            Vector3 forward = transform.forward;
            Terrain terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                Vector3 local = transform.position - terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                Vector3 groundNormal = terrain.terrainData.GetInterpolatedNormal(local.x / size.x, local.z / size.z);
                forward = Vector3.ProjectOnPlane(forward, groundNormal).normalized;
            }

            stone.Throw(forward * Tuning.throwSpeed + Vector3.up * Tuning.throwUpSpeed, _player.myDimension, enemyLayer);
        }

        // 손뼈가 들어 있는 모델(플레이어 바로 아래 자식)의 Scale.
        private float ModelScale()
        {
            Transform model = _hand;
            while (model.parent != null && model.parent != transform)
                model = model.parent;

            return model.localScale.x;
        }

        private void SetPrompt(bool show)
        {
            if (show == _promptShown || InteractionPrompt.Instance == null)
                return;

            _promptShown = show;
            if (show)
                InteractionPrompt.Instance.Show(this, pickupMessage);
            else
                InteractionPrompt.Instance.Hide(this);
        }

        // Stage가 사라질 때 안내가 남지 않게 한다.
        private void OnDisable() => SetPrompt(false);
    }
}
