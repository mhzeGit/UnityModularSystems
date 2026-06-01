using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(UltimateTriggerCollider))]
    public class UltimateTriggerColliderEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.style.paddingLeft = 4;
            root.style.paddingRight = 4;
            root.style.paddingTop = 4;

            var so = serializedObject;

            AddSectionHeader(root, "Tag Filtering");
            root.Add(new PropertyField(so.FindProperty("_targetTags"), "Target Tags"));

            AddSectionHeader(root, "Trigger Events");
            AddToggleGroup(root, so.FindProperty("_useOnEnter"), so.FindProperty("OnEnter"), "On Enter");
            AddToggleGroup(root, so.FindProperty("_useOnStay"), so.FindProperty("OnStay"), "On Stay");
            AddToggleGroup(root, so.FindProperty("_useOnExit"), so.FindProperty("OnExit"), "On Exit");

            AddSectionHeader(root, "Debug");
            root.Add(new PropertyField(so.FindProperty("_showDebugPreview")));

            root.Bind(so);
            return root;
        }

        private static void AddSectionHeader(VisualElement root, string label)
        {
            root.Add(new VisualElement { style = { height = 6 } });

            var line = new VisualElement();
            line.style.height = 1;
            line.style.marginLeft = 4;
            line.style.marginRight = 4;
            line.style.backgroundColor = new Color(0.35f, 0.35f, 0.35f, 0.3f);
            root.Add(line);

            var header = new Label(label);
            header.style.fontSize = 12;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginTop = 3;
            header.style.marginBottom = 3;
            root.Add(header);
        }

        private static void AddToggleGroup(VisualElement root, SerializedProperty toggleProp, SerializedProperty eventProp, string label)
        {
            var container = new VisualElement();

            var toggle = new Toggle(label);
            toggle.value = toggleProp.boolValue;
            toggle.style.unityFontStyleAndWeight = FontStyle.Bold;
            container.Add(toggle);

            var eventField = new PropertyField(eventProp);
            eventField.style.paddingLeft = 24;
            eventField.style.display = toggleProp.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
            container.Add(eventField);

            root.Add(container);

            toggle.RegisterValueChangedCallback(evt =>
            {
                toggleProp.boolValue = evt.newValue;
                toggleProp.serializedObject.ApplyModifiedProperties();
                eventField.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            });
        }
    }
