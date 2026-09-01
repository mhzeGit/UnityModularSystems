using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ModularNPC.Editor
{
    /// <summary>Shared helpers for resolving the animation feature's serialized context.</summary>
    internal static class NpcAnimationDrawerUtility
    {
        public static SerializedProperty GetParent(SerializedProperty property)
        {
            string path = property.propertyPath;
            int lastDot = path.LastIndexOf('.');
            if (lastDot < 0)
            {
                return null;
            }

            return property.serializedObject.FindProperty(path.Substring(0, lastDot));
        }

        public static SerializedProperty FindSibling(SerializedProperty property, string name)
        {
            string path = property.propertyPath;
            int lastDot = path.LastIndexOf('.');
            if (lastDot < 0)
            {
                return null;
            }

            return property.serializedObject.FindProperty(path.Substring(0, lastDot + 1) + name);
        }

        /// <summary>Walks up from a binding property to the serialized NpcAnimation feature root.</summary>
        public static SerializedProperty FindFeatureRoot(SerializedProperty property)
        {
            SerializedProperty current = property.Copy();
            while (current != null && current.depth > 0)
            {
                if (current.name == "_stateBindings")
                {
                    return GetParent(current);
                }

                current = GetParent(current);
            }

            return null;
        }

        /// <summary>Returns the assigned animator, or the auto-detected one when applicable.</summary>
        public static Animator GetEffectiveAnimator(SerializedProperty property)
        {
            SerializedProperty featureRoot = FindFeatureRoot(property);
            if (featureRoot == null)
            {
                return null;
            }

            SerializedProperty animatorProperty = featureRoot.FindPropertyRelative("_animator");
            SerializedProperty autoDetectProperty = featureRoot.FindPropertyRelative("_autoDetectAnimator");
            return Resolve(property, animatorProperty, autoDetectProperty);
        }

        private static Animator Resolve(
            SerializedProperty context,
            SerializedProperty animatorProperty,
            SerializedProperty autoDetectProperty)
        {
            if (animatorProperty == null)
            {
                return null;
            }

            Animator assigned = animatorProperty.objectReferenceValue as Animator;
            if (assigned != null)
            {
                return assigned;
            }

            if (autoDetectProperty != null && autoDetectProperty.boolValue)
            {
                UnityEngine.Object target = context.serializedObject.targetObject;
                if (target is Npc npc)
                {
                    return npc.GetComponentInChildren<Animator>(true);
                }
            }

            return null;
        }

        public static AnimatorControllerParameterType ToAnimatorParameterType(NpcAnimationParameterType type)
        {
            switch (type)
            {
                case NpcAnimationParameterType.Int:
                    return AnimatorControllerParameterType.Int;

                case NpcAnimationParameterType.Bool:
                    return AnimatorControllerParameterType.Bool;

                case NpcAnimationParameterType.Trigger:
                    return AnimatorControllerParameterType.Trigger;

                default:
                    return AnimatorControllerParameterType.Float;
            }
        }

        public static NpcAnimationParameterType FromAnimatorParameterType(AnimatorControllerParameterType type)
        {
            switch (type)
            {
                case AnimatorControllerParameterType.Int:
                    return NpcAnimationParameterType.Int;

                case AnimatorControllerParameterType.Bool:
                    return NpcAnimationParameterType.Bool;

                case AnimatorControllerParameterType.Trigger:
                    return NpcAnimationParameterType.Trigger;

                default:
                    return NpcAnimationParameterType.Float;
            }
        }
    }

    /// <summary>Draws the animator field with auto-detection and a live status label.</summary>
    [CustomPropertyDrawer(typeof(NpcAnimatorFieldAttribute))]
    public sealed class NpcAnimatorFieldDrawer : PropertyDrawer
    {
        private static GUIStyle _statusStyle;

        private static GUIStyle StatusStyle
        {
            get
            {
                if (_statusStyle == null)
                {
                    _statusStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleRight,
                        wordWrap = false
                    };
                    _statusStyle.normal.textColor = EditorGUIUtility.isProSkin
                        ? new Color(0.65f, 0.72f, 0.78f)
                        : new Color(0.30f, 0.38f, 0.45f);
                }

                return _statusStyle;
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 2f + EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty autoDetectProperty = NpcAnimationDrawerUtility.FindSibling(property, "_autoDetectAnimator");
            if (autoDetectProperty == null)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            Rect row1 = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(row1, property, new GUIContent("Animator"));

            Rect row2 = new Rect(
                position.x,
                row1.yMax + EditorGUIUtility.standardVerticalSpacing,
                position.width,
                EditorGUIUtility.singleLineHeight);

            Rect toggleRect = new Rect(row2.x, row2.y, 20f, row2.height);
            bool autoDetect = autoDetectProperty.boolValue;
            bool newAutoDetect = EditorGUI.Toggle(toggleRect, GUIContent.none, autoDetect);
            if (newAutoDetect != autoDetect)
            {
                autoDetectProperty.boolValue = newAutoDetect;
                autoDetect = newAutoDetect;
            }

            EditorGUI.LabelField(
                new Rect(toggleRect.xMax, row2.y, 175f, row2.height),
                "Auto-detect in self/children");

            string status = GetStatus(property, autoDetect);
            Rect statusRect = new Rect(row2.xMax - 220f, row2.y, 220f, row2.height);
            EditorGUI.LabelField(statusRect, status, StatusStyle);
        }

        private static string GetStatus(SerializedProperty property, bool autoDetect)
        {
            Animator assigned = property.objectReferenceValue as Animator;
            if (assigned != null)
            {
                return "Assigned";
            }

            if (!autoDetect)
            {
                return "Manual only";
            }

            UnityEngine.Object target = property.serializedObject.targetObject;
            if (target is Npc npc)
            {
                Animator detected = npc.GetComponentInChildren<Animator>(true);
                if (detected != null)
                {
                    if (!Application.isPlaying)
                    {
                        property.objectReferenceValue = detected;
                    }

                    return $"Auto-detected: {GetShortPath(detected.transform)}";
                }
            }

            return "No Animator found";
        }

        private static string GetShortPath(Transform transform)
        {
            List<string> segments = new List<string>(4);
            Transform current = transform;
            while (current != null && segments.Count < 4)
            {
                segments.Insert(0, current.name);
                current = current.parent;
            }

            return string.Join("/", segments.ToArray());
        }
    }

    /// <summary>Draws the per-state binding cards for the animation feature.</summary>
    [CustomPropertyDrawer(typeof(NpcAnimationStatesFieldAttribute))]
    public sealed class NpcAnimationStatesFieldDrawer : PropertyDrawer
    {
        private static GUIStyle _headerStyle;

        private static GUIStyle HeaderStyle
        {
            get
            {
                if (_headerStyle == null)
                {
                    _headerStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                    {
                        fontStyle = FontStyle.Bold
                    };
                }

                return _headerStyle;
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight + 2f;

            if (NpcAnimationDrawerUtility.GetEffectiveAnimator(property) == null)
            {
                height += EditorGUIUtility.standardVerticalSpacing + GetHelpBoxHeight();
            }

            for (int i = 0; i < property.arraySize; i++)
            {
                height += EditorGUIUtility.standardVerticalSpacing;
                height += EditorGUI.GetPropertyHeight(
                    property.GetArrayElementAtIndex(i),
                    GUIContent.none,
                    true);
            }

            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            Rect headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            string headerText = !string.IsNullOrEmpty(label.text)
                ? label.text
                : "State Bindings";
            EditorGUI.LabelField(headerRect, headerText, HeaderStyle);

            float y = headerRect.yMax + 2f;

            Animator animator = NpcAnimationDrawerUtility.GetEffectiveAnimator(property);
            if (animator == null)
            {
                Rect helpRect = new Rect(position.x, y, position.width, GetHelpBoxHeight());
                EditorGUI.HelpBox(
                    helpRect,
                    "No Animator assigned or detected. Parameters cannot be listed until an Animator with a controller is available.",
                    MessageType.Warning);
                y = helpRect.yMax + EditorGUIUtility.standardVerticalSpacing;
            }

            for (int i = 0; i < property.arraySize; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                float elementHeight = EditorGUI.GetPropertyHeight(element, GUIContent.none, true);
                Rect elementRect = new Rect(position.x, y, position.width, elementHeight);
                EditorGUI.PropertyField(elementRect, element, GUIContent.none, true);
                y = elementRect.yMax + EditorGUIUtility.standardVerticalSpacing;
            }
        }

        private static float GetHelpBoxHeight()
        {
            return Mathf.Max(
                EditorGUIUtility.singleLineHeight * 2f,
                EditorStyles.helpBox.CalcHeight(
                    new GUIContent("No Animator assigned or detected. Parameters cannot be listed until an Animator with a controller is available."),
                    Mathf.Max(200f, EditorGUIUtility.currentViewWidth - 40f)));
        }
    }

    /// <summary>Draws one NPC state as a foldout card containing parameter binding rows.</summary>
    [CustomPropertyDrawer(typeof(NpcAnimationStateBinding))]
    public sealed class NpcAnimationStateBindingDrawer : PropertyDrawer
    {
        private static GUIStyle _foldoutStyle;

        private static GUIStyle FoldoutStyle
        {
            get
            {
                if (_foldoutStyle == null)
                {
                    _foldoutStyle = new GUIStyle(EditorStyles.foldout)
                    {
                        fontStyle = FontStyle.Bold
                    };
                }

                return _foldoutStyle;
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded)
            {
                return height;
            }

            SerializedProperty parameters = property.FindPropertyRelative("_parameters");
            for (int i = 0; i < parameters.arraySize; i++)
            {
                height += EditorGUIUtility.standardVerticalSpacing;
                height += EditorGUI.GetPropertyHeight(
                    parameters.GetArrayElementAtIndex(i),
                    GUIContent.none,
                    true);
            }

            height += EditorGUIUtility.standardVerticalSpacing + EditorGUIUtility.singleLineHeight;
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty stateProperty = property.FindPropertyRelative("_state");
            SerializedProperty parameters = property.FindPropertyRelative("_parameters");

            Rect headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            string stateName = ObjectNames.NicifyVariableName(
                ((NpcNavigationState)stateProperty.enumValueIndex).ToString());
            int count = parameters.arraySize;
            string headerText = count == 0
                ? stateName
                : $"{stateName}  ·  {count} parameter{(count == 1 ? string.Empty : "s")}";

            property.isExpanded = EditorGUI.Foldout(
                headerRect,
                property.isExpanded,
                new GUIContent(headerText),
                true,
                FoldoutStyle);

            if (!property.isExpanded)
            {
                return;
            }

            const float contentIndent = 18f;
            float y = headerRect.yMax + EditorGUIUtility.standardVerticalSpacing;

            for (int i = 0; i < parameters.arraySize; i++)
            {
                SerializedProperty element = parameters.GetArrayElementAtIndex(i);
                float elementHeight = EditorGUI.GetPropertyHeight(element, GUIContent.none, true);
                Rect elementRect = new Rect(
                    position.x + contentIndent,
                    y,
                    position.width - contentIndent,
                    elementHeight);
                EditorGUI.PropertyField(elementRect, element, GUIContent.none, true);
                y = elementRect.yMax + EditorGUIUtility.standardVerticalSpacing;
            }

            Rect buttonRect = new Rect(
                position.x + contentIndent,
                y,
                120f,
                EditorGUIUtility.singleLineHeight);
            if (GUI.Button(buttonRect, "+ Add Parameter"))
            {
                parameters.InsertArrayElementAtIndex(parameters.arraySize);
                SerializedProperty added = parameters.GetArrayElementAtIndex(parameters.arraySize - 1);
                added.FindPropertyRelative("_parameterName").stringValue = string.Empty;
                added.FindPropertyRelative("_parameterType").enumValueIndex = 0;
                property.isExpanded = true;
            }
        }
    }

    /// <summary>
    /// Draws one animator parameter binding: a type-filtered parameter dropdown and a value field.
    /// The dropdown is rebuilt from the Animator every repaint, so it stays in sync with the controller.
    /// </summary>
    [CustomPropertyDrawer(typeof(NpcAnimationParameterBinding))]
    public sealed class NpcAnimationParameterBindingDrawer : PropertyDrawer
    {
        private const string NoneOption = "— None —";
        private const string MissingSuffix = " (missing)";

        private static GUIStyle _warningStyle;

        private static GUIStyle WarningStyle
        {
            get
            {
                if (_warningStyle == null)
                {
                    _warningStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        wordWrap = false,
                        alignment = TextAnchor.MiddleLeft
                    };
                    _warningStyle.normal.textColor = EditorGUIUtility.isProSkin
                        ? new Color(1f, 0.78f, 0.30f)
                        : new Color(0.75f, 0.45f, 0.05f);
                }

                return _warningStyle;
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return HasWarning(property)
                ? EditorGUIUtility.singleLineHeight * 2f + EditorGUIUtility.standardVerticalSpacing
                : EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty nameProperty = property.FindPropertyRelative("_parameterName");
            SerializedProperty typeProperty = property.FindPropertyRelative("_parameterType");
            SerializedProperty floatProperty = property.FindPropertyRelative("_floatValue");
            SerializedProperty intProperty = property.FindPropertyRelative("_intValue");
            SerializedProperty boolProperty = property.FindPropertyRelative("_boolValue");

            Animator animator = NpcAnimationDrawerUtility.GetEffectiveAnimator(property);

            Rect rowRect = position;
            rowRect.height = EditorGUIUtility.singleLineHeight;

            const float typeWidth = 64f;
            const float valueWidth = 72f;
            const float spacing = 4f;

            Rect typeRect = new Rect(rowRect.x, rowRect.y, typeWidth, rowRect.height);
            Rect valueRect = new Rect(rowRect.xMax - valueWidth, rowRect.y, valueWidth, rowRect.height);
            Rect parameterRect = new Rect(
                typeRect.xMax + spacing,
                rowRect.y,
                valueRect.x - typeRect.xMax - spacing * 2f,
                rowRect.height);

            EditorGUI.BeginProperty(rowRect, GUIContent.none, property);

            NpcAnimationParameterType type = (NpcAnimationParameterType)typeProperty.enumValueIndex;

            NpcAnimationParameterType newType = (NpcAnimationParameterType)EditorGUI.EnumPopup(
                typeRect,
                GUIContent.none,
                type);
            if (newType != type)
            {
                typeProperty.enumValueIndex = (int)newType;
                string currentName = nameProperty.stringValue;
                if (!string.IsNullOrEmpty(currentName) &&
                    !ParameterMatchesType(animator, currentName, newType))
                {
                    nameProperty.stringValue = string.Empty;
                }

                type = newType;
            }

            GUIContent[] options = BuildParameterOptions(
                animator,
                type,
                nameProperty.stringValue,
                out int selectedIndex);
            int newSelectedIndex = EditorGUI.Popup(
                parameterRect,
                GUIContent.none,
                selectedIndex,
                options);
            if (newSelectedIndex != selectedIndex)
            {
                if (newSelectedIndex == 0)
                {
                    nameProperty.stringValue = string.Empty;
                }
                else
                {
                    string picked = CleanOptionName(options[newSelectedIndex].text);
                    nameProperty.stringValue = picked;
                    AnimatorControllerParameterType actualType = FindParameterType(animator, picked);
                    if (actualType != (AnimatorControllerParameterType)(-1))
                    {
                        type = NpcAnimationDrawerUtility.FromAnimatorParameterType(actualType);
                        typeProperty.enumValueIndex = (int)type;
                    }
                }
            }

            switch (type)
            {
                case NpcAnimationParameterType.Float:
                    floatProperty.floatValue = EditorGUI.FloatField(valueRect, floatProperty.floatValue);
                    break;

                case NpcAnimationParameterType.Int:
                    intProperty.intValue = EditorGUI.IntField(valueRect, intProperty.intValue);
                    break;

                case NpcAnimationParameterType.Bool:
                    boolProperty.boolValue = EditorGUI.Toggle(valueRect, boolProperty.boolValue);
                    break;

                case NpcAnimationParameterType.Trigger:
                    EditorGUI.LabelField(valueRect, "on enter");
                    break;
            }

            EditorGUI.EndProperty();

            if (GetWarning(animator, nameProperty.stringValue, type, out string warning))
            {
                Rect warningRect = new Rect(
                    position.x + 10f,
                    rowRect.yMax + EditorGUIUtility.standardVerticalSpacing,
                    position.width - 10f,
                    EditorGUIUtility.singleLineHeight);
                EditorGUI.LabelField(warningRect, warning, WarningStyle);
            }
        }

        private static bool ParameterMatchesType(Animator animator, string name, NpcAnimationParameterType type)
        {
            AnimatorControllerParameterType actualType = FindParameterType(animator, name);
            return actualType != (AnimatorControllerParameterType)(-1) &&
                   actualType == NpcAnimationDrawerUtility.ToAnimatorParameterType(type);
        }

        private static AnimatorControllerParameterType FindParameterType(Animator animator, string name)
        {
            if (animator == null)
            {
                return (AnimatorControllerParameterType)(-1);
            }

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name == name)
                {
                    return parameters[i].type;
                }
            }

            return (AnimatorControllerParameterType)(-1);
        }

        private static GUIContent[] BuildParameterOptions(
            Animator animator,
            NpcAnimationParameterType type,
            string currentName,
            out int selectedIndex)
        {
            List<string> names = new List<string>(16) { NoneOption };
            selectedIndex = 0;
            bool found = string.IsNullOrEmpty(currentName);

            if (animator != null)
            {
                AnimatorControllerParameter[] parameters = animator.parameters;
                AnimatorControllerParameterType animatorType =
                    NpcAnimationDrawerUtility.ToAnimatorParameterType(type);
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (parameters[i].type != animatorType)
                    {
                        continue;
                    }

                    names.Add(parameters[i].name);
                    if (!found && parameters[i].name == currentName)
                    {
                        selectedIndex = names.Count - 1;
                        found = true;
                    }
                }
            }

            if (!found)
            {
                names.Add(currentName + MissingSuffix);
                selectedIndex = names.Count - 1;
            }

            GUIContent[] contents = new GUIContent[names.Count];
            for (int i = 0; i < names.Count; i++)
            {
                contents[i] = new GUIContent(names[i]);
            }

            return contents;
        }

        private static string CleanOptionName(string text)
        {
            if (text.EndsWith(MissingSuffix, StringComparison.Ordinal))
            {
                return text.Substring(0, text.Length - MissingSuffix.Length);
            }

            return text;
        }

        private static bool HasWarning(SerializedProperty property)
        {
            SerializedProperty nameProperty = property.FindPropertyRelative("_parameterName");
            string name = nameProperty.stringValue;
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            Animator animator = NpcAnimationDrawerUtility.GetEffectiveAnimator(property);
            NpcAnimationParameterType type =
                (NpcAnimationParameterType)property.FindPropertyRelative("_parameterType").enumValueIndex;
            return GetWarning(animator, name, type, out _);
        }

        private static bool GetWarning(
            Animator animator,
            string name,
            NpcAnimationParameterType type,
            out string message)
        {
            message = null;
            if (animator == null)
            {
                return false;
            }

            AnimatorControllerParameterType boundType =
                NpcAnimationDrawerUtility.ToAnimatorParameterType(type);
            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name != name)
                {
                    continue;
                }

                if (parameters[i].type != boundType)
                {
                    message = $"\"{name}\" is {parameters[i].type}, not {type}.";
                    return true;
                }

                return false;
            }

            message = $"Parameter \"{name}\" was not found on the Animator.";
            return true;
        }
    }
}
