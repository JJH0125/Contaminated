using System;
using System.Collections.Generic;
using Squad;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 시연 씬 7개를 Game 씬에서 복사해 만드는 에디터 도구.
/// 메뉴: Tools > 시연 씬 만들기
///
/// Game 씬을 Scenes/Demo 폴더에 단계 수만큼 복사하고, 각 씬에 DemoScenario 오브젝트를 하나 넣어 단계를 정한다.
/// 씬을 열고 플레이하면 타이틀을 건너뛰고 그 단계에 맞게 꾸민 라운드가 바로 시작된다.
///
/// 시연 씬은 Game 씬의 복사본이라, Game 씬을 고친 뒤에는 이 메뉴를 다시 실행해야 고친 것이 시연 씬에도 들어간다.
/// (같은 이름의 시연 씬은 지우고 새로 만든다. 시연 씬을 직접 고쳤다면 그 내용은 사라진다)
/// 만든 씬은 Build Settings에도 Game 씬 뒤에 넣는다.
/// </summary>
public static class DemoSceneBuilder
{
    private const string SourceScene = "Assets/_Game/Scenes/Game.unity";
    private const string ParentFolder = "Assets/_Game/Scenes";
    private const string DemoFolder = ParentFolder + "/Demo";

    private static readonly Dictionary<DemoStep, string> FileNames = new()
    {
        { DemoStep.Round1, "1_실제게임_1라운드" },
        { DemoStep.GoapBasics, "2_GOAP_기본" },
        { DemoStep.GoalToggle, "3_목표_켜고끄기" },
        { DemoStep.PlanFailure, "4_계획_실패" },
        { DemoStep.DualGrid, "5_두겹_격자" },
        { DemoStep.Cooperation, "6_협력" },
        { DemoStep.Round3, "7_실제게임_3라운드" },
    };

    [MenuItem("Tools/시연 씬 만들기")]
    private static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScene) == null)
        {
            EditorUtility.DisplayDialog("시연 씬 만들기", SourceScene + "을 찾을 수 없습니다.", "확인");
            return;
        }
        // 열려 있는 씬에 저장하지 않은 변경이 있으면 먼저 묻는다. (취소하면 아무것도 하지 않는다)
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        string previous = SceneManager.GetActiveScene().path;
        if (!AssetDatabase.IsValidFolder(DemoFolder))
            AssetDatabase.CreateFolder(ParentFolder, "Demo");

        var built = new List<string>();
        DemoStep[] steps = (DemoStep[])Enum.GetValues(typeof(DemoStep));
        foreach (DemoStep step in steps)
        {
            string path = $"{DemoFolder}/{FileNames[step]}.unity";
            AssetDatabase.DeleteAsset(path);
            if (!AssetDatabase.CopyAsset(SourceScene, path))
            {
                Debug.LogError("[DemoSceneBuilder] 복사하지 못했습니다: " + path);
                continue;
            }

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var scenarioObject = new GameObject("Demo Scenario");
            var scenario = scenarioObject.AddComponent<DemoScenario>();
            var serialized = new SerializedObject(scenario);
            serialized.FindProperty("step").enumValueIndex = Array.IndexOf(steps, step);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);
            built.Add(path);
        }

        AddToBuildSettings(built);

        if (!string.IsNullOrEmpty(previous))
            EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);

        Debug.Log($"[DemoSceneBuilder] 시연 씬 {built.Count}개를 {DemoFolder}에 만들었습니다.");
    }

    // 이미 있는 씬은 그대로 두고, 없는 시연 씬만 뒤에 붙인다.
    private static void AddToBuildSettings(List<string> paths)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (string path in paths)
            if (!scenes.Exists(scene => scene.path == path))
                scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
