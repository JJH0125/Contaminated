using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 게임에 쓰이는 모든 소리의 값을 한 곳에 모아 두는 에셋.
    ///
    /// 만드는 법: Project 창에서 Assets/Resources 폴더를 만들고, 그 안에서
    ///   우클릭 > Create > Squad > Sound Library
    ///   Resources.Load 함수가 이를 찾을 수 있도록 이름은 반드시 "SoundLibrary"로 둔다
    ///
    /// 에셋을 선택하면 인스펙터에서 소리별 반경 등을 조절할 수 있고,
    /// 플레이 중에 바꿔도 바로 반영된다.
    /// </summary>
    [CreateAssetMenu(menuName = "Squad/Sound Library", fileName = "SoundLibrary")]
    public class SoundLibrary : ScriptableObject
    {
        // Resources 폴더에서 asset 파일을 찾지 못했을 때 불러오는 기본값.
        [Header("○ 튜닝 값 — 자유롭게 조절")]
        [Tooltip("Walk 키를 누르고 천천히 걸을 때의 발소리")]
        [SerializeField] private Sound walking = new Sound("Walking", 4f, 0f, AlertLevel.Suspicious, false);
        [Tooltip("기본 이동(뛰기) 발소리")]
        [SerializeField] private Sound running = new Sound("Running", 13f, 0f, AlertLevel.Suspicious, false);
        [Tooltip("작동 중인 발전기 소리")]
        [SerializeField] private Sound generator = new Sound("Generator", 45f, 6f, AlertLevel.Alerted, true);
        [Tooltip("미끼 소리")]
        [SerializeField] private Sound decoy = new Sound("Decoy", 30f, 0f, AlertLevel.Suspicious, false);
        [Tooltip("플레이어가 차원을 넘어 도착한 자리에서 나는 소리. 도착한 차원의 적만 들어야 하므로 차원을 관통하지 않게 둔다")]
        [SerializeField] private Sound dimensionShift = new Sound("DimensionShift", 25f, 0f, AlertLevel.Suspicious, false);
        [Tooltip("꺼진 모닥불에 불이 붙는 소리. 차원을 넘어 퍼져서, 고리가 열린 불가에 두 차원의 적이 모여든다")]
        [SerializeField] private Sound fireLit = new Sound("FireLit", 35f, 0f, AlertLevel.Suspicious, true);

        public Sound Walking => walking;
        public Sound Running => running;
        public Sound Generator => generator;
        public Sound Decoy => decoy;
        public Sound DimensionShift => dimensionShift;
        public Sound FireLit => fireLit;
    }
}
