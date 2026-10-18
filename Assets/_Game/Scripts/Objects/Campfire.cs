using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 모닥불. 꺼지지 않는 불빛으로 길잡이가 되는 동시에, 그 옆에 선 플레이어를 적에게 드러내는 위험이기도 하다.
    ///
    /// 불빛과 "불빛에 드러난 자리인가" 판단은 같은 오브젝트의 FireSource가 맡는다.
    /// 이 스크립트는 StageLayout이 모닥불을 찾아 개수를 맞추고 배치하는 표식 역할만 한다.
    /// (예전에는 여기서 횃불을 집었지만, 이제 주인공이 처음부터 횃불을 들고 있어서 그 기능은 없앴다)
    ///
    /// 씬 구성:
    ///   같은 오브젝트에 FireSource를 붙이고, 모닥불 모델은 자식으로 둔다.
    /// </summary>
    public class Campfire : MonoBehaviour
    {
    }
}
