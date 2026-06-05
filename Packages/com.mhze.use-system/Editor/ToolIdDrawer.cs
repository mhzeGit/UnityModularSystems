using UnityEditor;
using UnityEngine;

namespace MHZE.UseSystem.Editor
{
    [CustomPropertyDrawer(typeof(ToolId))]
    public class ToolIdDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty valueProp = property.FindPropertyRelative("value");
            UseSystemDefinitions defs = FindDefinitions();

            if (defs == null || defs.toolNames == null || defs.toolNames.Length == 0)
            {
                EditorGUI.PropertyField(position, valueProp, label);
                return;
            }

            string currentValue = valueProp.stringValue;
            string[] options = defs.toolNames;
            int currentIndex = System.Array.IndexOf(options, currentValue);
            if (currentIndex < 0) currentIndex = 0;

            EditorGUI.BeginProperty(position, label, property);
            int newIndex = EditorGUI.Popup(position, label.text, currentIndex, options);
            if (newIndex != currentIndex)
            {
                valueProp.stringValue = options[newIndex];
            }
            EditorGUI.EndProperty();
        }

        static UseSystemDefinitions FindDefinitions()
        {
            string[] guids = AssetDatabase.FindAssets("t:UseSystemDefinitions");
            if (guids.Length == 0) return null;
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<UseSystemDefinitions>(path);
        }
    }
}
