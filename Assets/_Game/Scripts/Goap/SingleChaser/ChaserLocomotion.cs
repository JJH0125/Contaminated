using UnityEngine;
using Astar3D;
using System.Collections.Generic;

namespace Squad
{
    /// <summary>
    /// Wraps the 3D A* Pathfinder so GOAP actions can just say "move to X"
    /// without knowing how pathfinding works. One of these lives on each chaser.
    ///
    /// This is the bridge between the two layers of A* we talked about:
    ///   - GOAP planner  = A* over WORLD STATES ("what to do")
    ///   - this class    = drives A* over the GRID       ("how to get there")
    ///
    /// It caches the current path and follows it waypoint by waypoint, replanning
    /// the grid path only when the target moves far or the path runs out.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ChaserLocomotion : MonoBehaviour
    {
        // 수치는 GameTuning 에셋의 적 항목(Real 적 / Fake 적)에서 이 적의 차원에 맞는 것을 읽는다.
        private DimensionMember _member;
        private EnemyTuning Tuning
        {
            get
            {
                if (_member == null)
                    _member = GetComponent<DimensionMember>();
                return GameTuning.Instance.EnemyFor(_member);
            }
        }
        // 이 적의 차원. 길을 찾을 때 자기 차원의 장애물만 피한다. (이름표가 없으면 두 차원의 장애물을 모두 피한다)
        private Dimension MyDimension
        {
            get
            {
                if (_member == null)
                    _member = GetComponent<DimensionMember>();
                return _member != null ? _member.Dimension : Dimension.None;
            }
        }
        private float calmMoveSpeed => Tuning.calmMoveSpeed;
        private float suspiciousMoveSpeed => Tuning.suspiciousMoveSpeed;
        private float alertedMoveSpeed => Tuning.alertedMoveSpeed;
        [Tooltip("적이 고개를 돌리는 속도")]

        private float turnSpeed => Tuning.turnSpeed;
        [Tooltip("waypoint에 접근하여 다음 waypoint로 이동을 시작하는 거리")]

        private float waypointTolerance => Tuning.waypointTolerance;
        [Tooltip("플레이어가 유의미하게 움직였다 의 기준")]

        private float repathThreshold => Tuning.repathThreshold;
        [Tooltip("한 번의 Wander을 통해 이동하는 최대 거리")]

        private float wanderRadius => Tuning.wanderRadius;
        private float heightAboveGround => Tuning.heightAboveGround;
        private float footprintRadius => Tuning.footprintRadius;

        private Rigidbody _rigidbody;
        // 씬에 지형(Terrain)이 있으면 그 높이를 따라 움직인다. 없으면 평평한 맵으로 보고 높이를 유지한다.
        private Terrain _terrain;
        // 발이 닿는 범위의 둘레에서 땅 높이를 잴 지점의 수
        private const int FootprintSamples = 8;
        private List<Vector3> _path;
        private int _waypointIndex;
        private Vector3 _lastPathTarget;
        // 경로를 찾을 때의 PathGrid.Version. 막힌 칸이 바뀌면 경로를 다시 찾는다.
        private int _pathGridVersion;
        private bool _hasWanderTarget;
        private Vector3 _wanderTarget;
        private Pathfinder _pathfinder;

        // 이동은 GOAP 행동이 Update(매 프레임)에서 부르는데, MovePosition은 다음 물리 갱신 때에야 반영된다.
        // 그 사이에 다시 부르면 아직 옮기기 전의 위치에서 계산해 앞의 이동이 덮어써지므로,
        // 예약해 둔 위치와 방향을 기억했다가 거기서 이어서 움직인다. 물리 갱신이 끝나면 비운다.
        // (이렇게 하지 않으면 프레임이 물리 갱신보다 적게 도는 상황(50fps 미만)에서 그만큼 느려진다)
        private Vector3? _pendingPosition;
        private Quaternion? _pendingRotation;

        // 예약해 둔 이동이 있으면 그 자리를, 없으면 지금 자리를 돌려준다.
        private Vector3 Position => _pendingPosition ?? _rigidbody.position;
        private Quaternion Rotation => _pendingRotation ?? _rigidbody.rotation;

        // FixedUpdate 바로 뒤의 물리 갱신에서 예약한 이동이 반영되므로, 여기서 예약을 비운다.
        private void FixedUpdate()
        {
            _pendingPosition = null;
            _pendingRotation = null;
        }

        private void MoveBody(Vector3 position)
        {
            _rigidbody.MovePosition(position);
            _pendingPosition = position;
        }

        private void TurnBody(Quaternion rotation)
        {
            _rigidbody.MoveRotation(rotation);
            _pendingRotation = rotation;
        }

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _pathfinder = FindObjectOfType<Pathfinder>();
            _terrain = Terrain.activeTerrain;

            // 평평한 맵에서는 높이가 흔들리지 않도록 Rigidbody의 Y 위치를 고정해 두었다.
            // 지형 위에서는 땅을 따라 오르내려야 하므로 그 고정을 푼다.
            // (고정된 채로는 처음 선 높이에 머물러서, 봉우리를 오를 때 땅속으로 들어간다)
            if (_terrain != null)
                _rigidbody.constraints &= ~RigidbodyConstraints.FreezePositionY;

            // 프리팹에 저장된 높이는 평평한 맵 기준이므로, 시작할 때 서 있는 자리의 땅 높이로 맞춘다.
            Vector3 start = transform.position;
            start.y = HeightAt(start);
            transform.position = start;
        }

        /// <summary>
        /// 플레이어의 위치와 도착 판정을 측정하는 반경을 받아
        /// 적과 플레이어의 거리가 반경 안에 들어오면 true를 return.
        /// </summary>
        /// <summary>
        /// 현재 경계 단계(alert)에 따라 이동 속도가 달라진다
        /// (Calm/Suspicious/Alerted 순으로 점점 빨라짐).
        /// </summary>
        public bool MoveTo(Vector3 target, float arriveRadius, AlertLevel alert)
        {
            // 도착을 확인하면 true를 return.
            Vector3 flatSelf = Flat(Position);
            if (Vector3.Distance(flatSelf, Flat(target)) <= arriveRadius)
            {
                StopHorizontal();
                return true;
            }

            // path가 비어있거나
            // path를 모두 건너왔거나
            // 플레이어가 유의미하게 움직였거나
            // 막힌 칸이 바뀌었다면 (덤불 고리가 열리면 그 안을 가로지르는 지름길이 생긴다)
            if (_path == null ||
                _waypointIndex >= _path.Count ||
                Vector3.Distance(_lastPathTarget, target) > repathThreshold ||
                _pathGridVersion != PathGrid.Version)
            {
                _path = _pathfinder != null ?
                _pathfinder.FindPath(Position, target, MyDimension) : null;
                _waypointIndex = 0;
                _lastPathTarget = target;
                _pathGridVersion = PathGrid.Version;
            }

            FollowPath(MoveSpeedFor(alert));
            return false;
        }

        /// <summary>
        /// 그 자리에 설 수 있는가(길찾기 격자에서 막히지 않은 칸인가). 격자가 없으면 true.
        /// </summary>
        public bool IsWalkable(Vector3 position) => _pathfinder == null || _pathfinder.IsWalkable(position, MyDimension);

        private float MoveSpeedFor(AlertLevel alert) => alert switch
        {
            AlertLevel.Calm => calmMoveSpeed,
            AlertLevel.Suspicious => suspiciousMoveSpeed,
            _ => alertedMoveSpeed,
        };

        /// <summary>
        /// 제자리에 멈춰 주변을 느린 속도로 돌아보는 함수
        /// </summary>
        public void LookAround()
        {
            StopHorizontal();
            TurnBody(Rotation * Quaternion.AngleAxis(turnSpeed * 12f * Time.deltaTime, Vector3.up));
        }

        /// <summary>
        /// 추격자 주변의 무작위 새로운 지점을 생성하여
        /// 추격자가 해당 지점으로 이동하도록 하는 함수.
        /// 배회할 지점이 없으면 새로 좌표를 정해서 return하고,
        /// 이미 있으면 있던 좌표를 return한다
        /// </summary>
        public Vector3 GetWanderTarget()
        {
            if (!_hasWanderTarget)
            {
                /// Random.insideUnitCircle은 반지름이 1인 원 안의
                /// 무작위 점을 뽑아내는 함수
                /// 거기에 wanderRadius를 곱했으니
                /// 반지름이 wanderRadius인 원 안의 무작위 점을 뽑아낸다.
                /// 10번을 시도하고 도착할 수 있는 지점이 없으면 wanderTarget을 생성하지 않는다.
                for (int i = 0; _pathfinder != null && i < 10; i++)
                {
                    Vector2 r = Random.insideUnitCircle * wanderRadius;
                    Vector3 candidate = _rigidbody.position + new Vector3(r.x, 0f, r.y);
                    if (_pathfinder.IsWalkable(candidate, MyDimension))
                    {
                        _wanderTarget = candidate;
                        _hasWanderTarget = true;
                        break;
                    }
                }
            }
            return _wanderTarget;
        }

        /// <summary>
        /// 이동을 마쳤을 경우 호출하여
        /// 새 wanderTarget을 받을 수 있도록 준비한다.
        /// </summary>
        public void ClearWanderTarget() => _hasWanderTarget = false;

        /// 배회할 곳이 정해져 있는가, 그리고 그곳. (무리의 졸개가 우두머리의 목적지를 읽는다)
        public bool HasWanderTarget => _hasWanderTarget;
        public Vector3 WanderTarget => _wanderTarget;

        /// <summary>
        /// 배회할 곳을 직접 정한다. 무리를 따라가거나 켜진 불로 몰려갈 때 쓴다.
        /// 정하지 않으면 GetWanderTarget이 지금 자리 둘레에서 무작위로 고른다.
        /// </summary>
        public void SetWanderTarget(Vector3 target)
        {
            _wanderTarget = target;
            _hasWanderTarget = true;
        }

        /// <summary>
        /// 적이 교착 상태에 빠지는 것을 막기 위한 방어 코드.
        /// 경로가 없는 노드가 목적지로 정해지는 경우를 탐지한다.
        /// </summary>
        public bool HasValidPath => _path != null && _path.Count > 0;

        /// <summary>
        /// MoveTo에서 최종 호출하는, 한 프레임씩 움직이는 작업을 담은 함수
        /// </summary>
        private void FollowPath(float speed)
        {
            if (_path == null || _waypointIndex >= _path.Count)
                return;

            Vector3 flatSelf = Flat(Position); // 내 위치의 평면 버전
            Vector3 wp = _path[_waypointIndex];

            // 웨이포인트 도달 체크
            if (Vector3.Distance(flatSelf, Flat(wp)) <= waypointTolerance)
            {
                _waypointIndex++;
                if (_waypointIndex >= _path.Count)
                    return;
                wp = _path[_waypointIndex];
            }

            Vector3 toWaypoint = Flat(wp) - flatSelf;
            Vector3 dir = toWaypoint.normalized; // 평면 상에서 방향 계산
            // 매 프레임 부르므로 프레임 시간만큼 움직인다. 프레임이 길어도 웨이포인트를 지나치지 않게 한다.
            float distance = Mathf.Min(speed * Time.deltaTime, toWaypoint.magnitude);
            Vector3 next = Position + dir * distance;      // 실제 위치 + 이동
            // 길찾기는 위에서 내려다본 평면(xz)에서만 이뤄지므로, 높이(y)는 여기서 따로 정한다.
            // 지형이 있으면 옮겨 갈 자리의 땅 높이에 맞추고, 없으면 지금 높이를 유지한다.
            next.y = HeightAt(next);
            MoveBody(next);

            // 유의미한 이동을 한다면
            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
                TurnBody(Quaternion.Slerp(Rotation, look, turnSpeed * Time.deltaTime));
            }
        }

        /// <summary>
        /// position의 xz 자리에 섰을 때 이 오브젝트가 있어야 할 높이(y).
        /// 지형이 없는 평평한 맵에서는 물리 작용으로 y가 미세하게 변하지 않도록 지금 높이를 그대로 돌려준다.
        /// </summary>
        private float HeightAt(Vector3 position)
        {
            if (_terrain == null)
                return transform.position.y;

            // 몸집이 큰 적은 한가운데의 높이에만 맞추면, 비탈에서 오르막 쪽 발이 땅속에 묻힌다.
            // 그래서 발이 닿는 범위의 둘레도 함께 재서, 그중 가장 높은 땅에 맞춘다.
            // (내리막 쪽 발은 조금 뜨지만, 묻히는 것보다 눈에 덜 띈다)
            float ground = _terrain.SampleHeight(position);
            if (footprintRadius > 0f)
            {
                for (int i = 0; i < FootprintSamples; i++)
                {
                    float angle = i * Mathf.PI * 2f / FootprintSamples;
                    Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * footprintRadius;
                    ground = Mathf.Max(ground, _terrain.SampleHeight(position + offset));
                }
            }

            // SampleHeight는 지형 오브젝트의 위치를 뺀 높이를 주므로 다시 더한다.
            return ground + _terrain.transform.position.y + heightAboveGround;
        }

        /// <summary>
        /// x와 z축의 속도를 0으로 만들어 맵에서 정지시키는 함수
        /// </summary>
        private void StopHorizontal()
        {
            Vector3 v = _rigidbody.velocity;
            _rigidbody.velocity = new Vector3(0f, v.y, 0f);
        }

        /// <summary>
        /// xyz 위치를 xz 평면 상의 위치로 치환하는 함수
        /// </summary>
        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
