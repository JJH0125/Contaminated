using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>
/// 열린 씬의 메뉴와 글자를 꾸미는 에디터 도구.
/// 메뉴: Tools > 메뉴 UI 꾸미기
///
/// 씬 안의 모든 Button(꺼진 것 포함)과 그 버튼이 놓인 패널에 적용한다.
///   버튼: 어두운 UI 팩(ThirdParty/kΩsmaragd)의 분필 테두리(dark-Buttons_7, 9분할). 마우스를 올리면 모닥불 주황, 누르면 어두운 주황
///   버튼 글자: 밝은 분필색 (글꼴은 그대로 Noto Sans KR)
///   패널 바탕(PauseUI 등): 반투명 검정. 타이틀은 뒤의 숲(TitleBackground)이 보이도록 아주 옅게
///
/// 글꼴 세 가지 (Description 15번):
///   Black Ops One   타이틀 제목 "CONTAMINATED" (포스터와 같은 글꼴, 영문 전용). TitleUI 안에 "TitleText"를 만들거나 고친다
///   Black Han Sans  큰 문구(패널 안의 버튼이 아닌 글자: 일시정지됨, 잡혔습니다, 탈출 성공)와 라운드 시작 문구
///                   GameUI 안에 "RoundBanner"(RoundBanner 컴포넌트)를 만들거나 고친다
///   Noto Sans KR    메뉴, HUD, 안내 문장처럼 읽어야 하는 곳. 이미 씬의 모든 글자가 이것이라 건드리지 않는다
/// Black Ops One과 Black Han Sans의 TMP 글꼴 에셋은 없으면 ttf 옆에 만든다. 쓰는 글자만 미리 그려 둔 고정 에셋이라
/// 실행 중에 바뀌지 않는다. 문구를 고쳐 새 글자가 생기면 이 메뉴를 다시 실행하면 그 글자를 더 그려 넣는다.
///
/// 되돌리기(Ctrl+Z)가 되고(글꼴 에셋은 제외), 적용한 뒤 씬을 저장해야 남는다.
/// Game 씬에 적용한 뒤 Tools > 시연 씬 만들기를 다시 실행하면 시연 씬에도 들어간다.
/// 모습을 바꾸려면 아래 값만 고치고 메뉴를 다시 실행한다.
/// </summary>
public static class MenuStyleTool
{
    private const string UndoName = "메뉴 UI 꾸미기";

    private const string SheetPath = "Assets/ThirdParty/kΩsmaragd/Dark Buttons/dark-Buttons.png";
    private const string ButtonSprite = "dark-Buttons_7";

    // 테두리 굵기. 클수록 테두리가 가늘어진다 (9분할 테두리를 이 배수로 줄여 그린다)
    private const float BorderScale = 3f;
    private static readonly Vector2 ButtonSize = new(240f, 60f);

    private static readonly Color Chalk = new(0.86f, 0.84f, 0.80f, 1f);
    private static readonly Color Ember = new(1f, 0.58f, 0.25f, 1f);
    private static readonly Color EmberDark = new(0.65f, 0.30f, 0.12f, 1f);
    private static readonly Color Disabled = new(0.4f, 0.4f, 0.4f, 0.5f);
    private static readonly Color PanelBackground = new(0f, 0f, 0f, 0.75f);
    private static readonly Color TitlePanelBackground = new(0f, 0f, 0f, 0.15f);

    private const string FontFolder = "Assets/_Game/UI/Font/";

    // 타이틀 제목 (Black Ops One)
    private const string TitleFont = "BlackOpsOne-Regular";
    private const string TitleText = "CONTAMINATED";
    private const string TitleCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 -:.!?";
    private const float TitleFontSize = 130f;
    private const float TitleSpacing = 12f;
    // 화면 가운데 기준. 시작 버튼(y 50)보다 위
    private static readonly Vector2 TitlePosition = new(0f, 260f);

    // 큰 문구와 라운드 시작 문구 (Black Han Sans)
    private const string HeadlineFont = "BlackHanSans-Regular";
    private const string HeadlineExtraCharacters = "0123456789 .!?…";
    private const float HeadlineFontSize = 72f;
    private const float BannerFontSize = 84f;
    // 화면 가운데 기준. 플레이어보다 위
    private static readonly Vector2 BannerPosition = new(0f, 200f);

    [MenuItem("Tools/메뉴 UI 꾸미기")]
    private static void Apply()
    {
        Sprite sprite = LoadSprite();
        if (sprite == null)
        {
            EditorUtility.DisplayDialog(UndoName, $"{SheetPath}에서 {ButtonSprite}를 찾을 수 없습니다.", "확인");
            return;
        }

        var panels = new HashSet<Transform>();
        int count = 0;
        foreach (Button button in InScene<Button>())
        {
            StyleButton(button, sprite);
            if (button.transform.parent != null)
                panels.Add(button.transform.parent);
            count++;
        }
        foreach (Transform panel in panels)
            StylePanel(panel);

        var report = new List<string> { $"버튼 {count}개" };
        if (PlaceTitle())
            report.Add("타이틀 제목");
        report.AddRange(PlaceHeadlines(panels));

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog(UndoName, $"적용했습니다: {string.Join(", ", report)}.\n씬을 저장해야 남습니다.", "확인");
    }

    // 프리팹 원본 등 씬 밖의 것은 빼고, 꺼진 것까지 찾는다.
    private static IEnumerable<T> InScene<T>() where T : Component
        => Object.FindObjectsOfType<T>(true).Where(c => c.gameObject.scene.IsValid());

    private static Sprite LoadSprite()
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(SheetPath))
            if (asset is Sprite sprite && sprite.name == ButtonSprite)
                return sprite;
        return null;
    }

    // ── 버튼과 패널 ──────────────────────────────────────────────

    private static void StyleButton(Button button, Sprite sprite)
    {
        if (button.targetGraphic is Image image)
        {
            Undo.RecordObject(image, UndoName);
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.pixelsPerUnitMultiplier = BorderScale;
            image.color = Color.white;
        }

        Undo.RecordObject(button, UndoName);
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Chalk;
        colors.highlightedColor = Ember;
        colors.pressedColor = EmberDark;
        // 누른 뒤에도 주황으로 남지 않게, 선택된 상태는 평소와 같게 둔다.
        colors.selectedColor = Chalk;
        colors.disabledColor = Disabled;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        var rect = (RectTransform)button.transform;
        Undo.RecordObject(rect, UndoName);
        rect.sizeDelta = ButtonSize;

        foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true))
        {
            Undo.RecordObject(text, UndoName);
            text.color = Chalk;
        }
    }

    // 버튼이 놓인 패널의 바탕을 어둡게 한다. 바탕 그림이 없는 패널은 그대로 둔다.
    private static void StylePanel(Transform panel)
    {
        if (!panel.TryGetComponent(out Image background) || background.GetComponent<Button>() != null)
            return;

        Undo.RecordObject(background, UndoName);
        background.color = panel.GetComponent<Squad.TitleUI>() != null ? TitlePanelBackground : PanelBackground;
    }

    // ── 타이틀 제목 (Black Ops One) ──────────────────────────────

    // 타이틀 화면에 제목을 놓는다. 이미 있으면 글꼴·크기·자리만 다시 맞춘다.
    private static bool PlaceTitle()
    {
        Squad.TitleUI titleUI = InScene<Squad.TitleUI>().FirstOrDefault();
        TMP_FontAsset font = EnsureFont(TitleFont, TitleCharacters, 90);
        if (titleUI == null || font == null)
            return false;

        TextMeshProUGUI text = FindOrCreateText(titleUI.transform, "TitleText");
        Place(text, TitlePosition, new Vector2(1600f, 220f));
        text.text = TitleText;
        text.font = font;
        text.fontSize = TitleFontSize;
        text.characterSpacing = TitleSpacing;
        text.color = Chalk;
        return true;
    }

    // ── 큰 문구와 라운드 시작 문구 (Black Han Sans) ──────────────

    // 패널 안의 큰 문구(버튼 글자와 타이틀 제목이 아닌 것)와 라운드 시작 문구에 Black Han Sans를 입힌다.
    private static IEnumerable<string> PlaceHeadlines(IEnumerable<Transform> panels)
    {
        var headlines = new List<TMP_Text>();
        foreach (Transform panel in panels)
            foreach (TMP_Text text in panel.GetComponentsInChildren<TMP_Text>(true))
                if (!InsideButton(text.transform, panel) && text.name != "TitleText")
                    headlines.Add(text);

        Squad.GameUI gameUI = InScene<Squad.GameUI>().FirstOrDefault();
        Squad.RoundBanner banner = null;
        if (gameUI != null)
        {
            TextMeshProUGUI bannerText = FindOrCreateText(gameUI.transform, "RoundBanner");
            if (!bannerText.TryGetComponent(out banner))
                banner = Undo.AddComponent<Squad.RoundBanner>(bannerText.gameObject);
            Place(bannerText, BannerPosition, new Vector2(1400f, 260f));
            bannerText.text = "";
            bannerText.fontSize = BannerFontSize;
            bannerText.color = Chalk;
        }

        // 쓰이는 글자만 글꼴 에셋에 그려 둔다.
        string characters = HeadlineExtraCharacters + string.Concat(headlines.Select(t => t.text));
        if (banner != null)
            characters += banner.Characters;
        TMP_FontAsset font = EnsureFont(HeadlineFont, characters, 72);
        if (font == null)
            return new[] { "Black Han Sans 글꼴 없음" };

        foreach (TMP_Text text in headlines)
        {
            Undo.RecordObject(text, UndoName);
            Undo.RecordObject(text.rectTransform, UndoName);
            text.font = font;
            text.fontSize = HeadlineFontSize;
            text.color = Chalk;
            // 원래 200×50이라 큰 글자가 넘치므로 넓힌다.
            text.rectTransform.sizeDelta = new Vector2(1000f, 120f);
        }
        if (banner != null)
            banner.GetComponent<TMP_Text>().font = font;

        var report = new List<string>();
        if (headlines.Count > 0)
            report.Add($"큰 문구 {headlines.Count}개");
        report.Add(banner != null ? "라운드 시작 문구" : "라운드 시작 문구 없음(GameUI를 못 찾음)");
        return report;
    }

    // ── 공통 ─────────────────────────────────────────────────────

    // panel 아래에서 child까지 가는 길에 버튼이 있는가. (꺼진 오브젝트도 본다)
    private static bool InsideButton(Transform child, Transform panel)
    {
        for (Transform t = child; t != null && t != panel; t = t.parent)
            if (t.GetComponent<Button>() != null)
                return true;
        return false;
    }

    private static TextMeshProUGUI FindOrCreateText(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null && existing.TryGetComponent(out TextMeshProUGUI found))
        {
            Undo.RecordObject(found, UndoName);
            Undo.RecordObject(found.rectTransform, UndoName);
            return found;
        }

        var textObject = new GameObject(name, typeof(RectTransform));
        textObject.layer = parent.gameObject.layer;
        textObject.transform.SetParent(parent, false);
        var text = textObject.AddComponent<TextMeshProUGUI>();
        Undo.RegisterCreatedObjectUndo(textObject, UndoName);
        return text;
    }

    // 화면 가운데를 기준으로 놓는다.
    private static void Place(TMP_Text text, Vector2 position, Vector2 size)
    {
        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
    }

    /// <summary>
    /// FontFolder의 name.ttf로 만든 TMP 글꼴 에셋(name SDF.asset). 없으면 만들고, characters 중 빠진 글자가 있으면 더 그려 넣는다.
    /// 글자를 다 그린 뒤에는 고정(Static)으로 두어 실행 중에 에셋 파일이 바뀌지 않게 한다. ttf가 없으면 null.
    /// </summary>
    private static TMP_FontAsset EnsureFont(string name, string characters, int samplingSize)
    {
        string assetPath = FontFolder + name + " SDF.asset";
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);

        if (font == null)
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>(FontFolder + name + ".ttf");
            if (source == null)
                return null;

            // 아틀라스를 한 장으로 둔다. (여러 장이 되면 새 장이 에셋에 저장되지 않는다)
            font = TMP_FontAsset.CreateFontAsset(source, samplingSize, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                                                 AtlasPopulationMode.Dynamic, false);
            if (font == null)
                return null;
            font.name = name + " SDF";
            AssetDatabase.CreateAsset(font, assetPath);
            font.atlasTexture.name = font.name + " Atlas";
            AssetDatabase.AddObjectToAsset(font.atlasTexture, font);
            font.material.name = font.name + " Material";
            AssetDatabase.AddObjectToAsset(font.material, font);
        }

        string missing = new string(characters.Distinct().Where(c => !char.IsControl(c) && !font.HasCharacter(c)).ToArray());
        if (missing.Length > 0)
        {
            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            if (!font.TryAddCharacters(missing, out string failed) && !string.IsNullOrEmpty(failed))
                Debug.LogWarning($"[{UndoName}] {name}에 그리지 못한 글자: {failed}");
        }
        font.atlasPopulationMode = AtlasPopulationMode.Static;
        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssets();
        return font;
    }
}
