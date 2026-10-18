using UnityEngine;

namespace Squad
{
    public class Exit : MonoBehaviour
    {
        [Header("○ 튜닝 값 — 자유롭게 조절")]
        [Tooltip("상호작용할 수 있는 플레이어 대상 레이어")]
        [SerializeField] private LayerMask playerLayer;
        [Tooltip("범위 안에서 화면에 띄울 안내 문구")]
        [SerializeField] private string promptMessage = "아직 열리지 않았군요...";

        private bool IsActive;
        private bool _playerInRange;

        void Start()
        {
            // 먼저 닫아 둔 뒤 등록한다. 등록하면서 GameManager가 바로 열 수도 있기 때문이다.
            IsActive = false;
            if (GameManager.Instance != null)
                GameManager.Instance.AddExit(this);
        }
        
        void Update()
        {
            if (_playerInRange && IsActive)
                GameManager.Instance.RoundClear();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsInLayerMask(other.gameObject.layer, playerLayer))
                return;
            
            _playerInRange = true;
            if (!IsActive)
                ShowPrompt();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsInLayerMask(other.gameObject.layer, playerLayer))
                return;
            
            _playerInRange = false;
            if (!IsActive)
                HidePrompt();
        }

        // LayerMask는 비트로 레이어를 표시한다. 해당 레이어 비트가 켜져 있는지 확인.
        private static bool IsInLayerMask(int layer, LayerMask mask)
        {
            return (mask.value & (1 << layer)) != 0;
        }

        /// 탈출 조건을 채우면 GameManager가 호출한다. (지금은 등록하자마자 연다)
        public void Activate()
        {
            IsActive = true;
        }

        private void ShowPrompt()
        {            
            if (InteractionPrompt.Instance != null)
                InteractionPrompt.Instance.Show(this, promptMessage);
        }

        private void HidePrompt()
        {
            if (InteractionPrompt.Instance != null)
                InteractionPrompt.Instance.Hide(this);
        }
    }
}
