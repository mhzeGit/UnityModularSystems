using System;
using System.Collections.Generic;
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

        private SerializedProperty _featureModulesProperty;
        private GUIStyle _headerStyle;
        private GUIStyle _statusStyle;

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
            DrawHeader(npc);
            DrawFeatures(npc);
            DrawValidation(npc);

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
                EditorGUILayout.LabelField(
                    $"{_features.Count} internal feature{(_features.Count == 1 ? string.Empty : "s")} · " +
                    (Application.isPlaying
                        ? (npc.IsOperational ? "Operational" : "Inactive")
                        : "Edit Mode"),
                    _statusStyle);

                GUILayout.Space(3f);
                if (GUILayout.Button("+  Add Feature", GUILayout.Height(25f)))
                {
                    ShowAddFeatureMenu(npc);
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
                DrawFeatureCard(npc, feature, featureProperty);
            }
        }

        private void DrawFeatureCard(
            Npc npc,
            NpcFeature feature,
            SerializedProperty featureProperty)
        {
            NpcFeatureAttribute definition = GetDefinition(feature.GetType());
            string title = definition != null
                ? definition.DisplayName
                : ObjectNames.NicifyVariableName(feature.GetType().Name);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool expanded = _expandedFeatures.Contains(feature);
                    bool newExpanded = EditorGUILayout.Foldout(expanded, title, true, _headerStyle);
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

                    bool enabled = GUILayout.Toggle(
                        feature.Enabled,
                        feature.Enabled ? "Enabled" : "Disabled",
                        EditorStyles.miniButton,
                        GUILayout.Width(65f));
                    if (enabled != feature.Enabled)
                    {
                        Undo.RecordObject(npc, enabled ? "Enable NPC Feature" : "Disable NPC Feature");
                        feature.Enabled = enabled;
                        EditorUtility.SetDirty(npc);
                    }

                    GUIContent removeContent = EditorGUIUtility.IconContent("Toolbar Minus");
                    removeContent.tooltip = $"Remove {title}";
                    if (GUILayout.Button(removeContent, EditorStyles.miniButton, GUILayout.Width(24f)))
                    {
                        RemoveFeature(npc, feature);
                        GUIUtility.ExitGUI();
                    }
                }

                if (!string.IsNullOrEmpty(definition?.Description))
                {
                    EditorGUILayout.LabelField(definition.Description, EditorStyles.wordWrappedMiniLabel);
                }

                if (Application.isPlaying)
                {
                    EditorGUILayout.LabelField(
                        feature.IsOperational ? "Operational" : "Inactive",
                        _statusStyle);
                }

                if (_expandedFeatures.Contains(feature) && featureProperty != null)
                {
                    EditorGUILayout.Space(2f);
                    DrawManagedReferenceChildren(featureProperty);
                }
            }
        }

        private static void DrawManagedReferenceChildren(SerializedProperty managedReference)
        {
            SerializedProperty iterator = managedReference.Copy();
            SerializedProperty end = iterator.GetEndProperty();
            bool enterChildren = true;
            bool drewProperty = false;

            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;
                if (iterator.depth != managedReference.depth + 1)
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

        private void DrawValidation(Npc npc)
        {
            _issues.Clear();
            npc.CollectValidationIssues(_issues);
            if (_issues.Count == 0)
            {
                return;
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Setup Validation", _headerStyle);
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
                _features.Add(collection[i]);
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
