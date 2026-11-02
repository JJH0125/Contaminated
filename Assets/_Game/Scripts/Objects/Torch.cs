using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 플레이어가 손에 든 횃불의 표식. 횃불은 처음부터 끝까지 손을 떠나지 않는다.
    ///   들기와 숨기기, 손에 붙이기는 TorchHolder가 맡는다
    ///   불빛과 "불빛에 드러난 자리인가" 판단은 같은 오브젝트의 FireSource가 맡는다
    /// (예전에는 던져서 미끼로 쓸 수 있었지만, 미끼는 돌(Stone)로 바뀌어 던지는 코드는 지웠다)
    ///
    /// 씬 구성:
    ///   횃불 프리팹의 가장 위 오브젝트에 FireSource와 함께 붙인다. 횃불 모델은 자식으로 둔다.
    /// </summary>
    public class Torch : MonoBehaviour
    {
    }
}
