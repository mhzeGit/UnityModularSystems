// Custom inspector for GlobalVariableReader: detects every GlobalVariable asset created by GlobalVariableWriter in the project, lets the user pick one from a dropdown, and shows its current default value and type.

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MHZE.GlobalVariables.Editor
{
    [CustomEditor(typeof(GlobalVariableReader))]
    public class GlobalVariableReaderEditor : UnityEditor.Editor
    {
        SerializedProperty globalVariableProperty;

        void OnEnable()
        {
            globalVariableProperty = serializedObject.FindProperty("globalVariable");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            List<GlobalVariable> allVariables = FindAllGlobalVariables();
            string[] options = new string[allVariables.Count + 1];
            options[0] = "- None -";
            for (int i = 0; i < allVariables.Count; i++)
                options[i + 1] = allVariables[i].DisplayName;

            GlobalVariable current = globalVariableProperty.objectReferenceValue as GlobalVariable;
            int currentIndex = allVariables.IndexOf(current);
            int selectedIndex = EditorGUILayout.Popup("Global Variable", currentIndex + 1, options);

            if (selectedIndex == 0 && current != null)
                globalVariableProperty.objectReferenceValue = null;
            else if (selectedIndex > 0 && selectedIndex - 1 != currentIndex)
                globalVariableProperty.objectReferenceValue = allVariables[selectedIndex - 1];

            current = globalVariableProperty.objectReferenceValue as GlobalVariable;
            if (current != null)
            {
                EditorGUILayout.LabelField("Current Value", current.ValueString);
                EditorGUILayout.LabelField("Value Type", GetShortTypeName(current.ValueTypeName));
                EditorGUILayout.HelpBox("The default value is kept in sync automatically whenever a GlobalVariableWriter is assigned to this variable. Read or set it at runtime with GetValue / SetValue.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Pick a global variable created by a GlobalVariableWriter. Any writer assigned to the same variable keeps its default value in sync automatically.", MessageType.Info);
            }

            serializedObject.ApplyModifiedProperties();
        }

        static List<GlobalVariable> FindAllGlobalVariables()
        {
            List<GlobalVariable> result = new List<GlobalVariable>();

            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(GlobalVariable)}"))
            {
                GlobalVariable variable = AssetDatabase.LoadAssetAtPath<GlobalVariable>(AssetDatabase.GUIDToAssetPath(guid));
                if (variable != null)
                    result.Add(variable);
            }

            result.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
            return result;
        }

        static string GetShortTypeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return "unknown";

            Type type = Type.GetType(typeName);
            return type != null ? type.Name : typeName;
        }
    }
}
