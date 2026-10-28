using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 게임에서 조절할 수 있는 수치를 한 곳에 모아 두는 에셋. (소리의 값은 따로 SoundLibrary에 있다)
    ///
    /// 에셋 위치: Assets/_Game/Resources/GameTuning
    ///   Resources.Load 함수가 이를 찾을 수 있도록 이름은 반드시 "GameTuning"으로 둔다.
    ///   지워졌으면 Resources 폴더에서 우클릭 > Create > Squad > Game Tuning으로 다시 만든다.
    ///   에셋이 없어도 게임은 이 파일에 적힌 기본값으로 돌아간다.
    ///
    /// 에셋을 선택하면 인스펙터에서 항목별로 펼쳐 조절할 수 있다.
    /// 대부분의 값은 쓰는 순간마다 읽으므로 플레이 중에 바꿔도 바로 반영된다.
    /// 시작할 때 한 번만 읽는 값(효과를 만들 때 정하는 값, 적의 수 등)은 다음 라운드부터 반영된다.
    ///
    /// 적은 차원마다 값을 따로 둔다(Real 적 / Fake 적). 같은 차원의 적은 모두 같은 값을 쓴다.
    ///
    /// 여기 없는 것: 오브젝트·재질·소리 파일 같은 연결, 화면 문구, 레이어, 키, UI 배치, 에디터 도구와 디버그용 값.
    /// 이런 것은 지금처럼 각 컴포넌트의 인스펙터에 남아 있다.
    /// </summary>
    [CreateAssetMenu(menuName = "Squad/Game Tuning", fileName = "GameTuning")]
    public class GameTuning : ScriptableObject
    {
        private const string AssetPath = "GameTuning";
        private static GameTuning _instance;

        /// <summary>
        /// Resources/GameTuning 에셋. 처음 쓸 때 한 번 불러오고, 없으면 경고를 한 번 띄우고 기본값으로 만든다.
        /// </summary>
        public static GameTuning Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<GameTuning>(AssetPath);
                    if (_instance == null)
                    {
                        Debug.LogWarning("[GameTuning] Resources/GameTuning 에셋이 없어 기본값을 사용합니다. " +
                                         "Assets/_Game/Resources에서 Create > Squad > Game Tuning으로 만들어 주세요.");
                        _instance = CreateInstance<GameTuning>();
                    }
                }
                return _instance;
            }
        }

        [Tooltip("이동, 발소리")]
        public PlayerTuning player = new();
        [Tooltip("횃불을 쥐는 자리, 불빛, 숨기기와 온기 게이지")]
        public TorchTuning torch = new();
        [Tooltip("돌 줍기와 던지기, 날아가는 돌, 맵에 놓는 돌")]
        public StoneTuning stone = new();
        [Tooltip("플레이어의 숨소리")]
        public BreathingTuning breathing = new();
        [Tooltip("라운드에 따라 보호복이 오염되는 정도")]
        public SuitTuning suit = new();
        [Tooltip("플레이어를 따라가는 카메라")]
        public CameraTuning camera = new();
        [Tooltip("차원 이동의 대기·머무는 시간")]
        public DimensionTuning dimension = new();

        [Tooltip("Real 차원 적의 판단, 이동, 경계, 시야. Real 적은 무리 지어 순찰한다")]
        public EnemyTuning realEnemy = new() { roarRadius = 200f, packSize = 3, fireAttraction = 0f };
        [Tooltip("Fake 차원 적의 판단, 이동, 경계, 시야. Fake 적은 각자 다니다가 켜진 불로 몰려간다")]
        public EnemyTuning fakeEnemy = new() { roarRadius = 40f, packSize = 1, fireAttraction = 0.7f };
        [Tooltip("적 모델에 맞춘 값: 걷는 애니메이션, 잡힐 때의 연출")]
        public EnemyBodyTuning enemyBody = new();
        [Tooltip("발견 신호를 받은 적들이 나눠 맡는 수색 지점")]
        public SearchTuning search = new();

        [Tooltip("라운드 수, 라운드별 구성, 출구, 스테이지 배치")]
        public StageTuning stage = new();
        [Tooltip("구역 벽, 봉인된 모닥불, 오염 웅덩이, 뿌리")]
        public MapTuning map = new();

        [Tooltip("Fake 차원에 있는 동안의 화면 일렁임과 색")]
        public FakeScreenTuning fakeScreen = new();
        [Tooltip("Fake 차원에 들어갈 때 퍼지는 오염과, 맵 곳곳에 드러나는 오염의 흔적")]
        public FakeContaminationTuning fakeContamination = new();
        [Tooltip("횃불을 숨기는 동안 화면 가장자리에 끼는 김서림과 오염")]
        public ContaminationEdgeTuning contaminationEdge = new();
        [Tooltip("같은 차원의 적이 가까울 때의 화면 노이즈")]
        public CctvTuning cctv = new();
        [Tooltip("플레이어를 가리는 나무와 풀에 뚫는 창")]
        public FoliageTuning foliage = new();
        [Tooltip("맵 가장자리의 일렁이는 벽")]
        public BoundaryTuning boundary = new();

        [Tooltip("배경음과 효과음")]
        public AudioTuning audio = new();

        /// <summary>그 차원의 적이 쓰는 값. 차원을 모르면 Real 값을 쓴다.</summary>
        public EnemyTuning EnemyFor(Dimension dimension) => dimension == Dimension.Fake ? fakeEnemy : realEnemy;

        /// <summary>그 적이 쓰는 값. 이름표가 없으면 Real 값을 쓴다.</summary>
        public EnemyTuning EnemyFor(DimensionMember member) => EnemyFor(member != null ? member.Dimension : Dimension.Real);
    }

    [System.Serializable]
    public class PlayerTuning
    {
        [Tooltip("달리는 속도(m/s). 횃불을 숨긴 채 Shift를 누르고 있을 때")]
        public float moveSpeed = 15f;
        [Tooltip("걷는 속도(m/s). 기본 이동")]
        public float walkSpeed = 7.5f;
        [Tooltip("중력 가속도(음수)")]
        public float gravity = -20f;
        [Tooltip("걷는 중 발소리 간격(초). 느리게 걸으니 더 길게")]
        public float walkStepInterval = 0.5f;
        [Tooltip("뛰는 중 발소리 간격(초). 더 짧게 = 더 자주")]
        public float runStepInterval = 0.3f;
    }

    [System.Serializable]
    public class TorchTuning
    {
        [Header("손에 쥐는 자리")]
        [Tooltip("손뼈를 기준으로 한, 주먹 안쪽(횃불을 쥐는 자리)의 위치(m, 모델 Scale 1 기준). 손뼈의 축 방향 기준이다")]
        public Vector3 handOffset = new Vector3(0f, 0.07f, 0.04f);
        [Tooltip("손뼈를 기준으로 한 횃불의 각도. Z가 90이면 횃불의 불꽃 쪽이 엄지 방향을 향한다")]
        public Vector3 handRotation = new Vector3(0f, 0f, 120f);
        [Tooltip("횃불에서 손으로 쥐는 지점. 횃불 프리팹의 모델 기준이다")]
        public Vector3 gripPoint = new Vector3(0f, -0.12f, 0f);
        [Tooltip("손뼈를 찾지 못했을 때 횃불을 드는 위치. 플레이어 오브젝트 기준 (오른쪽, 위, 앞)")]
        public Vector3 holdPosition = new Vector3(0.65f, 0.2f, 0.35f);
        [Tooltip("손뼈를 찾지 못했을 때 횃불을 드는 각도. 플레이어 오브젝트 기준")]
        public Vector3 holdRotation = new Vector3(10f, 0f, -25f);

        [Header("손에 든 횃불의 불빛")]
        [Tooltip("불빛의 중심을 플레이어 앞으로 얼마나 내어 둘지(m). 빛은 둥글게 퍼지지만 밝은 곳이 진행 방향으로 쏠린다")]
        public float heldLightForward = 6f;
        [Tooltip("불빛의 높이(m). 플레이어 오브젝트 위치(발에서 1m 위) 기준")]
        public float heldLightHeight = 3f;
        [Tooltip("손에 들고 있는 동안 불빛이 닿는 거리(m)")]
        public float heldLightRange = 22f;

        [Header("숨기기와 온기 게이지")]
        [Tooltip("가득 찬 온기 게이지가 숨기고 걷는 동안 모두 닳는 데 걸리는 시간(초)")]
        public float warmthDrainTime = 10f;
        [Tooltip("숨긴 채 달리면 게이지가 걸을 때의 몇 배 빠르기로 닳는가")]
        public float runDrainMultiplier = 1.6f;
        [Tooltip("빈 온기 게이지가 횃불을 들고 있는 동안 모두 차는 데 걸리는 시간(초)")]
        public float warmthRecoverTime = 5f;
        [Tooltip("넣고 꺼내는 동작의 재생 속도 배율. 2면 원래 애니메이션의 2배 빠르기다")]
        public float hideAnimationSpeed = 2f;
        [Tooltip("넣는 동작에서 손이 주머니에 닿는 순간(초, 원래 애니메이션 기준). 넣을 때는 이 지점을 지나면 횃불이 사라지고, " +
                 "꺼낼 때는 이 지점을 지나면 나타난다")]
        public float torchVanishDelay = 0.4f;
    }

    [System.Serializable]
    public class StoneTuning
    {
        [Header("줍기와 던지기")]
        [Tooltip("이 거리(m, 평면) 안에 있는 돌을 주울 수 있다")]
        public float pickupRadius = 4f;
        [Tooltip("왼손뼈를 기준으로 한 돌의 위치(m, 모델 Scale 1 기준). Mixamo 뼈대에서는 Y가 손가락 쪽, Z가 손바닥이 향하는 쪽이다")]
        public Vector3 handOffset = new Vector3(0f, 0.08f, 0.05f);
        [Tooltip("던질 때 앞으로 나가는 속도(m/s)")]
        public float throwSpeed = 40f;
        [Tooltip("던질 때 위로 솟는 속도(m/s). 클수록 높이, 멀리 날아간다")]
        public float throwUpSpeed = 20f;

        [Header("날아가는 돌")]
        [Tooltip("던져진 돌에 작용하는 중력 가속도(음수)")]
        public float gravity = -80f;
        [Tooltip("튕기는 정도. 0이면 튕기지 않는다")]
        [Range(0f, 1f)]
        public float bounciness = 0.35f;
        [Tooltip("땅에서 튕길 수 있는 최대 횟수")]
        public int maxBounces = 2;
        [Tooltip("날아가는 동안 도는 빠르기(도/초)")]
        public float spinSpeed = 720f;
        [Tooltip("어둠 속에서도 돌이 보이도록 두르는 테두리의 색")]
        public Color outlineColor = Color.white;
        [Tooltip("테두리의 굵기(화면 픽셀). 멀리 있어도 같은 굵기로 보인다. 0이면 테두리를 그리지 않는다")]
        public float outlineWidth = 3f;

        [Header("맵에 놓는 돌")]
        [Tooltip("라운드마다 맵 곳곳에 놓을 돌의 개수")]
        public int count = 20;
        [Tooltip("돌의 크기(m). 모델의 가장 긴 변이 이 길이가 되도록 맞춘다")]
        public float size = 1f;
        [Tooltip("돌끼리 적어도 이만큼(m)은 떨어지게 한다")]
        public float spacing = 12f;
    }

    [System.Serializable]
    public class BreathingTuning
    {
        [Header("크기")]
        [Tooltip("숨소리 전체의 크기. 효과음 볼륨에 곱해진다. 다른 효과음보다 작게 둔다")]
        [Range(0f, 1f)] public float volume = 0.6f;
        [Tooltip("평소의 크기 (전체 크기에 대한 비율). 오래 들어도 귀가 지치지 않게 작게 둔다")]
        [Range(0f, 1f)] public float calmVolume = 0.35f;
        [Tooltip("적이 아주 가까울 때의 크기")]
        [Range(0f, 1f)] public float nearVolume = 0.7f;
        [Tooltip("쫓기는 중의 크기")]
        [Range(0f, 1f)] public float chasedVolume = 1f;

        [Header("숨 사이의 간격(초)")]
        public float calmGap = 1.75f;
        public float nearGap = 0.5f;
        public float chasedGap = 0.05f;

        [Header("음높이 (1이 원래 소리, 1.15를 넘기지 않는다)")]
        [Range(0.9f, 1.15f)] public float calmPitch = 1f;
        [Range(0.9f, 1.15f)] public float nearPitch = 1.05f;
        [Range(0.9f, 1.15f)] public float chasedPitch = 1.12f;
        [Tooltip("마지막 라운드에서 음높이를 이만큼 낮춘다. 라운드가 오를수록 고르게 낮아진다")]
        [Range(0f, 0.1f)] public float lastRoundPitchDrop = 0.04f;

        [Header("매번 흔드는 정도")]
        [Tooltip("음높이를 ± 이 비율만큼 흔든다")]
        public float pitchJitter = 0.03f;
        [Tooltip("크기를 ± 이 비율만큼 흔든다")]
        public float volumeJitter = 0.1f;
        [Tooltip("간격을 ± 이 시간(초)만큼 흔든다")]
        public float gapJitter = 0.2f;
    }

    [System.Serializable]
    public class SuitTuning
    {
        [Tooltip("첫 라운드의 오염 정도 (0: 깨끗함, 1: 완전히 오염)")]
        [Range(0f, 1f)] public float firstRoundContamination = 0f;
        [Tooltip("마지막 라운드의 오염 정도")]
        [Range(0f, 1f)] public float lastRoundContamination = 0.85f;
    }

    [System.Serializable]
    public class CameraTuning
    {
        [Tooltip("내려다보는 각도(도). 클수록 위에서 수직으로 내려다본다")]
        [Range(10f, 90f)] public float pitch = 35.4f;
        [Tooltip("바라보는 방향(도). 45면 대각선 방향에서 비춘다")]
        public float yaw = 45f;
        [Tooltip("플레이어와 카메라 사이의 거리(m)")]
        public float distance = 22f;
        [Tooltip("가는 방향으로 카메라를 얼마나 앞서 보낼지(초). 플레이어가 이 시간 뒤에 있을 자리를 비춘다. 0이면 밀지 않는다")]
        public float lookAheadTime = 0.25f;
        [Tooltip("카메라를 앞으로 미는 거리의 최댓값(m)")]
        public float maxLookAhead = 4f;
        [Tooltip("미는 정도가 바뀔 때 따라가는 데 걸리는 시간(초). 클수록 느긋하게 움직인다")]
        public float lookAheadSmoothTime = 0.5f;
    }

    [System.Serializable]
    public class DimensionTuning
    {
        [Tooltip("한 번 이동한 뒤 다시 이동할 수 있을 때까지의 시간(초). Fake로 넘어간 순간부터 센다. " +
                 "Fake에 머무는 시간보다 길게 두면, Fake에서 스스로 돌아올 수는 없고 튕겨 나온 뒤 남은 시간을 더 기다려야 한다")]
        public float cooldown = 15f;
        [Tooltip("Fake 차원에 머물 수 있는 시간(초). 지나면 자동으로 Real로 튕겨 나온다")]
        public float fakeStayTime = 8f;
        [Tooltip("이동한 직후 적에게 감지되지 않고 잡히지도 않는 시간(초)")]
        public float undetectableTime = 1f;
    }

    [System.Serializable]
    public class EnemyTuning
    {
        [Header("판단 (GOAP)")]
        [Tooltip("플레이어를 게임오버 시키는 거리(m)")]
        public float catchRadius = 3f;
        [Tooltip("소리/배회 목표에 접근하여 행동을 시작하는 거리(m)")]
        public float arriveRadius = 0.6f;
        [Tooltip("계획을 다시 세우는 주기(초)")]
        public float replanInterval = 0.4f;
        [Tooltip("다음 배회까지 대기 시간(초, 최소)")]
        public float wanderPauseMin = 1.5f;
        [Tooltip("다음 배회까지 대기 시간(초, 최대)")]
        public float wanderPauseMax = 4f;

        [Header("발견 신호 (포효)")]
        [Tooltip("플레이어를 새로 발견한 적이 이 거리(m, 평면) 안의 같은 차원 적에게 신호를 보낸다")]
        public float roarRadius = 40f;
        [Tooltip("신호를 받고 합류한 적이 이 시간(초) 동안 아무도 플레이어를 보지 못하면 합류를 풀고 돌아간다")]
        public float joinTimeout = 30f;

        [Header("추격 겹침 (여러 적이 함께 볼 때)")]
        [Tooltip("플레이어와 가장 가까운 적만 곧장 쫓고, 나머지는 이 시간(초) 뒤에 플레이어가 있을 위치로 앞질러 간다")]
        public float interceptLeadTime = 1.5f;
        [Tooltip("앞질러 가는 적끼리 목적지를 좌우로 벌리는 간격(m). 서로 다른 쪽에서 다가오게 한다")]
        public float interceptSpacing = 6f;
        [Tooltip("플레이어가 이 거리(m) 안에 있으면 앞질러 가는 적도 곧장 달려든다")]
        public float interceptDirectRadius = 10f;

        [Header("배회 (평소에 다니는 모습)")]
        [Tooltip("몇 마리씩 무리 지어 다니는가. 1이면 각자 다닌다. 무리의 첫 번째가 목적지를 정하고 나머지는 그 둘레로 따라간다")]
        [Range(1, 6)] public int packSize = 1;
        [Tooltip("무리가 따라갈 때 우두머리의 목적지에서 벌어지는 거리(m)")]
        public float packSpread = 7f;
        [Tooltip("켜진 모닥불이 있을 때, 배회할 곳을 그 불 둘레로 고를 확률 (0~1). 0이면 불에 끌리지 않는다")]
        [Range(0f, 1f)] public float fireAttraction = 0f;
        [Tooltip("불 둘레로 갈 때 불에서 이 거리(m) 안의 자리를 고른다")]
        public float fireGatherRadius = 14f;

        [Header("이동")]
        [Tooltip("Calm 상태일 때 이동 속도(m/s)")]
        public float calmMoveSpeed = 5f;
        [Tooltip("Suspicious 상태일 때 이동 속도(m/s)")]
        public float suspiciousMoveSpeed = 9f;
        [Tooltip("Alerted 상태일 때 이동 속도(m/s)")]
        public float alertedMoveSpeed = 13f;
        [Tooltip("몸을 돌리는 빠르기")]
        public float turnSpeed = 8f;
        [Tooltip("경로의 다음 지점에 이만큼(m) 가까워지면 그다음 지점으로 넘어간다")]
        public float waypointTolerance = 0.25f;
        [Tooltip("목적지가 이만큼(m) 넘게 움직이면 경로를 다시 찾는다")]
        public float repathThreshold = 3f;
        [Tooltip("배회할 때 지금 자리에서 이 반경(m) 안의 지점을 고른다")]
        public float wanderRadius = 30f;
        [Tooltip("지형 위에서 이 오브젝트의 위치가 땅보다 얼마나 위에 있을지(m). 모델의 발이 땅에 닿도록 맞춘다")]
        public float heightAboveGround = 1f;
        [Tooltip("몸이 땅을 딛는 범위의 반경(m). 비탈에서 이 범위 안의 가장 높은 땅에 맞춰 서므로 발이 땅에 묻히지 않는다. 0이면 한가운데의 높이만 본다")]
        public float footprintRadius = 2.5f;

        [Header("경계")]
        [Tooltip("Alerted에서 Suspicious로 내려가기까지 걸리는 시간(초)")]
        public float alertedToSuspiciousTime = 5f;
        [Tooltip("Suspicious에서 Calm으로 내려가기까지 걸리는 시간(초)")]
        public float suspiciousToCalmTime = 8f;

        [Header("시야")]
        [Tooltip("볼 수 있는 최대 거리(m)")]
        public float viewDistance = 22f;
        [Tooltip("시야각(도)")]
        public float viewAngle = 120f;
        [Tooltip("눈높이(m). 적 오브젝트 위치 기준이다. 모델의 얼굴 높이(잡힐 때 연출의 Face Height)와 맞춘다")]
        public float eyeHeight = 11.6f;
        [Tooltip("시선을 겨냥할 플레이어 몸의 높이(m). 플레이어 오브젝트 위치(몸 한가운데, 발에서 1m 위) 기준이다. 모델의 가슴 높이에 맞춘다")]
        public float playerHeight = 3.5f;
        [Tooltip("보는지 확인하는 주기(초)")]
        public float detectInterval = 0.2f;
        [Tooltip("첫 라운드에서, 불빛 안의 플레이어를 볼 수 있는 거리의 배율. 1이면 평소와 같다")]
        public float litViewMultiplierFirstRound = 1f;
        [Tooltip("마지막 라운드에서의 배율. 라운드가 오를수록 첫 라운드 값에서 이 값으로 고르게 커진다")]
        public float litViewMultiplierLastRound = 2.5f;
        [Tooltip("발견 소리가 한 번 난 뒤, 다시 날 수 있을 때까지의 시간(초). 모든 적이 함께 쓴다")]
        public float spottedSoundCooldown = 3f;
    }

    [System.Serializable]
    public class EnemyBodyTuning
    {
        [Header("걷는 애니메이션")]
        [Tooltip("이 속도(m/s)보다 빠르게 움직이면 걷는 것으로 본다")]
        public float moveThreshold = 0.5f;
        [Tooltip("Walk 애니메이션이 원래 빠르기로 재생되는 이동 속도(m/s). 발이 미끄러져 보이면 이 값을 조절")]
        public float walkAnimMoveSpeed = 6f;
        [Tooltip("Walk 애니메이션의 최대 재생 배속")]
        public float maxAnimSpeed = 2.5f;
        [Tooltip("움직임 전체에 곱하는 재생 배속. 1보다 작으면 느리고 무겁게 움직인다 (이동 속도는 그대로다)")]
        public float animSpeedMultiplier = 1f;

        [Header("잡힐 때의 연출")]
        [Tooltip("연출 길이(초). 끝나면 게임 오버 화면으로 넘어간다")]
        public float jumpscareDuration = 1.4f;
        [Tooltip("카메라가 바라볼 지점의 높이(m). 적 오브젝트의 위치 기준. 얼굴 높이에 맞춘다")]
        public float faceHeight = 11.6f;
        [Tooltip("얼굴에서 카메라까지의 거리(m)")]
        public float cameraDistance = 5.37f;
        [Tooltip("얼굴 높이를 기준으로 한 카메라의 높이(m). 음수면 아래에서 올려다본다")]
        public float cameraHeightOffset = 0f;
        [Tooltip("연출 중 카메라의 시야각(도)")]
        public float fieldOfView = 60f;
        [Tooltip("조명의 밝기. 0이면 조명을 만들지 않는다")]
        public float lightIntensity = 2f;
        [Tooltip("조명의 색")]
        public Color lightColor = Color.white;
        [Tooltip("카메라 높이를 기준으로 한 조명의 높이(m). 음수면 아래에서 비춰 얼굴에 그림자가 거꾸로 진다")]
        public float lightHeightOffset = -1.5f;
    }

    [System.Serializable]
    public class SearchTuning
    {
        [Tooltip("한 사건에서 나눠 수색할 지점의 개수")]
        public int pointCount = 6;
        [Tooltip("발견 지점에서 이 거리(m)들의 둘레에서 수색 지점 후보를 뽑는다")]
        public float[] rings = { 8f, 16f, 24f };
        [Tooltip("수색 지점끼리 적어도 이만큼(m) 떨어지게 한다")]
        public float pointSpacing = 7f;
        [Tooltip("맡은 지점을 이 시간(초)이 지나도록 끝내지 못하면 다른 적이 가져갈 수 있다")]
        public float claimTimeout = 20f;
    }

    [System.Serializable]
    public class StageTuning
    {
        [Header("라운드")]
        [Tooltip("전체 라운드 수")]
        public int totalRounds = 5;
        [Tooltip("출구를 처음부터 열어 둘지 (시험용). 끄면 정해 둔 수의 모닥불을 켜야 열린다")]
        public bool openExitAtStart = false;

        [Header("라운드별 구성 (1라운드부터 차례로. 라운드가 이 목록보다 많으면 마지막 것을 쓴다)")]
        public RoundSetup[] rounds =
        {
            new RoundSetup { openZones = 2, campfires = 2, firesToOpenExit = 2, realEnemies = 2, fakeEnemies = 0, contaminationPools = 3,
                             lightIntensity = 1f, fogDensity = 1f, fakeContamination = 0.5f, realContamination = 0f },
            new RoundSetup { openZones = 3, campfires = 3, firesToOpenExit = 3, realEnemies = 3, fakeEnemies = 2, contaminationPools = 5,
                             lightIntensity = 0.8f, fogDensity = 1.2f, fakeContamination = 0.6f, realContamination = 0f },
            new RoundSetup { openZones = 4, campfires = 3, firesToOpenExit = 3, realEnemies = 5, fakeEnemies = 3, contaminationPools = 6,
                             lightIntensity = 0.6f, fogDensity = 1.45f, fakeContamination = 0.7f, realContamination = 0.15f },
            new RoundSetup { openZones = 4, campfires = 4, firesToOpenExit = 4, realEnemies = 6, fakeEnemies = 4, contaminationPools = 8,
                             lightIntensity = 0.45f, fogDensity = 1.7f, fakeContamination = 0.8f, realContamination = 0.25f },
            new RoundSetup { openZones = 4, campfires = 4, firesToOpenExit = 4, realEnemies = 6, fakeEnemies = 4, contaminationPools = 8,
                             lightIntensity = 0.4f, fogDensity = 1.8f, fakeContamination = 0.85f, realContamination = 0.3f },
        };

        /// <summary>그 라운드의 구성. 목록보다 큰 라운드는 마지막 구성을 쓴다.</summary>
        public RoundSetup Round(int round)
        {
            if (rounds == null || rounds.Length == 0)
                return new RoundSetup();
            return rounds[Mathf.Clamp(round - 1, 0, rounds.Length - 1)];
        }

        [Header("배치")]
        [Tooltip("봉우리의 반경(m). 적은 플레이어에게서 이 값의 2배(지름) 이상 떨어져 생성된다")]
        public float hillRadius = 40f;
        [Tooltip("출구의 자리. 지형 한가운데를 (0, 0)으로 본 (x, z)")]
        public Vector2 exitPosition = new Vector2(0f, -84f);
        [Tooltip("플레이어가 출구에서 지형 한가운데 쪽으로 얼마나 나와서 시작할지(m)")]
        public float playerDistanceFromExit = 25f;
        [Tooltip("지형 가장자리에서 이만큼(m)은 비워 둔다")]
        public float edgeMargin = 10f;
        [Tooltip("땅의 기울기가 이 각도(도)보다 가파른 곳에는 놓지 않는다")]
        public float maxSlope = 25f;
        [Tooltip("적과 모닥불이 먼저 놓인 오브젝트에서 적어도 이만큼(m)은 떨어지게 한다")]
        public float objectSpacing = 10f;

        [Header("모닥불")]
        [Tooltip("모닥불끼리 적어도 이만큼(m)은 떨어지게 한다. 덤불 고리가 겹치지 않도록 고리 지름보다 크게 둔다")]
        public float campfireSpacing = 50f;

        [Header("땅에서 띄우는 높이(m)")]
        [Tooltip("출구")]
        public float exitHeight = 13.63f;
        [Tooltip("플레이어. 기준점이 몸 한가운데(발에서 1m 위)이므로 1보다 조금 크게 둔다. 값이 크면 그만큼 공중에서 떨어지며 시작한다")]
        public float playerHeight = 1.2f;
    }

    /// <summary>한 라운드의 구성. 맵은 모든 라운드가 같고, 열린 구역과 놓이는 것의 수만 달라진다.</summary>
    [System.Serializable]
    public class RoundSetup
    {
        [Tooltip("열린 구역의 수 (1~4). 출구가 있는 남쪽부터 동 → 서 → 북 순서로 열린다. 닫힌 구역은 두꺼운 덤불로 막힌다")]
        [Range(1, 4)] public int openZones = 4;
        [Tooltip("놓을 모닥불의 수. 모두 꺼진 채 Real에서만 막히는 덤불 고리 안에 있다")]
        public int campfires = 3;
        [Tooltip("출구가 열리려면 켜야 하는 모닥불의 수. 실제로 놓인 수보다 크면 놓인 수만큼만 켜면 된다")]
        public int firesToOpenExit = 3;
        [Tooltip("Real 차원 적의 수")]
        public int realEnemies = 3;
        [Tooltip("Fake 차원 적의 수. 0이면 Fake에는 적이 없다")]
        public int fakeEnemies = 3;
        [Tooltip("Fake에서만 길을 막는 오염 웅덩이의 수")]
        public int contaminationPools = 5;

        [Header("분위기")]
        [Tooltip("씬에 놓아 둔 빛(해·달)과 주변광의 밝기 배율. 1이 씬 그대로이고, 작을수록 어둡다")]
        [Range(0f, 1.5f)] public float lightIntensity = 1f;
        [Tooltip("안개 짙기의 배율. 1이 씬 그대로이고, 클수록 가까운 곳까지 안개에 묻힌다")]
        [Range(0.5f, 3f)] public float fogDensity = 1f;
        [Tooltip("Fake에 들어갔을 때 드러나는 얼룩의 정도 (보호복의 오염 정도와 같은 뜻, 0~1)")]
        [Range(0f, 1f)] public float fakeContamination = 0.6f;
        [Tooltip("Real에도 늘 비치는 얼룩의 정도. 0이면 Real은 깨끗하다. 라운드가 오를수록 Real까지 오염이 번진다")]
        [Range(0f, 1f)] public float realContamination = 0f;
    }

    [System.Serializable]
    public class MapTuning
    {
        [Header("구역")]
        [Tooltip("닫힌 구역을 막는 덤불 벽의 두께(m)")]
        public float zoneWallThickness = 4f;

        [Header("봉인된 모닥불 (Real에서만 막히는 덤불 고리)")]
        [Tooltip("모닥불을 둘러싼 덤불 고리의 반지름(m). Fake로 들어가 8초 안에 불까지 가서 켤 수 있어야 하므로 " +
                 "고리 바깥에서 불까지 걷는 시간 + 켜는 시간이 8초보다 넉넉히 짧게 둔다 (걷기 7.5m/s 기준 18m ≈ 2.4초)")]
        public float ringRadius = 18f;
        [Tooltip("덤불 고리의 두께(m)")]
        public float ringThickness = 3f;
        [Tooltip("이 거리(m, 평면) 안에서 E를 누르고 있으면 불을 붙인다")]
        public float lightRadius = 4f;
        [Tooltip("불이 붙기까지 E를 누르고 있어야 하는 시간(초)")]
        public float lightHoldTime = 1.5f;
        [Tooltip("불이 붙은 뒤 덤불 고리가 타서 사라지는 데 걸리는 시간(초). 막는 것은 불이 붙는 순간 바로 풀린다")]
        public float burnTime = 1.5f;

        [Header("덤불의 모습 (숲의 나무와 풀을 복제해 늘어놓는다)")]
        [Tooltip("덤불을 늘어놓는 간격(m)")]
        public float bushSpacing = 3.5f;
        [Tooltip("덤불 하나의 크기 배율 (최소)")]
        public float bushScaleMin = 1f;
        [Tooltip("덤불 하나의 크기 배율 (최대)")]
        public float bushScaleMax = 1.6f;
        [Tooltip("덤불을 몇 줄로 늘어놓는가. 1이면 한 줄. 늘리면 벽이 두꺼워 보이지만 그만큼 많아진다")]
        [Range(1, 3)] public int bushRows = 1;
        [Tooltip("구역 벽에 큰 나무도 섞을지. 섞으면 벽이 높아 보이지만, 큰 잎이 플레이어를 가리기 쉽다")]
        public bool zoneWallTrees = false;
        [Tooltip("닫힌 구역 벽 덤불의 색. 불로도 걷히지 않는 덤불이라 어둡고 거칠게 보이게 한다")]
        public Color zoneWallTint = new Color(0.35f, 0.3f, 0.35f);
        [Tooltip("모닥불 고리 덤불의 색")]
        public Color ringTint = new Color(0.7f, 0.65f, 0.55f);

        [Header("오염 웅덩이 (Fake에서만 막힘)")]
        [Tooltip("웅덩이 반지름(m, 최소)")]
        public float poolRadiusMin = 3f;
        [Tooltip("웅덩이 반지름(m, 최대)")]
        public float poolRadiusMax = 6f;
        [Tooltip("웅덩이 가운데의 색")]
        public Color poolColor = new Color(0.02f, 0.02f, 0.03f);
        [Tooltip("웅덩이 가장자리의 희미한 빛 색")]
        public Color poolRimColor = new Color(0.35f, 0.5f, 0.2f);

        [Header("뿌리 (Fake에서만 보임, 꺼진 모닥불에서 출구까지 이어진다)")]
        [Tooltip("뿌리의 굵기(m)")]
        public float rootWidth = 0.7f;
        [Tooltip("뿌리의 색")]
        public Color rootColor = new Color(0.03f, 0.02f, 0.03f);
        [Tooltip("뿌리를 따라 흐르는 빛의 색. 출구 쪽으로 흘러간다")]
        public Color rootPulseColor = new Color(0.45f, 0.65f, 0.25f);
        [Tooltip("뿌리가 좌우로 구불거리는 정도(m)")]
        public float rootWiggle = 1.2f;

        [Header("랜드마크 (모든 라운드에 같은 자리. Fake에서는 그을리고 덩굴이 감긴 모습)")]
        [Tooltip("봉우리 꼭대기와 각 구역의 랜드마크 선돌 크기 배율")]
        public float landmarkScale = 1f;
        [Tooltip("Fake에서 랜드마크가 그을린 색")]
        public Color landmarkCharred = new Color(0.16f, 0.13f, 0.13f);
        [Tooltip("Fake에서 랜드마크에 감긴 덩굴의 색")]
        public Color landmarkVineColor = new Color(0.04f, 0.03f, 0.04f);

        [Header("차원 이동 위치 보정")]
        [Tooltip("넘어간 자리가 그 차원에서 막혀 있으면, 이 거리(m) 안의 가장 가까운 열린 자리로 옮긴다")]
        public float escapeSearchDistance = 30f;
    }

    [System.Serializable]
    public class FakeScreenTuning
    {
        [Tooltip("효과가 나타나고 사라지는 데 걸리는 시간(초)")]
        public float fadeTime = 0.6f;
        [Tooltip("노이즈의 세기")]
        [Range(0f, 1f)] public float grainIntensity = 0.6f;
        [Tooltip("노이즈 알갱이의 크기")]
        [Range(0.3f, 3f)] public float grainSize = 1.4f;
        [Tooltip("화면이 휘는 정도. 클수록 크게 일렁인다")]
        [Range(0f, 60f)] public float wobbleAmount = 18f;
        [Tooltip("일렁이는 빠르기")]
        public float wobbleSpeed = 1.5f;
        [Tooltip("가장자리에서 색이 번지는 정도")]
        [Range(0f, 1f)] public float colorFringe = 0.5f;
        [Tooltip("가장자리가 어두워지는 정도")]
        [Range(0f, 1f)] public float vignette = 0.4f;
        [Tooltip("색이 빠지는 정도. -100이면 흑백, 0이면 그대로")]
        [Range(-100f, 0f)] public float saturation = -45f;
        [Tooltip("화면 전체에 입힐 색조")]
        public Color tint = new Color(0.75f, 0.85f, 1f);
    }

    [System.Serializable]
    public class FakeContaminationTuning
    {
        [Header("퍼지는 물결")]
        [Tooltip("Fake에 들어가는 순간 플레이어 자리에서 오염이 퍼져 나가는 빠르기(m/s)")]
        public float spreadSpeed = 90f;
        [Tooltip("물결이 지나간 뒤, 그 자리의 얼룩이 다 번지기까지 물결이 더 나아가는 거리(m). 클수록 뒤에서 천천히 번진다")]
        public float growDistance = 25f;
        [Tooltip("물결 앞머리의 띠 폭(m)")]
        public float frontWidth = 4f;
        [Tooltip("물결 앞머리의 빛 색")]
        public Color frontColor = new Color(0.55f, 0.75f, 0.35f);
        [Tooltip("물결 앞머리가 빛나는 세기")]
        [Range(0f, 3f)] public float frontGlow = 1.2f;
        [Tooltip("물결이 이 거리(m)까지 퍼지면 앞머리의 빛이 다 사라진다 (얼룩은 계속 퍼진다)")]
        public float frontFadeDistance = 120f;
        [Tooltip("Real로 돌아갈 때 얼룩이 걷히는 데 걸리는 시간(초)")]
        public float fadeOutTime = 0.5f;

        [Header("오염의 흔적 (보호복과 같은 방식으로 번진다)")]
        // 오염 정도는 라운드마다 다르다: 라운드별 구성(stage.rounds)의 fakeContamination / realContamination
        [Tooltip("번지는 경계의 부드러움. 보호복 셰이더의 값과 같은 뜻이다")]
        [Range(0.01f, 0.5f)] public float spread = 0.15f;
        [Tooltip("얼룩 하나의 크기(m). 클수록 큰 덩어리로 묻는다")]
        public float stainSize = 7f;
        [Tooltip("얼룩이 묻은 곳의 색. 화면 색에 곱해진다 (어두울수록 진하게 묻는다)")]
        public Color stainColor = new Color(0.22f, 0.17f, 0.13f);
        [Tooltip("얼룩의 진하기")]
        [Range(0f, 1f)] public float stainStrength = 0.85f;
        [Tooltip("얼룩 가장자리에 도는 희미한 빛의 색. 어둠 속에서도 얼룩이 보이게 한다")]
        public Color rimColor = new Color(0.45f, 0.6f, 0.25f);
        [Tooltip("얼룩 가장자리 빛의 세기. 0이면 빛나지 않는다")]
        [Range(0f, 1f)] public float rimGlow = 0.25f;

        [Header("캐릭터에는 묻히지 않기")]
        [Tooltip("플레이어 몸 둘레에서 얼룩을 지우는 반경(m)")]
        public float playerClearRadius = 1.8f;
        [Tooltip("플레이어 몸의 높이(m, 발 기준)")]
        public float playerClearHeight = 7f;
        [Tooltip("적 몸 둘레에서 얼룩을 지우는 반경(m)")]
        public float enemyClearRadius = 3.5f;
        [Tooltip("적 몸의 높이(m, 발 기준)")]
        public float enemyClearHeight = 15f;
    }

    [System.Serializable]
    public class ContaminationEdgeTuning
    {
        [Tooltip("숨기기 시작한 뒤 차가운 색 톤이 다 드는 데 걸리는 시간(초)")]
        public float chillTime = 2f;
        [Tooltip("오염 결정이 자라 들어오는 빠르기 (1초에 덮는 정도가 늘어날 수 있는 최대량)")]
        public float growSpeed = 0.5f;
        [Tooltip("횃불을 들었을 때 김서림과 오염이 걷히는 빠르기")]
        public float burnAwaySpeed = 2f;
    }

    [System.Serializable]
    public class CctvTuning
    {
        [Tooltip("이 거리(m)보다 멀면 노이즈가 없다")]
        public float farDistance = 50f;
        [Tooltip("이 거리(m) 안으로 들어오면 노이즈가 가장 세다")]
        public float nearDistance = 8f;
        [Tooltip("가장 셀 때의 노이즈 세기")]
        [Range(0f, 1f)] public float maxIntensity = 0.8f;
        [Tooltip("세기가 출렁이는 빠르기")]
        public float flickerSpeed = 6f;
        [Tooltip("1초에 몇 번꼴로 노이즈가 확 튀는가 (적이 가장 가까울 때 기준)")]
        public float spikesPerSecond = 1.5f;
        [Tooltip("지지직 소리의 크기. 효과음 볼륨에 곱해진다. 0이면 소리를 내지 않는다")]
        [Range(0f, 1f)] public float soundVolume = 0.6f;
    }

    [System.Serializable]
    public class FoliageTuning
    {
        [Tooltip("플레이어 주변에 뚫는 창의 반지름(m). 플레이어 몸 한가운데를 중심으로 잰다")]
        public float cutoutRadius = 5f;
        [Tooltip("창의 가장자리를 부드럽게 흩뜨리는 폭(m). 0이면 칼로 자른 듯한 경계가 된다")]
        public float edgeWidth = 0.6f;
        [Tooltip("창이 열리거나 닫히는 데 걸리는 시간(초)")]
        public float fadeTime = 0.25f;
        [Tooltip("시선의 굵기(m). 클수록 시선 가까이에 있는 것까지 함께 창의 대상이 된다")]
        public float sightRadius = 1f;
    }

    [System.Serializable]
    public class BoundaryTuning
    {
        [Tooltip("벽의 색")]
        public Color color = new Color(0.25f, 0.55f, 1f);
        [Tooltip("빛나는 정도")]
        [Range(0f, 2f)] public float glow = 0.6f;
        [Tooltip("벽 너머가 일렁이는 정도")]
        [Range(0f, 0.05f)] public float distortion = 0.015f;
        [Tooltip("플레이어가 이 거리(m) 안으로 다가가면 벽이 가장 또렷해진다")]
        public float nearDistance = 30f;
        [Tooltip("멀리 있는 벽이 보이는 정도 (0: 안 보임, 1: 가까이 있을 때만큼 또렷함)")]
        [Range(0f, 1f)] public float farStrength = 0.15f;
    }

    [System.Serializable]
    public class AudioTuning
    {
        [Header("배경음")]
        [Tooltip("배경음 볼륨")]
        [Range(0f, 1f)] public float musicVolume = 0.321f;
        [Tooltip("곡이 바뀔 때 줄어들고 커지는 데 걸리는 시간(초)")]
        public float musicFadeDuration = 1f;

        [Header("효과음")]
        [Tooltip("효과음 볼륨")]
        [Range(0f, 1f)] public float sfxVolume = 0.5f;
        [Tooltip("이 거리(m)까지는 최대 크기로 들린다")]
        public float minDistance = 10f;
        [Tooltip("이 거리(m)부터는 들리지 않는다")]
        public float maxDistance = 40f;
        [Tooltip("동시에 재생할 수 있는 3D 효과음의 최대 개수")]
        public int poolSize = 8;
    }
}
