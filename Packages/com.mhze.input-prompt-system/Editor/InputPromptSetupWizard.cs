using MHZE.InputPromptSystem;
using UnityEditor;
using UnityEngine;

namespace MHZE.InputPromptSystem.Editor
{
    public static class InputPromptSetupWizard
{
    [MenuItem("Tools/Input Prompt System/Setup Input Prompt", false, 100)]
    public static void SetupInputPrompt()
    {
        var canvasPrefab = LoadAsset<GameObject>("InputPromptCanvas t:Prefab");
        var textPrefab = LoadAsset<GameObject>("InputPromptTextPrefab t:Prefab");
        var iconLibrary = LoadAsset<InputBindingIconLibrary>("t:InputBindingIconLibrary");
        var promptCollection = LoadAsset<InputPromptCollection>("t:InputPromptCollection");

        if (canvasPrefab == null)
        {
            Debug.LogError("Could not find InputPromptCanvas.prefab.");
            return;
        }

        if (textPrefab == null)
        {
            Debug.LogError("Could not find InputPromptTextPrefab.prefab.");
            return;
        }

        if (iconLibrary == null)
        {
            Debug.LogError("Could not find InputBindingIconLibrary asset.");
            return;
        }

        if (promptCollection == null)
        {
            Debug.LogError("Could not find InputPromptCollection asset.");
            return;
        }

        Undo.SetCurrentGroupName("Setup Input Prompt System");
        var group = Undo.GetCurrentGroup();

        var promptUI = FindOrCreateCanvasUI(canvasPrefab, textPrefab);
        FindOrCreateManager(promptUI, iconLibrary, promptCollection);

        Undo.CollapseUndoOperations(group);

        Debug.Log("Input Prompt System setup complete.");
    }

    private static InputPromptUI FindOrCreateCanvasUI(GameObject canvasPrefab, GameObject textPrefab)
    {
        var existing = Object.FindFirstObjectByType<InputPromptUI>();
        if (existing != null)
        {
            Undo.RecordObject(existing, "Setup Input Prompt UI");
            ConfigureUIComponent(existing, textPrefab);
            return existing;
        }

        var canvasGO = (GameObject)PrefabUtility.InstantiatePrefab(canvasPrefab);
        if (canvasGO == null)
        {
            canvasGO = Object.Instantiate(canvasPrefab);
        }

        Undo.RegisterCreatedObjectUndo(canvasGO, "Create Input Prompt Canvas");
        canvasGO.name = "InputPromptCanvas";

        var ui = canvasGO.GetComponent<InputPromptUI>();
        if (ui == null)
        {
            ui = Undo.AddComponent<InputPromptUI>(canvasGO);
        }

        ConfigureUIComponent(ui, textPrefab);
        return ui;
    }

    private static void ConfigureUIComponent(InputPromptUI ui, GameObject textPrefab)
    {
        var serialized = new SerializedObject(ui);

        serialized.FindProperty("promptPrefab").objectReferenceValue = textPrefab;

        var centerHolder = ui.transform.Find("CenterHolder") as RectTransform;
        var rightHolder = ui.transform.Find("RightHolder") as RectTransform;
        var leftHolder = ui.transform.Find("LeftHolder") as RectTransform;

        serialized.FindProperty("centerAnchor").objectReferenceValue = centerHolder;
        serialized.FindProperty("rightAnchor").objectReferenceValue = rightHolder;
        serialized.FindProperty("leftAnchor").objectReferenceValue = leftHolder;

        serialized.ApplyModifiedProperties();
    }

    private static void FindOrCreateManager(InputPromptUI promptUI, InputBindingIconLibrary iconLibrary, InputPromptCollection promptCollection)
    {
        var existing = Object.FindFirstObjectByType<InputPromptManager>();
        if (existing != null)
        {
            Undo.RecordObject(existing, "Setup Input Prompt Manager");
            ConfigureManagerComponent(existing, promptUI, iconLibrary, promptCollection);
            return;
        }

        var go = new GameObject("InputPromptManager");
        Undo.RegisterCreatedObjectUndo(go, "Create Input Prompt Manager");

        var manager = go.AddComponent<InputPromptManager>();
        ConfigureManagerComponent(manager, promptUI, iconLibrary, promptCollection);
    }

    private static void ConfigureManagerComponent(InputPromptManager manager, InputPromptUI promptUI, InputBindingIconLibrary iconLibrary, InputPromptCollection promptCollection)
    {
        var serialized = new SerializedObject(manager);

        serialized.FindProperty("promptUI").objectReferenceValue = promptUI;
        serialized.FindProperty("bindingIconLibrary").objectReferenceValue = iconLibrary;
        serialized.FindProperty("inputPromptCollection").objectReferenceValue = promptCollection;

        serialized.ApplyModifiedProperties();
    }

    private static T LoadAsset<T>(string filter) where T : Object
    {
        var guids = AssetDatabase.FindAssets(filter);
        if (guids.Length == 0)
        {
            return null;
        }

        var path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }
}
}
