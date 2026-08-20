using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ModularNPC.Editor
{
    [CustomEditor(typeof(Npc))]
    [CanEditMultipleObjects]
    public sealed class NpcEditor : UnityEditor.Editor
    {
        private static readonly List<Type> FeatureTypes = new List<Type>(32);
        private static bool _featureTypesCached;

        private readonly List<NpcFeature> _features = new List<NpcFeature>(16);
        private readonly List<NpcValidationIssue> _issues = new List<NpcValidationIssue>(16);
        private readonly HashSet<NpcFeature> _expandedFeatures = new HashSet<NpcFeature>();
        private readonly HashSet<NpcFeature> _knownFeatures = new HashSet<NpcFeature>();

        private SerializedProperty _featureModulesProperty;
        private GUIStyle _headerStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _categoryStyle;
        private GUIStyle _cardTitleStyle;
        private GUIStyle _badgeStyle;
        private bool _showValidation = true;

        private void OnEnable()
        {
            _featureModulesProperty = serializedObject.FindProperty("_featureModules");
        }

        public override void OnInspectorGUI()
        {
            EnsureStyles();

            if (targets.Length > 1)
            {
                EditorGUILayout.HelpBox(
                    "Internal feature management is available when a single NPC is selected.",
                    MessageType.Info);
                return;
            }

            Npc npc = (Npc)target;
            if (npc == null)
            {
                return;
            }

            serializedObject.Update();
            RefreshFeatures(npc);
            RefreshValidation(npc);
            DrawHeader(npc);
            DrawFeatures(npc);
            DrawValidation(npc, false);

            if (serializedObject.ApplyModifiedProperties())
            {
                npc.RefreshFeatures();
                EditorUtility.SetDirty(npc);
            }
        }

        [MenuItem("GameObject/Modular NPC/NPC", false, 10)]
        private static void CreateNpc(MenuCommand menuCommand)
        {
            GameObject gameObject = new GameObject("NPC");
            Undo.RegisterCreatedObjectUndo(gameObject, "Create NPC");
            if (menuCommand.context is GameObject parent)
            {
                GameObjectUtility.SetParentAndAlign(gameObject, parent);
            }

            Undo.AddComponent<Npc>(gameObject);
            Selection.activeGameObject = gameObject;
        }

        private void DrawHeader(Npc npc)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Modular NPC", _headerStyle);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        $"{_features.Count} internal feature{(_features.Count == 1 ? string.Empty : "s")} · " +
                        (Application.isPlaying
                            ? (npc.IsOperational ? "Operational" : "Inactive")
                            : "Edit Mode"),
                        _statusStyle);

                    GUILayout.FlexibleSpace();
                    DrawIssueBadge();
                }

                EditorGUILayout.LabelField(
                    "Capabilities are serialized inside this component; no feature scripts are attached.",
                    EditorStyles.wordWrappedMiniLabel);

                GUILayout.Space(3f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("+  Add Feature", GUILayout.Height(27f)))
                    {
                        ShowAddFeatureMenu(npc);
                    }

                    using (new EditorGUI.DisabledScope(_issues.Count == 0))
                    {
                        if (GUILayout.Button("Validation", GUILayout.Width(82f), GUILayout.Height(27f)))
                        {
                            _showValidation = true;
                        }
                    }
                }
            }
        }

        private void DrawFeatures(Npc npc)
        {
            if (_features.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "This is an empty NPC root. Added capabilities are stored internally in this one component.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.Space(3f);
            for (int i = 0; i < _features.Count; i++)
            {
                NpcFeature feature = _features[i];
                if (feature == null)
                {
                    continue;
                }

                SerializedProperty featureProperty = i < _featureModulesProperty.arraySize
                    ? _featureModulesProperty.GetArrayElementAtIndex(i)
                    : null;
                DrawFeatureCard(npc, feature, featureProperty, i);
            }
        }

        private void DrawFeatureCard(
            Npc npc,
            NpcFeature feature,
            SerializedProperty featureProperty,
            int featureIndex)
        {
            NpcFeatureAttribute definition = GetDefinition(feature.GetType());
            string title = definition != null
                ? definition.DisplayName
                : ObjectNames.NicifyVariableName(feature.GetType().Name);
            string category = definition != null ? definition.Category : "Custom";

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                Rect accentRect = EditorGUILayout.GetControlRect(false, 2f);
                EditorGUI.DrawRect(accentRect, GetCategoryColor(category));

                using (new EditorGUILayout.HorizontalScope())
                {
                    bool expanded = _expandedFeatures.Contains(feature);
                    GUIContent titleContent = new GUIContent(title, GetCategoryIcon(category));
                    bool newExpanded = EditorGUILayout.Foldout(expanded, titleContent, true, _cardTitleStyle);
                    if (newExpanded != expanded)
                    {
                        if (newExpanded)
                        {
                            _expandedFeatures.Add(feature);
                        }
                        else
                        {
                            _expandedFeatures.Remove(feature);
                        }
                    }

                    Color previousBackground = GUI.backgroundColor;
                    GUI.backgroundColor = feature.Enabled
                        ? new Color(0.55f, 0.85f, 0.62f)
                        : new Color(0.65f, 0.65f, 0.65f);
                    bool enabled = GUILayout.Toggle(
                        feature.Enabled,
                        feature.Enabled ? "ON" : "OFF",
                        EditorStyles.miniButton,
                        GUILayout.Width(38f));
                    GUI.backgroundColor = previousBackground;
                    if (enabled != feature.Enabled)
                    {
                        Undo.RecordObject(npc, enabled ? "Enable NPC Feature" : "Disable NPC Feature");
                        feature.Enabled = enabled;
                        EditorUtility.SetDirty(npc);
                    }

                    GUIContent menuContent = EditorGUIUtility.IconContent("_Menu");
                    menuContent.tooltip = $"{title} actions";
                    if (GUILayout.Button(menuContent, EditorStyles.miniButton, GUILayout.Width(24f)))
                    {
                        ShowFeatureActions(npc, feature, featureIndex, featureProperty);
                    }
                }

                EditorGUILayout.LabelField(category.ToUpperInvariant(), _categoryStyle);

                if (_expandedFeatures.Contains(feature))
                {
                    if (!string.IsNullOrEmpty(definition?.Description))
                    {
                        EditorGUILayout.LabelField(definition.Description, EditorStyles.wordWrappedMiniLabel);
                    }

                    if (Application.isPlaying)
                    {
                        EditorGUILayout.LabelField(
                            feature.IsOperational ? "Runtime status: Operational" : "Runtime status: Inactive",
                            _statusStyle);
                    }

                    EditorGUILayout.Space(2f);
                    DrawManagedReferenceChildren(featureProperty, feature.GetType());
                }
            }
        }

        private static void DrawManagedReferenceChildren(
            SerializedProperty managedReference,
            Type featureType)
        {
            if (managedReference == null)
            {
                EditorGUILayout.HelpBox("Serialized feature data is unavailable.", MessageType.Error);
                return;
            }

            SerializedProperty iterator = managedReference.Copy();
            SerializedProperty end = iterator.GetEndProperty();
            bool enterChildren = true;
            bool drewProperty = false;

            while (iterator.Next(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;
                if (iterator.depth != managedReference.depth + 1)
                {
                    continue;
                }

                if (IsHiddenField(featureType, iterator.name))
                {
                    continue;
                }

                EditorGUILayout.PropertyField(iterator, true);
                drewProperty = true;
            }

            if (!drewProperty)
            {
                EditorGUILayout.LabelField("No settings", EditorStyles.centeredGreyMiniLabel);
            }
        }

        private void RefreshValidation(Npc npc)
        {
            _issues.Clear();
            npc.CollectValidationIssues(_issues);
        }

        private void DrawValidation(Npc npc, bool forceOpen)
        {
            if (_issues.Count == 0 && !forceOpen)
            {
                return;
            }

            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _showValidation = EditorGUILayout.Foldout(
                        _showValidation,
                        _issues.Count == 0 ? "Setup Validation — Ready" : $"Setup Validation — {_issues.Count} issue(s)",
                        true,
                        _headerStyle);
                    GUILayout.FlexibleSpace();
                }

                if (!_showValidation)
                {
                    return;
                }

                if (_issues.Count == 0)
                {
                    EditorGUILayout.HelpBox("All feature dependencies and conflicts are valid.", MessageType.Info);
                    return;
                }

            for (int i = 0; i < _issues.Count; i++)
            {
                NpcValidationIssue issue = _issues[i];
                EditorGUILayout.HelpBox(issue.Message, ToMessageType(issue.Severity));

                if (CanAddSuggestedFeature(npc, issue.SuggestedFeatureType))
                {
                    string name = GetDisplayName(issue.SuggestedFeatureType);
                    if (GUILayout.Button($"Add {name}"))
                    {
                        AddFeature(npc, issue.SuggestedFeatureType);
                        GUIUtility.ExitGUI();
                    }
                }
                    else if (CanOfferCompatibleFeatures(issue.SuggestedFeatureType))
                    {
                        Type capabilityType = issue.SuggestedFeatureType;
                        if (GUILayout.Button($"Choose {ObjectNames.NicifyVariableName(capabilityType.Name)} Feature"))
                        {
                            ShowCompatibleFeatureMenu(npc, capabilityType);
                        }
                    }
                }
            }
        }

        private void ShowAddFeatureMenu(Npc npc)
        {
            CacheFeatureTypes();
            GenericMenu menu = new GenericMenu();
            bool addedAny = false;

            for (int i = 0; i < FeatureTypes.Count; i++)
            {
                Type featureType = FeatureTypes[i];
                NpcFeatureAttribute definition = GetDefinition(featureType);
                string category = definition != null ? definition.Category : "Custom";
                string displayName = definition != null
                    ? definition.DisplayName
                    : ObjectNames.NicifyVariableName(featureType.Name);
                string menuPath = $"{category}/{displayName}";
                bool canAdd = CanAddFeature(npc, featureType, definition);
                Type capturedType = featureType;

                if (canAdd)
                {
                    menu.AddItem(new GUIContent(menuPath), false, () => AddFeature(npc, capturedType));
                    addedAny = true;
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent(menuPath));
                }
            }

            if (!addedAny)
            {
                menu.AddDisabledItem(new GUIContent("No additional features available"));
            }

            menu.ShowAsContext();
        }

        private void ShowCompatibleFeatureMenu(Npc npc, Type capabilityType)
        {
            CacheFeatureTypes();
            GenericMenu menu = new GenericMenu();
            bool found = false;

            for (int i = 0; i < FeatureTypes.Count; i++)
            {
                Type featureType = FeatureTypes[i];
                NpcFeatureAttribute definition = GetDefinition(featureType);
                if (!capabilityType.IsAssignableFrom(featureType) ||
                    !CanAddFeature(npc, featureType, definition))
                {
                    continue;
                }

                Type capturedType = featureType;
                string displayName = GetDisplayName(featureType);
                menu.AddItem(new GUIContent(displayName), false, () => AddFeature(npc, capturedType));
                found = true;
            }

            if (!found)
            {
                menu.AddDisabledItem(new GUIContent("No compatible feature available"));
            }

            menu.ShowAsContext();
        }

        private void ShowFeatureActions(
            Npc npc,
            NpcFeature feature,
            int featureIndex,
            SerializedProperty featureProperty)
        {
            GenericMenu menu = new GenericMenu();
            Type featureType = feature.GetType();

            if (featureIndex > 0)
            {
                menu.AddItem(
                    new GUIContent("Move Up"),
                    false,
                    () => MoveFeature(npc, featureIndex, featureIndex - 1));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Move Up"));
            }

            if (featureIndex < _features.Count - 1)
            {
                menu.AddItem(
                    new GUIContent("Move Down"),
                    false,
                    () => MoveFeature(npc, featureIndex, featureIndex + 1));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Move Down"));
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(
                new GUIContent("Reset Settings"),
                false,
                () => ResetFeature(npc, featureIndex, featureType));

            NpcFeatureAttribute definition = GetDefinition(featureType);
            if (definition != null && definition.AllowMultiple)
            {
                menu.AddItem(
                    new GUIContent("Add Another"),
                    false,
                    () => AddFeature(npc, featureType));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Add Another"));
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(
                new GUIContent("Remove Feature"),
                false,
                () => RemoveFeature(npc, feature));
            menu.ShowAsContext();
        }

        private void MoveFeature(Npc npc, int sourceIndex, int destinationIndex)
        {
            serializedObject.Update();
            Undo.RecordObject(npc, "Reorder NPC Feature");
            _featureModulesProperty.MoveArrayElement(sourceIndex, destinationIndex);
            serializedObject.ApplyModifiedProperties();
            npc.RefreshFeatures();
            EditorUtility.SetDirty(npc);
        }

        private void ResetFeature(Npc npc, int featureIndex, Type featureType)
        {
            serializedObject.Update();
            if (featureIndex < 0 || featureIndex >= _featureModulesProperty.arraySize)
            {
                return;
            }

            Undo.RecordObject(npc, $"Reset {GetDisplayName(featureType)}");
            SerializedProperty element = _featureModulesProperty.GetArrayElementAtIndex(featureIndex);
            element.managedReferenceValue = Activator.CreateInstance(featureType);
            serializedObject.ApplyModifiedProperties();
            npc.RefreshFeatures();
            EditorUtility.SetDirty(npc);
        }

        private static void AddFeature(Npc npc, Type featureType)
        {
            if (npc == null || featureType == null)
            {
                return;
            }

            Undo.RecordObject(npc, $"Add {GetDisplayName(featureType)}");
            npc.AddFeature(featureType);
            EditorUtility.SetDirty(npc);
        }

        private void RemoveFeature(Npc npc, NpcFeature feature)
        {
            _expandedFeatures.Remove(feature);
            Undo.RecordObject(npc, $"Remove {GetDisplayName(feature.GetType())}");
            npc.RemoveFeature(feature);
            EditorUtility.SetDirty(npc);
        }

        private void RefreshFeatures(Npc npc)
        {
            _features.Clear();
            NpcFeatureCollection collection = npc.Features;
            for (int i = 0; i < collection.Count; i++)
            {
                NpcFeature feature = collection[i];
                _features.Add(feature);
                if (_knownFeatures.Add(feature))
                {
                    _expandedFeatures.Add(feature);
                }
            }
        }

        private void EnsureStyles()
        {
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(EditorStyles.boldLabel);
            }

            if (_statusStyle == null)
            {
                _statusStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal =
                    {
                        textColor = EditorGUIUtility.isProSkin
                            ? new Color(0.65f, 0.72f, 0.78f)
                            : new Color(0.25f, 0.32f, 0.38f)
                    }
                };
            }

            if (_categoryStyle == null)
            {
                _categoryStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    fontSize = 9,
                    normal =
                    {
                        textColor = EditorGUIUtility.isProSkin
                            ? new Color(0.55f, 0.62f, 0.68f)
                            : new Color(0.35f, 0.40f, 0.44f)
                    }
                };
            }

            if (_cardTitleStyle == null)
            {
                _cardTitleStyle = new GUIStyle(EditorStyles.foldoutHeader)
                {
                    fontStyle = FontStyle.Bold
                };
            }

            if (_badgeStyle == null)
            {
                _badgeStyle = new GUIStyle(EditorStyles.miniButton)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    fontSize = 9
                };
            }
        }

        private static void CacheFeatureTypes()
        {
            if (_featureTypesCached)
            {
                return;
            }

            FeatureTypes.Clear();
            TypeCache.TypeCollection discoveredTypes = TypeCache.GetTypesDerivedFrom<NpcFeature>();
            foreach (Type type in discoveredTypes)
            {
                if (!type.IsAbstract && !type.IsGenericTypeDefinition && type.IsSerializable)
                {
                    FeatureTypes.Add(type);
                }
            }

            FeatureTypes.Sort(CompareFeatureTypes);
            _featureTypesCached = true;
        }

        private static int CompareFeatureTypes(Type left, Type right)
        {
            NpcFeatureAttribute leftDefinition = GetDefinition(left);
            NpcFeatureAttribute rightDefinition = GetDefinition(right);
            string leftCategory = leftDefinition != null ? leftDefinition.Category : "Custom";
            string rightCategory = rightDefinition != null ? rightDefinition.Category : "Custom";
            int categoryComparison = string.Compare(leftCategory, rightCategory, StringComparison.OrdinalIgnoreCase);
            if (categoryComparison != 0)
            {
                return categoryComparison;
            }

            int orderComparison = (leftDefinition?.Order ?? 0).CompareTo(rightDefinition?.Order ?? 0);
            if (orderComparison != 0)
            {
                return orderComparison;
            }

            return string.Compare(GetDisplayName(left), GetDisplayName(right), StringComparison.OrdinalIgnoreCase);
        }

        private static bool CanAddFeature(Npc npc, Type featureType, NpcFeatureAttribute definition)
        {
            return definition != null && definition.AllowMultiple || !npc.HasExactFeature(featureType);
        }

        private static bool CanAddSuggestedFeature(Npc npc, Type featureType)
        {
            return featureType != null &&
                   !featureType.IsAbstract &&
                   featureType.IsSerializable &&
                   typeof(NpcFeature).IsAssignableFrom(featureType) &&
                   !npc.HasExactFeature(featureType);
        }

        private static bool CanOfferCompatibleFeatures(Type capabilityType)
        {
            if (capabilityType == null || typeof(NpcFeature).IsAssignableFrom(capabilityType))
            {
                return false;
            }

            CacheFeatureTypes();
            for (int i = 0; i < FeatureTypes.Count; i++)
            {
                if (capabilityType.IsAssignableFrom(FeatureTypes[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private void DrawIssueBadge()
        {
            if (_issues.Count == 0)
            {
                Color previous = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.55f, 0.85f, 0.62f);
                GUILayout.Label("READY", _badgeStyle, GUILayout.Width(52f));
                GUI.backgroundColor = previous;
                return;
            }

            bool hasError = false;
            for (int i = 0; i < _issues.Count; i++)
            {
                if (_issues[i].Severity == NpcValidationSeverity.Error)
                {
                    hasError = true;
                    break;
                }
            }

            Color oldBackground = GUI.backgroundColor;
            GUI.backgroundColor = hasError
                ? new Color(1f, 0.55f, 0.50f)
                : new Color(1f, 0.80f, 0.40f);
            GUILayout.Label($"{_issues.Count} ISSUE{(_issues.Count == 1 ? string.Empty : "S")}", _badgeStyle);
            GUI.backgroundColor = oldBackground;
        }

        private static bool IsHiddenField(Type featureType, string fieldName)
        {
            Type current = featureType;
            while (current != null && current != typeof(object))
            {
                FieldInfo field = current.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field.IsDefined(typeof(HideInInspector), true);
                }

                current = current.BaseType;
            }

            return false;
        }

        private static Texture GetCategoryIcon(string category)
        {
            string iconName;
            switch (category)
            {
                case "Movement":
                    iconName = "d_NavMeshAgent Icon";
                    break;
                case "Targeting":
                    iconName = "d_ViewToolOrbit";
                    break;
                case "Perception":
                    iconName = "d_SceneViewVisibility";
                    break;
                case "Presentation":
                    iconName = "d_SceneViewCamera";
                    break;
                default:
                    iconName = "d_cs Script Icon";
                    break;
            }

            return EditorGUIUtility.IconContent(iconName).image;
        }

        private static Color GetCategoryColor(string category)
        {
            switch (category)
            {
                case "Movement":
                    return new Color(0.25f, 0.62f, 0.95f, 0.9f);
                case "Targeting":
                    return new Color(0.95f, 0.48f, 0.35f, 0.9f);
                case "Perception":
                    return new Color(0.68f, 0.48f, 0.95f, 0.9f);
                case "Presentation":
                    return new Color(0.30f, 0.78f, 0.62f, 0.9f);
                default:
                    return new Color(0.60f, 0.65f, 0.70f, 0.9f);
            }
        }

        private static NpcFeatureAttribute GetDefinition(Type featureType)
        {
            return (NpcFeatureAttribute)Attribute.GetCustomAttribute(
                featureType,
                typeof(NpcFeatureAttribute),
                false);
        }

        private static string GetDisplayName(Type featureType)
        {
            NpcFeatureAttribute definition = GetDefinition(featureType);
            return definition != null
                ? definition.DisplayName
                : ObjectNames.NicifyVariableName(featureType.Name);
        }

        private static MessageType ToMessageType(NpcValidationSeverity severity)
        {
            switch (severity)
            {
                case NpcValidationSeverity.Error:
                    return MessageType.Error;
                case NpcValidationSeverity.Warning:
                    return MessageType.Warning;
                default:
                    return MessageType.Info;
            }
        }
    }
}
