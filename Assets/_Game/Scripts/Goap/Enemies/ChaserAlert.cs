using UnityEngine;

namespace Squad
{
    // 적의 경계 상태 3가지 : Calm < Suspicious < Alerted
    public enum AlertLevel { Calm, Suspicious, Alerted }

    /// <summary>
    /// 적 각각의 경계 상태를 관리하는 컴포넌트
    /// </summary>
    public class ChaserAlert : MonoBehaviour
    {
        // 내려가는 시간은 GameTuning 에셋의 적 항목(차원별)에서 읽는다.
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
        private float AlertedToSuspiciousTime => Tuning.alertedToSuspiciousTime;
        private float SuspiciousToCalmTime => Tuning.suspiciousToCalmTime;

        // 현재 경계 상태 (기본값 : Calm)
        public AlertLevel Level { get; private set; } = AlertLevel.Calm;
        // 마지막으로 경계 상태가 업데이트된 이후로 경과한 시간
        private float TimeInAlertState = 0f;

        /// <summary>
        /// 경계 레벨 타이머를 작동시키고, 타이머에 따라 경계 레벨을 낮추는 역할을 한다.
        /// </summary>
        void Update()
        {
            TimeInAlertState += Time.deltaTime;

            /// 시간이 지남에 따라 경계 상태를 서서히 품
            /// Alerted → Suspicious
            if (Level == AlertLevel.Alerted && 
            TimeInAlertState > AlertedToSuspiciousTime)
                SetAlert(AlertLevel.Suspicious);
            /// Suspicious → Calm
            else if (Level == AlertLevel.Suspicious && 
            TimeInAlertState > SuspiciousToCalmTime)
                SetAlert(AlertLevel.Calm);
        }

        /// <summary>
        /// 모든 소리와 시야로 인한 경계 상태 상승은 이 함수를 통해서만 이루어진다.
        /// </summary>
        public void RaiseAlert(AlertLevel level)
        {
            // 현재 경계 상태보다 높은 경계 상태로만 올릴 수 있다.
            if (level > Level)
                SetAlert(level);
            // 현재 경계 상태와 같은 경계 상태로 올리면 TimeInAlertState만 초기화한다.
            else if (level == Level)
                TimeInAlertState = 0f;
            // 현재 경계 상태보다 낮은 경계 상태로 올리면 아무 것도 하지 않는다.
        }

        /// 현재 Level 상태를 바꾸고 TimeInAlertState를 초기화한다.
        /// RaiseAlert()의 동작을 보조하는 함수.
        private void SetAlert(AlertLevel level)
        {
            if (Level == level)
                return;
            Level = level;
            TimeInAlertState = 0f;
        }
    }
}