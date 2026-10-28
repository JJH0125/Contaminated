using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 소리 한 종류의 데이터. 소리마다 실행 코드가 다르지 않고 값만 다르므로
    /// 상속 없이 값을 채워 쓴다(Goal과 같은 원리).
    ///
    /// [System.Serializable]이라 SoundLibrary 에셋의 인스펙터에서 값을 조절할 수 있다.
    /// </summary>
    [System.Serializable]
    public class Sound
    {
        [Tooltip("소리의 이름 (어떤 소리인지)")]
        [SerializeField] private string name;
        [Tooltip("소리가 들릴 반경(m)")]
        [SerializeField] private float radius;
        [Tooltip("소리의 지속시간(초)")]
        [SerializeField] private float duration;
        [Tooltip("소리를 들은 적이 취할 경계 단계")]
        [SerializeField] private AlertLevel alert;
        [Tooltip("소리가 차원을 초월하는지")]
        [SerializeField] private bool canCrossDimension;
        [Tooltip("소리가 날 때 재생할 효과음. 비워두면 적에게만 들리고 플레이어에게는 들리지 않는다.\n" +
                 "발전기처럼 계속 나는 소리는 비워두고, 그 오브젝트에 AudioSource를 붙여 반복 재생한다")]
        [SerializeField] private AudioClip clip;

        public string Name => name;
        public float Radius => radius;
        public float Duration => duration;
        public AlertLevel Alert => alert;
        public bool CanCrossDimension => canCrossDimension;
        public AudioClip Clip => clip;

        public Sound(string name, float radius, float duration,
        AlertLevel alert, bool canCrossDimension)
        {
            this.name = name;
            this.radius = radius;
            this.duration = duration;
            this.alert = alert;
            this.canCrossDimension = canCrossDimension;
        }
    }

    /// <summary>
    /// 소리를 쓰는 쪽(발소리, 발전기 등)이 SoundList.Walking의 형식으로 소리를 호출하는 게이트.
    /// 실제 값은 Resources/SoundLibrary 에셋에서 읽는다.
    /// 에셋이 없으면 경고를 한 번 띄우고 기본값으로 동작한다.
    /// </summary>
    public static class SoundList
    {
        private const string LibraryPath = "SoundLibrary";
        private static SoundLibrary _library;

        private static SoundLibrary Library
        {
            /// Library 함수가 실행되면 (즉 게임에 돌입하고 플레이어가 첫 소리를 내면) _library를 채운다.
            /// 즉 여기서 get 블록은 게임에 진입하기도 전에 소리 데이터를 가져오느라 로딩이 지연되는 현상을 방지하는 역할을 수행한다.
            /// 또한 set 블록이 아예 없기 때문에 소리 데이터에 다른 값을 덮어씌우는 것이 소용없다.
            get
            {
                if (_library == null)
                {
                    _library = Resources.Load<SoundLibrary>(LibraryPath);
                    /// asset 파일이 만들어져있지 않을 경우, 임시로 파일을 생성한다
                    if (_library == null)
                    {
                        Debug.LogWarning("[SoundList] Resources/SoundLibrary 에셋이 없어 기본값을 사용합니다. " +
                                         "Assets/_Game/Resources에서 Create > Squad > Sound Library로 만들어 주세요.");
                        /// SoundLibrary.cs에 적힌 기본값들을 불러온다
                        _library = ScriptableObject.CreateInstance<SoundLibrary>();
                    }
                }
                return _library;
            }
        }

        /// 외부에서 SoundList.Walking을 호출하면, 위의 Library 함수를 실행한다.
        public static Sound Walking => Library.Walking;
        public static Sound Running => Library.Running;
        public static Sound Generator => Library.Generator;
        public static Sound Decoy => Library.Decoy;
        public static Sound DimensionShift => Library.DimensionShift;
        public static Sound FireLit => Library.FireLit;
    }
}
