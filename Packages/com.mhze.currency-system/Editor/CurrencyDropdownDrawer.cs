// Inspector dropdown for string fields marked with [Currency]. The options are
// read live from the CurrencyDatabase (a ScriptableObject list of strings), so
// adding a currency name in the database immediately updates every dropdown.

using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MHZE.CurrencySystem.Editor
{
    [CustomPropertyDrawer(typeof(CurrencyAttribute))]
    public class CurrencyDropdownDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            CurrencyDatabase database = ResolveDatabase(property);
            if (database == null || database.Count == 0)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            int currentIndex = Mathf.Max(0, database.GetIndex(property.stringValue));
            string[] options = new string[database.Count];
            for (int i = 0; i < database.Count; i++)
                options[i] = database.GetName(i);

            Rect fieldRect = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
            int newIndex = EditorGUI.Popup(fieldRect, currentIndex, options);
            if (newIndex >= 0 && newIndex != currentIndex)
                property.stringValue = database.GetName(newIndex);
        }

        /// <summary>
        /// Resolves the database for the dropdown: first a "database" field on the
        /// component being edited, then the project default (Resources/CurrencyDatabase).
        /// </summary>
        private static CurrencyDatabase ResolveDatabase(SerializedProperty property)
        {
            if (property.serializedObject.targetObject is Component component)
            {
                FieldInfo databaseField = component.GetType().GetField("database",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (databaseField?.GetValue(component) is CurrencyDatabase assigned && assigned != null)
                    return assigned;
            }

            return CurrencyDatabase.Default;
        }
    }
}
