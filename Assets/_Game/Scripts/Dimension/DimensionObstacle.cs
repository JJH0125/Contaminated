using System.Collections.Generic;
using Astar3D;
using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 한 차원에서만(또는 두 차원 모두에서) 길을 막는 장애물.
    ///   Real 장애물   모닥불을 둘러싼 덤불 고리. Fake에서는 없는 것처럼 지나갈 수 있다
    ///   Fake 장애물   오염 웅덩이. Real에서는 없는 것처럼 지나갈 수 있다
    ///   두 차원 모두  닫힌 구역을 막는 두꺼운 덤불 벽
    /// 어느 차원인지는 같은 오브젝트의 DimensionMember가 정한다. (보이고 숨는 것도 DimensionMember가 한다)
    ///
    /// 두 가지를 막는다.
    ///   적      길찾기 격자의 칸을 그 차원에서 막는다. 적은 자기 차원의 칸만 보고 길을 찾으므로,
    ///           같은 맵에서 Real 적과 Fake 적이 서로 다른 길로 다닌다
    ///   플레이어 Collider가 플레이어의 몸을 막는다. 플레이어가 다른 차원에 있으면 충돌을 꺼 둔다.
    ///           (레이어 설정은 ProjectSettings에 있어 Git으로 옮겨지지 않으므로, 레이어 대신 충돌을 하나하나 끈다)
    ///
    /// Collider는 colliderRoot 아래에서만 모은다. 모습으로 쓰는 나무·풀 복제본에 남은 Collider가 섞이지 않게 하려는 것이다.
    /// 불이 붙어 장벽이 열리면 Open을 부른다. 막는 것은 그 순간 풀리고, 모습은 따로 타서 사라진다(BurnAway).
    ///
    /// 씬 구성:
    ///   붙일 필요가 없다. StageLayout이 라운드마다 장벽을 만들 때 붙인다.
    /// </summary>
    [RequireComponent(typeof(DimensionMember))]
    public class DimensionObstacle : MonoBehaviour
    {
        // 지금 있는 모든 차원별 장애물. 플레이어가 차원을 넘을 때 충돌을 한꺼번에 다시 정한다.
        private static readonly List<DimensionObstacle> _all = new();

        private DimensionMember _member;
        private Transform _colliderRoot;
        private Collider[] _colliders = new Collider[0];
        private List<Node> _nodes;
        private bool _open;

        /// <summary>막는 차원. None이면 두 차원 모두.</summary>
        public Dimension BlockedIn => _member != null ? _member.Dimension : Dimension.None;

        /// <summary>
        /// 장애물을 켠다. colliderRoot 아래의 Collider로 몸을 막고, 그 자리의 격자 칸을 막는다.
        /// 만든 쪽이 바로 부르므로, 같은 프레임에 이어서 길을 찾아도(예: 뿌리의 경로) 이 장애물을 피한다.
        /// </summary>
        public void Setup(Transform colliderRoot)
        {
            _member = GetComponent<DimensionMember>();
            _colliderRoot = colliderRoot;
            _colliders = colliderRoot != null ? colliderRoot.GetComponentsInChildren<Collider>() : new Collider[0];

            Pathfinder pathfinder = FindObjectOfType<Pathfinder>();
            if (pathfinder != null && pathfinder.Grid != null && _nodes == null)
            {
                _nodes = new List<Node>();
                foreach (Collider part in _colliders)
                    foreach (Node node in pathfinder.Grid.NodesInside(part))
                        if (!_nodes.Contains(node))
                            _nodes.Add(node);
                PathGrid.AddBlock(_nodes, BlockedIn, 1);
            }

            if (!_all.Contains(this))
                _all.Add(this);

            if (DimensionController.Instance != null)
                Apply(DimensionController.Instance.player);
        }

        /// <summary>이 차원에서 지금 막고 있는가.</summary>
        public bool Blocks(Dimension dimension)
            => !_open && (BlockedIn == Dimension.None || BlockedIn == dimension);

        /// <summary>장벽을 연다. 몸도 길도 더 막지 않는다. (모습은 그대로 남으므로 따로 치운다)</summary>
        public void Open()
        {
            if (_open)
                return;

            _open = true;
            PathGrid.AddBlock(_nodes, BlockedIn, -1);
            foreach (Collider part in _colliders)
                if (part != null)
                    part.enabled = false;
        }

        /// <summary>플레이어가 지금 있는 차원에 맞게, 모든 장애물이 플레이어의 몸을 막을지 다시 정한다.</summary>
        public static void ApplyCollisions(Player player)
        {
            foreach (DimensionObstacle obstacle in _all)
                if (obstacle != null)
                    obstacle.Apply(player);
        }

        private void Apply(Player player)
        {
            if (player == null)
                return;

            CharacterController body = player.GetComponent<CharacterController>();
            if (body == null)
                return;

            bool ignore = !Blocks(player.myDimension);
            foreach (Collider part in _colliders)
                if (part != null && part.enabled)
                    Physics.IgnoreCollision(body, part, ignore);
        }

        // Stage와 함께 사라질 때 막아 둔 칸을 풀어 준다. (격자는 씬에 남아 다음 라운드에도 쓰인다)
        private void OnDestroy()
        {
            if (!_open)
                PathGrid.AddBlock(_nodes, BlockedIn, -1);
            _all.Remove(this);
        }
    }
}
