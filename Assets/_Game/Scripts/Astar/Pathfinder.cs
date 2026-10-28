using System.Collections.Generic;
using UnityEngine;

namespace Astar3D
{     
    // 이 스크립트를 오브젝트에 추가하면
    // PathGrid 스크립트는 자동으로 추가된다
    // PathGrid가 있어야 하는 PathFinder에 있어서 에러 방지 역할을 함.
    [RequireComponent(typeof(PathGrid))]
    public class Pathfinder : MonoBehaviour
    {
        private PathGrid _grid;
        // 바로 옆 노드까지의 거리 = 1
        private const int StraightCost = 10;
        // 대각선의 노드까지의 거리 = 1.4
        private const int DiagonalCost = 14;

        private void Awake() => _grid = GetComponent<PathGrid>();

        public bool IsWalkable(Vector3 worldPos, Dimension dimension = Dimension.None) => _grid.IsWalkable(worldPos, dimension);

        /// 격자 자체. 차원별 장애물을 칸에 더하고 뺄 때 쓴다.
        public PathGrid Grid => _grid;

        /// <summary>
        /// Returns a list of 3D waypoints from start to target, or null if no
        /// path exists.
        /// </summary>
        /// dimension: 그 차원의 장애물만 피한다. 적은 자기 차원을 넘긴다. (None이면 두 차원의 장애물을 모두 피한다)
        public List<Vector3> FindPath(Vector3 startPos, Vector3 targetPos, Dimension dimension = Dimension.None)
        {
            // 벽에 바짝 붙어 있어서 그 칸이 벽으로 판정되면 가장 가까운 빈 칸에서 출발/도착한다.
            Node startNode = _grid.ClosestWalkableNode(startPos, 2, dimension);
            Node targetNode = _grid.ClosestWalkableNode(targetPos, 2, dimension);

            // 주변에도 빈 칸이 없다면 경로 탐색 x
            if (startNode == null || targetNode == null)
                return null;

            // 계산을 수행할 노드를 넣을 집합
            var openSet = new NodeHeap(_grid.MaxSize);
            // 계산이 끝난 노드를 넣을 집합
            // 중복을 허용하지 않는 집합으로 closedSet을 생성
            var closedSet = new HashSet<Node>();

            startNode.GCost = 0;
            startNode.HCost = Heuristic(startNode, targetNode);
            startNode.Parent = null;
            openSet.Add(startNode);

            // openSet이 비워질 때까지 반복.
            // 즉 목적지에 도달하거나, 길이 없을 때까지
            while (openSet.Count > 0)
            {
                Node current = openSet.RemoveFirst();
                closedSet.Add(current);

                // 목적지 도달
                if (current == targetNode)
                    return RetracePath(startNode, targetNode);

                // 이웃한 노드들 하나씩 호출
                foreach (Node neighbor in _grid.GetNeighbors(current, dimension))
                {
                    // 이웃한 노드가 벽이거나 이미 방문한 곳일 경우 생략
                    if (!neighbor.IsWalkableIn(dimension) || closedSet.Contains(neighbor))
                        continue;

                    int tentativeG = current.GCost + Distance(current, neighbor);
                    bool inOpen = openSet.Contains(neighbor);

                    // 이웃 노드가 이미 방문했던 노드인데,
                    // 기록된 기존 루트보다 지금 가는 이 루트의 비용이 더 적을 때
                    // 또는 처음 방문하는 노드일 때.
                    // 만나지 못한 노드의 GCost는 0으로 초기화되는데
                    // (초기화가 명시적으로 안 되어있을 뿐 C#에서 알아서 초기화해줌)
                    // 단순히 G 값만 비교하게 되면 openSet에 들어갈 수가 없게 되니
                    // inOpen이라는 bool 값을 통해
                    // 처음 만난 노드라면 G 값에 상관없이 openSet에 들어가도록 함.
                    // 이론상으로는 아직 만나지 않은 노드의 G 값을 무한대로 초기화하는데,
                    // 코드상에서는 무한대 대신 이런 방식으로 처음 만난 노드의
                    // openSet 추가를 보장함.
                    if (tentativeG < neighbor.GCost || !inOpen)
                    {
                        // 노드의 비용, 부모 노드 정보 갱신
                        neighbor.GCost = tentativeG;
                        neighbor.HCost = Heuristic(neighbor, targetNode);
                        neighbor.Parent = current;

                        // 처음 방문한 것이라면 openSet에 추가
                        if (!inOpen)
                            openSet.Add(neighbor);
                        // 이미 openSet에 있었다면, 더 짧은 거리로 갱신되었기 때문에
                        // 이를 바탕으로 힙 안에서 위치 재정렬
                        else
                            openSet.UpdateItem(neighbor);
                    }
                }
            }

            return null;
        }

        // 휴리스틱 거리 반환.
        private int Heuristic(Node a, Node b)
        {
            int dx = Mathf.Abs(a.GridX - b.GridX);
            int dy = Mathf.Abs(a.GridY - b.GridY);
            return DiagonalCost * Mathf.Min(dx, dy)
            + StraightCost * Mathf.Abs(dx - dy);
        }

        // 두 노드 사이의 거리.
        // 이웃한 노드 사이에서만 호출되기 때문에 항상 10 또는 14
        private int Distance(Node a, Node b)
        {
            int dx = Mathf.Abs(a.GridX - b.GridX);
            int dy = Mathf.Abs(a.GridY - b.GridY);
            return (dx != 0 && dy != 0) ? DiagonalCost : StraightCost;
        }

        // 목적지에 도달할 경우
        // 각 노드가 갖고 있는 부모 노드 정보를 바탕으로
        // 지나온 길을 거슬러 올라가 추적하는 함수
        private List<Vector3> RetracePath(Node start, Node end)
        {
            var path = new List<Node>();
            Node current = end;
            while (current != start)
            {
                path.Add(current);
                current = current.Parent;
            }
            path.Reverse();

            _grid.DebugPath = path;

            var waypoints = new List<Vector3>(path.Count);
            foreach (Node n in path)
                waypoints.Add(n.WorldPosition);
            return waypoints;
        }
    }
}
