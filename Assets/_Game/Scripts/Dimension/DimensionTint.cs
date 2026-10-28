using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 두 차원에 모두 있는 것이 차원마다 다른 색으로 보이게 한다. 랜드마크가 Fake에서 그을린 모습이 되는 데 쓴다.
    /// 자리와 모양은 그대로 두고 색만 바꾸므로, Fake에서 8초 뒤 Real로 튕겨 나와도 같은 자리의 랜드마크로 길을 잃지 않는다.
    ///
    /// 재질은 바꾸지 않고 Renderer마다 색(_Color)만 따로 넣는다(MaterialPropertyBlock).
    ///
    /// 씬 구성:
    ///   붙일 필요가 없다. StageLayout이 랜드마크를 만들 때 붙인다.
    /// </summary>
    public class DimensionTint : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private Color _realColor = Color.white;
        private Color _fakeColor = Color.white;
        private Renderer[] _renderers;
        private MaterialPropertyBlock _block;
        private Dimension _shown = Dimension.None;

        /// <summary>Real과 Fake에서의 색을 정한다. 자식의 Renderer를 모두 칠한다. (Renderer를 다 붙인 뒤에 부른다)</summary>
        public void Setup(Color realColor, Color fakeColor)
        {
            _realColor = realColor;
            _fakeColor = fakeColor;
            _renderers = GetComponentsInChildren<Renderer>(true);
            _block = new MaterialPropertyBlock();
            _shown = Dimension.None;
        }

        private void Update()
        {
            if (_renderers == null)
                return;

            DimensionController controller = DimensionController.Instance;
            Dimension now = controller != null && controller.player != null ? controller.player.myDimension : Dimension.Real;
            if (now == _shown)
                return;

            _shown = now;
            Color color = now == Dimension.Fake ? _fakeColor : _realColor;
            foreach (Renderer part in _renderers)
            {
                if (part == null)
                    continue;
                part.GetPropertyBlock(_block);
                _block.SetColor(ColorId, color);
                part.SetPropertyBlock(_block);
            }
        }
    }
}
