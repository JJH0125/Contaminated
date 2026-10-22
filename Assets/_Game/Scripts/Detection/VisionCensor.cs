using UnityEngine;
using System.Collections.Generic;

namespace Squad
{
    public class VisionCensor : MonoBehaviour
    {
        /// 시야 감지에 필요한 조건
        /// 1. 플레이어가 감지 거리 안에 있는가?
        /// 2. 플레이어가 적의 시야각 안에 있는가?
        /// 3. 적과 플레이어 사이에 벽이나 장애물이 없는가?
        /// 4. 플레이어가 죽은 상태는 아닌가?
        /// 4가지 조건을 모두 만족해야 적이 플레이어를 "봤다"고 판단
        /// 
        [Header("■ 필수 연결 — 비워두면 에러")]
        [Tooltip("플레이어")]
        [SerializeField] private Transform player;
        [Tooltip("감지할 대상 레이어")]
        [SerializeField] private LayerMask targetLayer;
        [Tooltip("장애물로 인식할 대상 레이어")]
        [SerializeField] private LayerMask obstacleLayer;

        // 수치는 GameTuning 에셋의 적 항목(Real 적 / Fake 적)에서 이 적의 차원에 맞는 것을 읽는다.
        private EnemyTuning Tuning => GameTuning.Instance.EnemyFor(enemy);
        private float viewDistance => Tuning.viewDistance;
        private float viewAngle => Tuning.viewAngle;
        private float eyeHeight => Tuning.eyeHeight;
        private float playerHeight => Tuning.playerHeight;
        private float detectInterval => Tuning.detectInterval;

        // 불빛 — 불(모닥불, 횃불) 근처의 플레이어는 더 멀리서도 보인다
        private float litViewMultiplierFirstRound => Tuning.litViewMultiplierFirstRound;
        private float litViewMultiplierLastRound => Tuning.litViewMultiplierLastRound;

        [Header("○ 발견 소리 — 플레이어를 못 보다가 막 발견한 순간에 낸다")]
        [Tooltip("발견했을 때 재생할 소리. 거리와 상관없이 항상 같은 크기로 들린다. 비워두면 소리를 내지 않는다")]
        [SerializeField] private AudioClip spottedSound;
        private float spottedSoundCooldown => Tuning.spottedSoundCooldown;

        private float _detectTimer;
        private DimensionController dimensionController;
        private DimensionMember enemy;

        // 플레이어의 이동 담당(이동 속도를 읽는다)과, 이 적의 두뇌(포효를 시킨다)
        private Player _playerMover;
        private HorrorChaserAgent _agent;

        void Start()
        {
            dimensionController = DimensionController.Instance;
            enemy = GetComponent<DimensionMember>();
            _agent = GetComponent<HorrorChaserAgent>();
            if (player != null)
                _playerMover = player.GetComponent<Player>();
        }

        void Update()
        {
            if (player == null)
                return;
            
            _detectTimer -= Time.deltaTime;

            if (_detectTimer <= 0)
            {
                DetectVision(player);
                _detectTimer = detectInterval;
            }
        }

        /// DetectVision을 한번 수행한 후, 해당 결과를 다음 수행까지 기억하는 역할
        /// 플레이어가 보이다가 막 사라졌을 때를 감지하기 위해 사용
        private bool visibleJustBefore;

        // 1. 플레이어가 같은 차원에 있는가? (Transform으로 알 수는 없고, DimensionMember를 통해 확인)
        public bool IsInSameDimension()
        {
            // 같은 차원이어도, 플레이어가 막 넘어온 직후의 유예 시간 동안에는 감지하지 못한다.
            if (dimensionController != null && enemy != null)
                return dimensionController.CanDetectPlayer(enemy);

            return false;
        }

        // 2. 플레이어가 감지 거리 안에 있는가?
        private bool IsInViewDistance(Transform target)
        {
            Vector3 flatSelf = transform.position;
            Vector3 flatTarget = target.position;
            flatSelf.y = flatTarget.y = 0f;

            return Vector3.Distance(flatSelf, flatTarget) <= ViewDistanceFor(target);
        }

        /// <summary>
        /// target을 볼 수 있는 거리. 평소에는 viewDistance이고, target이 불빛 안에 있으면 더 멀어진다.
        /// 얼마나 멀어지는지는 라운드에 따라 정해진다. 라운드가 오를수록 적의 눈이 밝아져서,
        /// 초반에는 안전하던 불 옆이 후반에는 위험해진다.
        /// </summary>
        private float ViewDistanceFor(Transform target)
        {
            if (!FireSource.IsLit(target.position))
                return viewDistance;

            // 첫 라운드를 0, 마지막 라운드를 1로 본 진행도
            float progress = 0f;
            GameManager manager = GameManager.Instance;
            if (manager != null && manager.TotalRounds > 1)
                progress = (manager.CurrentRound - 1f) / (manager.TotalRounds - 1f);

            return viewDistance * Mathf.Lerp(litViewMultiplierFirstRound, litViewMultiplierLastRound, progress);
        }

        // 3. 플레이어가 적의 시야각 안에 있는가?
        private bool IsInViewAngle(Transform target)
        {
            // 적 위치 기준 플레이어의 위치를 나타내는 벡터
            Vector3 directionToTarget = target.position - transform.position;

            // 높이 차이가 무시할 만한 경우
            directionToTarget.y = 0f;

            if (directionToTarget.sqrMagnitude < 0.001f)
                return true;

            directionToTarget.Normalize();
            
            float angle = Vector3.Angle(transform.forward, directionToTarget);

            return angle <= viewAngle * 0.5f;
        }

        // 4. 적과 플레이어 사이에 벽이나 장애물이 없는가?
        private bool HasLineOfSight(Transform target)
        {
            Vector3 eyePosition = transform.position + Vector3.up * eyeHeight;
            Vector3 targetPoint = target.position + Vector3.up * playerHeight;

            /// 주의 : 플레이어가 벽 너머에서 점프하여 보이게 되면 적이 인식할 수 있는 상태
            /// 인식하지 못하도록 하고 싶다면
            /// 
            /// // 플레이어의 실제 y 대신, 지면 기준 높이를 쓰기
            /// Vector3 flatTarget = target.position;
            /// flatTarget.y = transform.position.y;   // 추격자와 같은 지면 높이로
            /// Vector3 targetPoint = flatTarget + Vector3.up * bodyHeight;
            /// 
            /// targetPoint 산출 코드를 위와 같이 변경하면 됨

            Vector3 direction = targetPoint - eyePosition;
            float distance = direction.magnitude;

            if (distance <= 0.01f)
                return true;

            if (Physics.Raycast(
                eyePosition,
                direction.normalized,
                distance,
                obstacleLayer,
                QueryTriggerInteraction.Ignore
            ))
                return false;

            return true;
        }

        // 1 ~ 4 조건 검사를 한 흐름으로
        private void DetectVision(Transform target)
        {
            bool canSee = IsInSameDimension()
            && IsInViewDistance(target)
            && IsInViewAngle(target)
            && HasLineOfSight(target);

            // 못 보다가 막 본 순간에만 발견 소리를 낸다. (보고 있는 동안 계속 울리지 않도록)
            if (canSee && !visibleJustBefore)
                PlaySpottedSound();

            if (canSee)
                // 플레이어가 시야에 있으면, 매번 호출하여 그 위치를 업데이트하고 경계 상태를 Alerted로 격상
                {
                Vector3 velocity = _playerMover != null ? _playerMover.HorizontalVelocity : Vector3.zero;
                bool newSpot = SquadBlackboard.Instance.ReportSighting(enemy, target.position, velocity);
                gameObject.GetComponent<ChaserAlert>().RaiseAlert(AlertLevel.Alerted);

                // 아무도 보지 않던 플레이어를 이 적이 새로 봤다면, 주변의 같은 차원 적에게 발견 신호(포효)를 보낸다.
                if (newSpot && _agent != null)
                    _agent.Roar();
                }
            else

                if (visibleJustBefore)
                    // 플레이어가 시야에서 사라지면, 단 한번만 호출하여 위치 제거
                    SquadBlackboard.Instance.ReportLostSight(enemy);

            // 이번 실행의 결과를 기억
            visibleJustBefore = canSee;
        }

        // 마지막으로 발견 소리가 난 시각. 적이 여럿이어도 소리가 겹치지 않도록 모든 적이 함께 쓴다.
        private static float _lastSpottedSoundTime = float.NegativeInfinity;

        /// <summary>
        /// 발견 소리를 낸다. 플레이어에게 "들켰다"는 것을 알리는 소리이므로, 적이 멀리 있어도 똑같이 들려야 한다.
        /// 그래서 위치가 있는 3D 소리가 아니라 2D 소리(PlayUI)로 재생한다.
        ///
        /// 플레이어가 나무 사이를 지나가면 "보임 → 안 보임 → 보임"이 짧은 사이에 되풀이될 수 있다.
        /// 그때마다 소리가 나지 않도록, 한 번 난 뒤에는 일정 시간 동안 다시 내지 않는다.
        /// </summary>
        private void PlaySpottedSound()
        {
            if (spottedSound == null || SfxPlayer.Instance == null)
                return;
            if (Time.time - _lastSpottedSoundTime < spottedSoundCooldown)
                return;

            _lastSpottedSoundTime = Time.time;
            SfxPlayer.Instance.PlayUI(spottedSound);
        }

        private void OnDrawGizmosSelected()
        {
            // 시야 거리 (원)
            Gizmos.color = Color.gray;
            Gizmos.DrawWireSphere(transform.position, viewDistance);

            // 시야각 경계 (두 직선)
            Vector3 left = Quaternion.Euler(0, -viewAngle * 0.5f, 0) * transform.forward;
            Vector3 right = Quaternion.Euler(0, viewAngle * 0.5f, 0) * transform.forward;
            Gizmos.DrawLine(transform.position, transform.position + left * viewDistance);
            Gizmos.DrawLine(transform.position, transform.position + right * viewDistance);

            // 플레이어까지의 레이 — 보이면 초록, 안 보이면 빨강
            if (Application.isPlaying && player != null)
            {
                Vector3 eye = transform.position + Vector3.up * eyeHeight;
                Vector3 target = player.position + Vector3.up * playerHeight;
                Gizmos.color = visibleJustBefore ? Color.green : Color.red;
                Gizmos.DrawLine(eye, target);
            }
        }
    }
}
