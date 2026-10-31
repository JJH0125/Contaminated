using System.Collections.Generic;

namespace Squad
{
    /// <summary>
    /// 함께 순찰하는 적의 무리. Real 적은 2~3마리씩 무리를 지어 다닌다. (차원별 값 packSize)
    ///
    /// 무리에서 살아 있는(켜져 있는) 첫 번째 적이 우두머리다. 우두머리는 혼자 다닐 때처럼 배회할 곳을 고르고,
    /// 나머지는 우두머리의 목적지 둘레로 저마다 다른 자리(몫)를 맡아 따라간다. (WanderStep)
    /// 무리는 평소에 다니는 모습에만 쓴다. 플레이어를 보거나 소리를 들으면 각자 판단한다.
    ///
    /// StageLayout이 라운드를 시작할 때 무리를 짜서 적마다 넘겨준다.
    /// </summary>
    public class ChaserPack
    {
        private readonly List<HorrorChaserAgent> _members = new();

        public void Add(HorrorChaserAgent member)
        {
            if (member != null && !_members.Contains(member))
                _members.Add(member);
        }

        /// 지금의 우두머리. 켜져 있는 첫 번째 적. 아무도 없으면 null.
        public HorrorChaserAgent Leader
        {
            get
            {
                foreach (HorrorChaserAgent member in _members)
                    if (member != null && member.isActiveAndEnabled)
                        return member;
                return null;
            }
        }

        /// 우두머리를 뺀 몇 번째 졸개인가 (1부터). 우두머리면 0, 무리에 없으면 -1.
        public int SlotOf(HorrorChaserAgent member)
        {
            int slot = 0;
            foreach (HorrorChaserAgent other in _members)
            {
                if (other == null || !other.isActiveAndEnabled)
                    continue;
                if (other == member)
                    return slot;
                slot++;
            }
            return -1;
        }
    }
}
