using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 디버그용. 플레이 중에 F2를 누르면 화면을 깨끗하게 해서 맵을 둘러볼 수 있게 한다. 다시 누르면 원래대로 돌아간다.
    ///
    /// 켜 있는 동안에는
    ///   - 라운드마다 어두워지는 숲의 밝기를 씬 그대로(1라운드의 밝기)로 두고, 안개를 끈다 (RoundAtmosphere)
    ///   - 같은 차원의 적이 가까울 때 끼는 화면 노이즈를 끈다. 지지직 소리는 그대로 난다 (CctvNoiseEffect)
    /// 라운드가 바뀌어도 켠 상태가 이어진다. 화면 왼쪽 위에 켜져 있다고 표시한다.
    ///
    /// 씬이나 프리팹에 붙일 필요가 없다. 게임이 시작될 때 스스로 만들어진다.
    /// 필요 없어지면 이 파일과, RoundAtmosphere·CctvNoiseEffect에서 On을 읽는 줄만 지우면 된다.
    /// </summary>
    public class CleanViewDebug : MonoBehaviour
    {
        // 깨끗한 화면을 켜고 끄는 키
        private const KeyCode ToggleKey = KeyCode.F2;

        /// 지금 깨끗한 화면이 켜져 있는가.
        public static bool On { get; private set; }

        private GUIStyle _labelStyle;

        // 게임이 시작되고 첫 씬이 열린 직후에 Unity가 한 번 불러 준다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            On = false;
            var debugObject = new GameObject("Clean View Debug");
            debugObject.AddComponent<CleanViewDebug>();
            // Stage가 라운드마다 바뀌어도 이 오브젝트는 남아 있어야 한다.
            DontDestroyOnLoad(debugObject);
        }

        private void Update()
        {
            if (!Input.GetKeyDown(ToggleKey))
                return;

            On = !On;
            RoundAtmosphere.Refresh();
            Debug.Log(On ? "[CleanViewDebug] 깨끗한 화면을 켰습니다." : "[CleanViewDebug] 깨끗한 화면을 껐습니다.");
        }

        private void OnGUI()
        {
            if (!On)
                return;

            _labelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 16, normal = { textColor = Color.yellow } };
            GUI.Label(new Rect(12f, 12f, 400f, 30f), "깨끗한 화면 (F2로 끄기)", _labelStyle);
        }
    }
}
