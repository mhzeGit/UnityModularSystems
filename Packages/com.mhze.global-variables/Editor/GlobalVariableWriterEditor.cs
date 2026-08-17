// Custom inspector for GlobalVariableWriter: assigns a source script and a BindableValue variable. When a new variable is assigned, the GlobalVariable asset is automatically created (or found) in the project, its default value is synced from the source, and everything is saved.

using System;
using System.Collections.Generic;
using System.Reflection;
using MHZE.TextWrite;
using UnityEditor;
using UnityEngine;

namespace MHZE.GlobalVariables.Editor
{
    [CustomEditor(typeof(GlobalVariableWriter))]
    public class GlobalVariableWriterEditor : UnityEditor.Editor
    {
        const string GlobalVariablesFolder = "Assets/GlobalVariables";

        SerializedProperty sourceProperty;
        SerializedProperty variableNameProperty;
        SerializedProperty globalVariableProperty;

        GlobalVariableWriter writer;

        void OnEnable()
        {
            writer = (GlobalVariableWriter)target;

            sourceProperty = serializedObject.FindProperty("sourceComponent");
            variableNameProperty = serializedObject.FindProperty("variableName");
            globalVariableProperty = serializedObject.FindProperty("globalVariable");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawSourceSection();

            MonoBehaviour source = sourceProperty.objectReferenceValue as MonoBehaviour;
            if (source != null && source != writer && !string.IsNullOrEmpty(variableNameProperty.stringValue))
                EnsureAsset(source, variableNameProperty.stringValue);

            DrawAssetSection();

            serializedObject.ApplyModifiedProperties();
        }

        void DrawSourceSection()
        {
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(sourceProperty);

            MonoBehaviour source = sourceProperty.objectReferenceValue as MonoBehaviour;
            if (source == null)
            {
                EditorGUILayout.HelpBox("Assign any script (MonoBehaviour) that has BindableValue fields, e.g. 'public readonly BindableValue<int> Gold = new BindableValue<int>(0);'.", MessageType.Info);
                return;
            }

            if (source == writer)
            {
                EditorGUILayout.HelpBox("The source cannot be the GlobalVariableWriter itself.", MessageType.Error);
                return;
            }

            List<string> memberNames = GetBindableMemberNames(source);
            if (memberNames.Count == 0)
            {
                EditorGUILayout.HelpBox($"'{source.GetType().Name}' has no BindableValue members. Add BindableValue<T> fields to the script to make them selectable here.", MessageType.Warning);
                return;
            }

            string[] options = new string[memberNames.Count + 1];
            options[0] = "- None -";
            memberNames.CopyTo(options, 1);

            int currentIndex = memberNames.IndexOf(variableNameProperty.stringValue);
            int selectedIndex = EditorGUILayout.Popup("Variable", currentIndex + 1, options);

            if (selectedIndex == 0 && currentIndex >= 0)
                variableNameProperty.stringValue = string.Empty;
            else if (selectedIndex > 0 && selectedIndex - 1 != currentIndex)
                variableNameProperty.stringValue = memberNames[selectedIndex - 1];
        }

        void DrawAssetSection()
        {
            EditorGUILayout.LabelField("Global Variable", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(globalVariableProperty);

            GlobalVariable variable = globalVariableProperty.objectReferenceValue as GlobalVariable;
            if (variable == null)
            {
                if (sourceProperty.objectReferenceValue != null && !string.IsNullOrEmpty(variableNameProperty.stringValue))
                    EditorGUILayout.HelpBox("Select a variable above — the GlobalVariable asset is created automatically.", MessageType.Info);

                return;
            }

            EditorGUILayout.LabelField("Asset", AssetDatabase.GetAssetPath(variable));
            EditorGUILayout.LabelField("Current Value", variable.ValueString);
            EditorGUILayout.LabelField("Value Type", GetShortTypeName(variable.ValueTypeName));

            if (GUILayout.Button("Sync Value From Source"))
            {
                writer.SyncNow();
                EditorUtility.SetDirty(variable);
                AssetDatabase.SaveAssets();
            }

            if (GUILayout.Button("Delete Asset"))
            {
                string path = AssetDatabase.GetAssetPath(variable);
                if (!string.IsNullOrEmpty(path))
                    AssetDatabase.DeleteAsset(path);

                globalVariableProperty.objectReferenceValue = null;
            }
        }

        void EnsureAsset(MonoBehaviour source, string memberName)
        {
            string assetName = $"{source.GetType().Name}.{memberName}";
            string path = $"{GlobalVariablesFolder}/{assetName}.asset";

            GlobalVariable variable = globalVariableProperty.objectReferenceValue as GlobalVariable;
            if (variable != null && variable.DisplayName == assetName)
                return;

            if (!AssetDatabase.IsValidFolder(GlobalVariablesFolder))
                AssetDatabase.CreateFolder("Assets", "GlobalVariables");

            variable = AssetDatabase.LoadAssetAtPath<GlobalVariable>(path);
            if (variable == null)
            {
                variable = ScriptableObject.CreateInstance<GlobalVariable>();
                AssetDatabase.CreateAsset(variable, path);
            }

            variable.SetDisplayName(assetName);

            Type valueType = GetBoundValueType(source, memberName);
            if (valueType != null)
                variable.SetValueType(valueType);

            globalVariableProperty.objectReferenceValue = variable;
            serializedObject.ApplyModifiedProperties();

            writer.SyncNow();
            EditorUtility.SetDirty(variable);
            AssetDatabase.SaveAssets();
        }

        static List<string> GetBindableMemberNames(MonoBehaviour component)
        {
            List<string> names = new List<string>();
            Type type = component.GetType();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

            foreach (FieldInfo field in type.GetFields(flags))
            {
                if (typeof(BindableValue).IsAssignableFrom(field.FieldType))
                    names.Add(field.Name);
            }

            foreach (PropertyInfo property in type.GetProperties(flags))
            {
                if (typeof(BindableValue).IsAssignableFrom(property.PropertyType) && property.CanRead)
                    names.Add(property.Name);
            }

            names.Sort(StringComparer.Ordinal);
            return names;
        }

        static Type GetBoundValueType(MonoBehaviour component, string memberName)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
            Type componentType = component.GetType();

            FieldInfo field = componentType.GetField(memberName, flags);
            PropertyInfo property = field == null ? componentType.GetProperty(memberName, flags) : null;

            Type memberType = field != null ? field.FieldType : (property != null ? property.PropertyType : null);
            if (memberType != null && memberType.IsGenericType && memberType.GetGenericTypeDefinition() == typeof(BindableValue<>))
                return memberType.GetGenericArguments()[0];

            return null;
        }

        static string GetShortTypeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return "unknown";

            Type type = Type.GetType(typeName);
            return type != null ? type.Name : typeName;
        }
    }
}
