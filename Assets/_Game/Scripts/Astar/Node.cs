using UnityEngine;

namespace Astar3D
{
    public class Node
    {
        public readonly bool Walkable;
        public readonly Vector3 WorldPosition;   // 3D — on the floor (XZ), fixed Y
        public readonly int GridX;  // 가로로 몇 번째 노드
        public readonly int GridY;  // 세로로 몇 번째 노드

        // 차원별로 이 칸을 막고 있는 장애물의 수. 장벽은 라운드 중에 생기고 사라지므로 바뀔 수 있다.
        // (Walkable은 씬이 시작될 때 나무 기둥 등으로 구운 값이라 두 차원에 똑같이 적용된다)
        public int RealBlocks;
        public int FakeBlocks;

        /// <summary>
        /// 그 차원에서 걸을 수 있는 칸인가.
        /// Dimension.None은 "두 차원 모두"라는 뜻이다. 두 차원 어디에서도 막히지 않은 칸만 걸을 수 있다.
        /// </summary>
        public bool IsWalkableIn(Dimension dimension)
        {
            if (!Walkable)
                return false;

            return dimension switch
            {
                Dimension.Real => RealBlocks == 0,
                Dimension.Fake => FakeBlocks == 0,
                _ => RealBlocks == 0 && FakeBlocks == 0,
            };
        }

        public int GCost;
        public int HCost;
        public Node Parent;
        public int HeapIndex;

        public int FCost => GCost + HCost;

        public Node(bool walkable, Vector3 worldPosition, int gridX, int gridY)
        {
            Walkable = walkable;
            WorldPosition = worldPosition;
            GridX = gridX;
            GridY = gridY;
        }
    }
}
