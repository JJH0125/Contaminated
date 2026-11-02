using System.Collections.Generic;
using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 적에게 끼웠다 뺄 수 있는 "목표 부품". 목표 하나와, 그 목표를 이루는 데 필요한 행동들을 한 컴포넌트에 담는다.
    ///
    /// 적의 두뇌(HorrorChaserAgent)는 켜져 있는 부품을 모두 모아, 원래 목표·행동과 함께 계획을 세운다.
    /// 그래서 새 목표를 넣을 때 적의 코드는 하나도 고치지 않는다. 컴포넌트를 켜면 다음 계획부터 그 목표를 추구하고,
    /// 끄면 원래 목표로 돌아간다. 행동의 순서도 적지 않는다. 행동마다 조건과 효과만 적으면 플래너가 이어 붙인다.
    ///
    /// 부품이 하는 일 세 가지:
    ///   Goal         목표 (이름, 우선순위, 이뤘을 때의 상태)
    ///   Actions      목표에 필요한 행동들 (조건, 효과, 실제 실행)
    ///   WriteFacts   세계에서 읽어 온 사실을 계획용 상태에 적는다. 이게 없으면 플래너가 계획을 세울 수 없다
    ///   IsRelevant   지금 이 목표를 고를 이유가 있는가 (예: 켜진 불이 하나도 없으면 끌 이유가 없다)
    /// </summary>
    public abstract class GoapModule : MonoBehaviour
    {
        public abstract Goal Goal { get; }
        public abstract IReadOnlyList<GoapAction> Actions { get; }

        /// 세계에서 읽어 온 사실을 계획용 상태에 적는다. 계획을 세울 때마다 불린다.
        public virtual void WriteFacts(WorldState state, ChaserContext ctx) { }

        /// 지금 이 목표를 고를 이유가 있는가.
        public virtual bool IsRelevant(WorldState state, ChaserContext ctx) => true;

        // 켜고 끄는 즉시 두뇌가 다시 계획을 세우게 한다.
        protected virtual void OnEnable() => Notify();
        protected virtual void OnDisable() => Notify();

        protected void Notify()
        {
            HorrorChaserAgent agent = GetComponent<HorrorChaserAgent>();
            if (agent != null)
                agent.RefreshModules();
        }
    }
}
