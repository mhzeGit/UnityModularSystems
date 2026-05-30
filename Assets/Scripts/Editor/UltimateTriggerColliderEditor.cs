using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace MHZE.Editor
{
    [CustomEditor(typeof(UltimateTriggerCollider))]
    public class UltimateTriggerColliderEditor : UnityEditor.Editor
    {
        private SerializedProperty _targetTags;
        private SerializedProperty _useOnEnter;
        private SerializedProperty _useOnStay;
        private SerializedProperty _useOnExit;
        private SerializedProperty _onEnter;
        private SerializedProperty _onStay;
        private SerializedProperty _onExit;
        private SerializedProperty _showDebugPreview;
        private ReorderableList _tagList;

        private void OnEnable()
        {
            _targetTags = serializedObject.FindProperty("_targetTags");
            _useOnEnter = serializedObject.FindProperty("_useOnEnter");
            _useOnStay = serializedObject.FindProperty("_useOnStay");
            _useOnExit = serializedObject.FindProperty("_useOnExit");
            _onEnter = serializedObject.FindProperty("OnEnter");
            _onStay = serializedObject.FindProperty("OnStay");
            _onExit = serializedObject.FindProperty("OnExit");
            _showDebugPreview = serializedObject.FindProperty("_showDebugPreview");

            _tagList = new ReorderableList(serializedObject, _targetTags, true, false, true, true)
            {
                drawElementCallback = DrawTagElement,
                onAddCallback = OnAddTag,
                elementHeight = EditorGUIUtility.singleLineHeight + 2,
                footerHeight = 0,
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawSectionHeader("Tag Filtering");
            EditorGUI.indentLevel++;
            _tagList.DoLayoutList();
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(20);

            DrawSectionHeader("Trigger Events");
            DrawEventToggle(_useOnEnter, _onEnter, "On Enter");
            DrawEventToggle(_useOnStay, _onStay, "On Stay");
            DrawEventToggle(_useOnExit, _onExit, "On Exit");

            DrawSectionHeader("Debug");
            EditorGUILayout.PropertyField(_showDebugPreview);

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawSectionHeader(string label)
        {
            EditorGUILayout.Space(6);
            Rect r = EditorGUILayout.GetControlRect(false, 2);
            r.height = 1;
            r.x += 4;
            r.width -= 8;
            EditorGUI.DrawRect(r, new Color(0.35f, 0.35f, 0.35f, 0.3f));
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        }

        private void DrawTagElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            SerializedProperty element = _targetTags.GetArrayElementAtIndex(index);
            rect.y += 1;
            rect.height = EditorGUIUtility.singleLineHeight;
            element.stringValue = EditorGUI.TagField(rect, element.stringValue);
        }

        private void OnAddTag(ReorderableList list)
        {
            _targetTags.InsertArrayElementAtIndex(_targetTags.arraySize);
            _targetTags.GetArrayElementAtIndex(_targetTags.arraySize - 1).stringValue = "Untagged";
        }

        private static void DrawEventToggle(SerializedProperty toggle, SerializedProperty eventProp, string label)
        {
            toggle.boolValue = EditorGUILayout.ToggleLeft(label, toggle.boolValue, EditorStyles.boldLabel);
            if (toggle.boolValue)
            {
                EditorGUI.indentLevel += 2;
                EditorGUILayout.PropertyField(eventProp);
                EditorGUI.indentLevel -= 2;
            }
        }
    }
}
