using System;
using MHZE.InputPromptSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MHZE.InputPromptSystem.Editor
{
    [CustomEditor(typeof(InputBindingIconLibrary))]
    public class InputBindingIconLibraryEditor : UnityEditor.Editor
{
    private InputActionReference sourceAction;
    private int selectedBindingIndex;
    private Sprite sourceIcon;
    private bool useWildcardLayout;
    private string layoutOverride;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();

        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField("Capture From Binding", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Pick an InputAction + binding, then click Add/Update Entry. This auto-fills control path and layout so you don’t type strings manually.", MessageType.Info);

        sourceAction = (InputActionReference)EditorGUILayout.ObjectField("Input Action", sourceAction, typeof(InputActionReference), false);
        sourceIcon = (Sprite)EditorGUILayout.ObjectField("Icon", sourceIcon, typeof(Sprite), false);

        var action = sourceAction != null ? sourceAction.action : null;
        if (action == null)
        {
            EditorGUILayout.HelpBox("Assign an InputActionReference to capture binding data.", MessageType.None);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        var labels = BuildBindingLabels(action);
        if (labels.Length == 0)
        {
            EditorGUILayout.HelpBox("Selected action has no bindings.", MessageType.Warning);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        selectedBindingIndex = Mathf.Clamp(selectedBindingIndex, 0, labels.Length - 1);
        selectedBindingIndex = EditorGUILayout.Popup("Binding", selectedBindingIndex, labels);

        var resolvedDisplay = action.GetBindingDisplayString(
            selectedBindingIndex,
            out var deviceLayout,
            out var controlPath,
            InputBinding.DisplayStringOptions.DontUseShortDisplayNames);

        EditorGUILayout.LabelField("Resolved Display", string.IsNullOrWhiteSpace(resolvedDisplay) ? "(empty)" : resolvedDisplay);
        EditorGUILayout.LabelField("Resolved Layout", string.IsNullOrWhiteSpace(deviceLayout) ? "(empty)" : deviceLayout);
        EditorGUILayout.LabelField("Resolved Path", string.IsNullOrWhiteSpace(controlPath) ? "(empty)" : controlPath);

        useWildcardLayout = EditorGUILayout.Toggle("Use Wildcard Layout (*)", useWildcardLayout);
        using (new EditorGUI.DisabledScope(useWildcardLayout))
        {
            layoutOverride = EditorGUILayout.TextField("Layout Override", layoutOverride);
        }

        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(controlPath)))
        {
            if (GUILayout.Button("Add/Update Entry"))
            {
                AddOrUpdateEntry(controlPath, deviceLayout, sourceIcon);
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private string[] BuildBindingLabels(InputAction action)
    {
        var labels = new string[action.bindings.Count];
        for (var i = 0; i < action.bindings.Count; i++)
        {
            var binding = action.bindings[i];
            var type = binding.isComposite ? "Composite" : binding.isPartOfComposite ? "Part" : "Binding";
            var bindingName = string.IsNullOrWhiteSpace(binding.name) ? "-" : binding.name;
            var bindingPath = string.IsNullOrWhiteSpace(binding.path) ? "-" : binding.path;
            labels[i] = $"{i} | {type} | {bindingName} | {bindingPath}";
        }

        return labels;
    }

    private void AddOrUpdateEntry(string controlPath, string resolvedLayout, Sprite icon)
    {
        var entriesProperty = serializedObject.FindProperty("entries");
        if (entriesProperty == null)
        {
            Debug.LogError("[InputBindingIconLibraryEditor] Could not find 'entries' property.");
            return;
        }

        var targetLayout = ResolveTargetLayout(resolvedLayout);
        var existingIndex = FindEntry(entriesProperty, targetLayout, controlPath);

        SerializedProperty entry;
        if (existingIndex >= 0)
        {
            entry = entriesProperty.GetArrayElementAtIndex(existingIndex);
        }
        else
        {
            entriesProperty.InsertArrayElementAtIndex(entriesProperty.arraySize);
            entry = entriesProperty.GetArrayElementAtIndex(entriesProperty.arraySize - 1);
        }

        entry.FindPropertyRelative("deviceLayout").stringValue = targetLayout;
        entry.FindPropertyRelative("controlPath").stringValue = controlPath;
        entry.FindPropertyRelative("icon").objectReferenceValue = icon;

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);

        Debug.Log($"[InputBindingIconLibraryEditor] {(existingIndex >= 0 ? "Updated" : "Added")} entry: layout='{targetLayout}', path='{controlPath}'.");
    }

    private string ResolveTargetLayout(string resolvedLayout)
    {
        if (useWildcardLayout)
        {
            return "*";
        }

        if (!string.IsNullOrWhiteSpace(layoutOverride))
        {
            return layoutOverride.Trim();
        }

        return string.IsNullOrWhiteSpace(resolvedLayout) ? "*" : resolvedLayout.Trim();
    }

    private static int FindEntry(SerializedProperty entries, string layout, string path)
    {
        for (var i = 0; i < entries.arraySize; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i);
            var existingLayout = entry.FindPropertyRelative("deviceLayout").stringValue;
            var existingPath = entry.FindPropertyRelative("controlPath").stringValue;

            if (string.Equals(existingLayout, layout, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(existingPath, path, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
}
}
