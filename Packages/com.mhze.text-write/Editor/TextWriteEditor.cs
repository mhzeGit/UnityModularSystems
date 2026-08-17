// Custom inspector for TextWrite: auto-detects the Text/TMP component on the GameObject, lets the user assign any script, and shows a dropdown of all BindableValue members on that script to bind the text to.

using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MHZE.TextWrite.Editor
{
    [CustomEditor(typeof(TextWrite))]
    public class TextWriteEditor : UnityEditor.Editor
    {
        SerializedProperty textProperty;
        SerializedProperty tmpTextProperty;
        SerializedProperty sourceProperty;
        SerializedProperty variableNameProperty;
        SerializedProperty formatProperty;

        TextWrite textWrite;

        void OnEnable()
        {
            textWrite = (TextWrite)target;

            textProperty = serializedObject.FindProperty("text");
            tmpTextProperty = serializedObject.FindProperty("tmpText");
            sourceProperty = serializedObject.FindProperty("sourceComponent");
            variableNameProperty = serializedObject.FindProperty("variableName");
            formatProperty = serializedObject.FindProperty("format");

            if (textProperty.objectReferenceValue == null && tmpTextProperty.objectReferenceValue == null)
            {
                textWrite.DetectTextComponent();
                EditorUtility.SetDirty(textWrite);
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawTextSection();
            DrawSourceSection();
            DrawFormatSection();

            serializedObject.ApplyModifiedProperties();
        }

        void DrawTextSection()
        {
            EditorGUILayout.LabelField("Text Component", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(textProperty);
            EditorGUILayout.PropertyField(tmpTextProperty);

            if (textProperty.objectReferenceValue == null && tmpTextProperty.objectReferenceValue == null)
            {
                if (GUILayout.Button("Detect Text Component"))
                {
                    textWrite.DetectTextComponent();
                    EditorUtility.SetDirty(textWrite);
                }
            }
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

            if (source == textWrite)
            {
                EditorGUILayout.HelpBox("The source cannot be the TextWrite itself.", MessageType.Error);
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

            if (selectedIndex == 0)
                variableNameProperty.stringValue = string.Empty;
            else if (selectedIndex != currentIndex + 1)
                variableNameProperty.stringValue = memberNames[selectedIndex - 1];
        }

        void DrawFormatSection()
        {
            EditorGUILayout.LabelField("Format", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(formatProperty);
            EditorGUILayout.HelpBox("Use {0} as the value placeholder, e.g. \"Gold: {0}\". The text refreshes automatically whenever the bound value changes — no Update polling.", MessageType.None);
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
    }
}
