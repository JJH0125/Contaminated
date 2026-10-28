using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 한 차원에 속한 것이 가지는, 차원이 담긴 일종의 이름표.
    /// 적뿐 아니라 차원마다 다른 장애물(덤불 고리, 오염 웅덩이)과 Fake에서만 보이는 뿌리에도 붙는다.
    ///
    /// 플레이어와 같은 차원일 때만 보인다. Dimension.None이면 두 차원 모두에 있는 것이라 늘 보인다.
    ///
    /// 보이고 숨길 Renderer:
    ///   적       인스펙터에서 모델의 SkinnedMeshRenderer 하나를 넣는다
    ///   그 밖의 것 코드로 만들 때 Setup을 부르면 자식의 Renderer를 모두 모아 함께 보이고 숨긴다
    /// </summary>
    public class DimensionMember : MonoBehaviour
    {
        [Header("■ 필수 연결 — 비워두면 에러")]
        [Tooltip("적의 Renderer. 뼈대가 있는 모델이면 자식의 SkinnedMeshRenderer를 넣는다")]
        [SerializeField] private Renderer meshRenderer;

        [Header("○ 설정")]
        [Tooltip("속한 차원. None이면 두 차원 모두에 있다")]
        [SerializeField] private Dimension dimension;
        public Dimension Dimension => dimension;

        // Setup으로 모은 Renderer들 (코드로 만든 것만)
        private Renderer[] _renderers;

        /// <summary>
        /// 코드로 만든 오브젝트에 이름표를 달 때 부른다. 차원을 정하고, 자식의 Renderer를 모두 모은다.
        /// (Renderer를 다 붙인 뒤에 불러야 한다)
        /// </summary>
        public void Setup(Dimension memberDimension)
        {
            dimension = memberDimension;
            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        void Start()
        {
            // 컨트롤러는 Start에서 한번만 참고하고, 이후에는 몰라도 된다.
            DimensionController dimensionController = DimensionController.Instance;
            if (dimensionController != null)
                dimensionController.AddMember(this);
        }

        void OnDestroy()
        {
            if (DimensionController.Instance != null)
                DimensionController.Instance.RemoveMember(this);
        }

        /// <summary>
        /// 컨트롤러가 명령하는 대로 보이고 숨는다.
        /// </summary>
        public void SetRenderer(bool isVisible)
        {
            if (meshRenderer != null)
                meshRenderer.enabled = isVisible;

            if (_renderers != null)
                foreach (Renderer part in _renderers)
                    if (part != null)
                        part.enabled = isVisible;
        }
    }
}
