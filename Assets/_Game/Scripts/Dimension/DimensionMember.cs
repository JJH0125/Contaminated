using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 적이 각각 가지는, 차원이 담긴 일종의 이름표.
    /// </summary>
    public class DimensionMember : MonoBehaviour
    {
        [Header("■ 필수 연결 — 비워두면 에러")]
        [Tooltip("적의 Renderer. 뼈대가 있는 모델이면 자식의 SkinnedMeshRenderer를 넣는다")]
        [SerializeField] private Renderer meshRenderer;

        [Header("○ 튜닝 값 — 자유롭게 조절")]
        [Tooltip("적이 속한 차원")]
        [SerializeField] private Dimension dimension;
        public Dimension Dimension => dimension;

        void Start()
        {
            // 컨트롤러는 Start에서 한번만 참고하고, 이후에는 몰라도 된다.
            DimensionController dimensionController = DimensionController.Instance;
            if (dimensionController != null)
                dimensionController.AddEnemy(this);
        }

        /// <summary>
        /// 적은 컨트롤러가 명령하는 대로 보이고 숨는다.
        /// </summary>
        public void SetRenderer(bool isVisible)
        {
            if (meshRenderer != null)
            {
                if (isVisible)
                    meshRenderer.enabled = true;
                else
                    meshRenderer.enabled = false;
            }
        }
    }
}
