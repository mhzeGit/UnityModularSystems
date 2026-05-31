using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;

using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace MHZE.EventSystem.Editor
{
    [CustomPropertyDrawer(typeof(EventBinding), true)]
    public class EventBindingDrawer : PropertyDrawer
    {
        private Type[] _eventArgTypes = Array.Empty<Type>();
        private static readonly string[] UnitySpecialMethods =
        {
            "Awake", "Start", "Update", "LateUpdate", "FixedUpdate",
            "OnEnable", "OnDisable", "OnDestroy", "OnValidate", "Reset",
            "OnAnimatorMove", "OnAnimatorIK",
            "OnApplicationFocus", "OnApplicationPause", "OnApplicationQuit",
            "OnBecameInvisible", "OnBecameVisible",
            "OnCollisionEnter", "OnCollisionStay", "OnCollisionExit",
            "OnCollisionEnter2D", "OnCollisionStay2D", "OnCollisionExit2D",
            "OnTriggerEnter", "OnTriggerStay", "OnTriggerExit",
            "OnTriggerEnter2D", "OnTriggerStay2D", "OnTriggerExit2D",
            "OnDrawGizmos", "OnDrawGizmosSelected",
            "OnGUI", "OnMouseDown", "OnMouseDrag", "OnMouseEnter",
            "OnMouseExit", "OnMouseOver", "OnMouseUp", "OnMouseUpAsButton",
            "OnParticleCollision", "OnParticleTrigger",
            "OnPostRender", "OnPreCull", "OnPreRender",
            "OnRenderImage", "OnRenderObject",
            "OnTransformChildrenChanged", "OnTransformParentChanged",
            "OnWillRenderObject",
            "OnBeforeTransformParentChange", "OnAfterTransformParentChange",
            "OnServerInitialized", "OnConnectedToServer",
            "OnDisconnectedFromServer", "OnFailedToConnect",
            "OnFailedToConnectToMasterServer", "OnMasterServerEvent",
            "OnNetworkInstantiate", "OnPlayerConnected", "OnPlayerDisconnected",
            "OnSerializeNetworkView",
            "Main", "AwakeFromLoad"
        };

        private static StyleSheet _cachedStyleSheet;

        private static StyleSheet GetStyleSheet()
        {
            if (_cachedStyleSheet == null)
            {
                var guids = AssetDatabase.FindAssets("EventBindingDrawer t:StyleSheet");
                for (int i = 0; i < guids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (path.Contains("com.mhze.event-system"))
                    {
                        _cachedStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                        break;
                    }
                }
            }
            return _cachedStyleSheet;
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            root.AddToClassList("event-binding-root");

            _eventArgTypes = Array.Empty<Type>();
            if (fieldInfo != null && fieldInfo.FieldType.IsGenericType)
            {
                var def = fieldInfo.FieldType.GetGenericTypeDefinition();
                if (def == typeof(EventBinding<>) || def == typeof(EventBinding<,>))
                {
                    _eventArgTypes = fieldInfo.FieldType.GetGenericArguments();
                }
            }

            var styleSheet = GetStyleSheet();
            if (styleSheet != null)
                root.styleSheets.Add(styleSheet);

            var listenersProp = property.FindPropertyRelative("_listeners");
            var so = property.serializedObject;

            var header = new VisualElement();
            header.AddToClassList("binding-header-row");

            var arrow = new Label("\u25B6");
            arrow.AddToClassList("foldout-arrow");
            header.Add(arrow);

            var title = new Label(property.displayName);
            title.AddToClassList("foldout-label");
            header.Add(title);

            header.Add(new VisualElement { style = { flexGrow = 1 } });

            root.Add(header);

            var content = new VisualElement();
            content.AddToClassList("bindings-content-area");
            root.Add(content);

            var footerBtn = new Button { text = "+  Add Listener" };
            footerBtn.AddToClassList("add-listener-footer");
            root.Add(footerBtn);

            bool expanded = false;

            // Drag state
            VisualElement dragDropIndicator = null;
            int dragFromIdx = -1;
            bool dragActive = false;

            void SetExpanded(bool val)
            {
                expanded = val;
                arrow.text = expanded ? "\u25BC" : "\u25B6";
                content.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
                footerBtn.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            }

            arrow.RegisterCallback<PointerDownEvent>(_ => SetExpanded(!expanded));
            title.RegisterCallback<PointerDownEvent>(_ => SetExpanded(!expanded));
            SetExpanded(false);

            void RebuildNow()
            {
                so.Update();
                content.Clear();

                var listeners = property.FindPropertyRelative("_listeners");
                int count = listeners.arraySize;
                title.text = count > 0 ? $"{property.displayName}  ({count})" : property.displayName;

                for (int i = 0; i < count; i++)
                {
                    int index = i;
                    var lp = listeners.GetArrayElementAtIndex(index);
                    var card = BuildListenerCard(lp, index, listeners, () => root.schedule.Execute((TimerState _) => RebuildNow()), _eventArgTypes);
                    content.Add(card);

                    var dragHandle = card.Q("drag-handle");
                    if (dragHandle != null)
                    {
                        int capturedIndex = index;
                        VisualElement capturedCard = card;
                        dragHandle.RegisterCallback<PointerDownEvent>(evt =>
                        {
                            if (evt.button != 0) return;
                            dragFromIdx = capturedIndex;
                            dragActive = true;
                            capturedCard.AddToClassList("dragging");
                        });
                    }
                }

                dragDropIndicator = new VisualElement();
                dragDropIndicator.AddToClassList("drop-indicator");
                dragDropIndicator.style.display = DisplayStyle.None;
                content.Add(dragDropIndicator);
            }

            void AddNewAndRebuild()
            {
                AddNewListener(listenersProp);
                if (!expanded) SetExpanded(true);
                RebuildNow();
            }

            footerBtn.clicked += AddNewAndRebuild;

            RebuildNow();

            // Drag-and-drop event handling

            root.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!dragActive) return;
                Vector2 localPos = content.WorldToLocal(evt.position);
                int targetIdx = ComputeDropIndex(localPos.y);
                if (targetIdx != dragFromIdx)
                    UpdateDropIndicator(targetIdx);
                else if (dragDropIndicator != null)
                    dragDropIndicator.style.display = DisplayStyle.None;
            }, TrickleDown.TrickleDown);

            root.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!dragActive) return;
                if (evt.button != 0) return;
                dragActive = false;
                if (dragFromIdx >= 0)
                {
                    Vector2 localPos = content.WorldToLocal(evt.position);
                    int targetIdx = ComputeDropIndex(localPos.y);
                    if (dragFromIdx != targetIdx)
                    {
                        listenersProp.MoveArrayElement(dragFromIdx, targetIdx);
                        listenersProp.serializedObject.ApplyModifiedProperties();
                    }
                }
                RebuildNow();
            }, TrickleDown.TrickleDown);

            root.RegisterCallback<PointerCancelEvent>(evt =>
            {
                if (!dragActive) return;
                dragActive = false;
                RebuildNow();
            }, TrickleDown.TrickleDown);

            int ComputeDropIndex(float localY)
            {
                int idx = 0;
                foreach (var child in content.Children())
                {
                    if (child == dragDropIndicator) continue;
                    float midY = child.layout.y + child.layout.height * 0.5f;
                    if (localY < midY)
                        return idx;
                    idx++;
                }
                return idx;
            }

            void UpdateDropIndicator(int targetIdx)
            {
                if (dragDropIndicator == null) return;
                int cardCount = content.childCount - 1;
                if (targetIdx < 0 || targetIdx == dragFromIdx || targetIdx > cardCount)
                {
                    dragDropIndicator.style.display = DisplayStyle.None;
                    return;
                }
                dragDropIndicator.style.display = DisplayStyle.Flex;
                if (targetIdx >= cardCount)
                    content.Add(dragDropIndicator);
                else
                    content.Insert(targetIdx, dragDropIndicator);
            }

            return root;
        }

        private VisualElement BuildListenerCard(SerializedProperty lp, int index,
            SerializedProperty listenersProp, Action rebuild, Type[] eventArgTypes = null)
        {
            var card = new VisualElement();
            card.AddToClassList("listener-card");

            var accent = new VisualElement();
            accent.AddToClassList("accent-strip");
            card.Add(accent);

            var enabledProp = lp.FindPropertyRelative("_enabled");
            var targetProp = lp.FindPropertyRelative("_target");
            var goProp = lp.FindPropertyRelative("_gameObject");
            var methodNameProp = lp.FindPropertyRelative("_methodName");
            var methodDisplayProp = lp.FindPropertyRelative("_methodDisplayName");
            var paramsProp = lp.FindPropertyRelative("_parameters");

            bool enabled = enabledProp.boolValue;
            SetCardAccent(card, accent, enabled);

            var hdr = new VisualElement();
            hdr.AddToClassList("card-header");

            var dragHandle = new Label("\u2261");
            dragHandle.name = "drag-handle";
            dragHandle.AddToClassList("drag-handle");
            hdr.Add(dragHandle);

            var toggle = new Toggle();
            toggle.value = enabled;
            toggle.RegisterValueChangedCallback(evt =>
            {
                enabledProp.boolValue = evt.newValue;
                enabledProp.serializedObject.ApplyModifiedProperties();
                SetCardAccent(card, accent, evt.newValue);
            });
            hdr.Add(toggle);

            var customLabelProp = lp.FindPropertyRelative("_customLabel");
            string displayName = !string.IsNullOrEmpty(customLabelProp.stringValue)
                ? customLabelProp.stringValue
                : GetListenerDisplayName(lp);
            var nameField = new TextField { value = displayName };
            nameField.AddToClassList("card-header-label");
            nameField.AddToClassList("card-header-name-field");
            if (!enabled) nameField.AddToClassList("disabled");
            Color darkBg = new Color(0.149f, 0.149f, 0.157f, 1f);
            var ni = nameField.Q(className: "unity-base-field__input");
            if (ni != null)
            {
                ni.style.backgroundColor = darkBg;
                ni.RegisterCallback<FocusEvent>(_ => ni.style.backgroundColor = darkBg);
                ni.RegisterCallback<BlurEvent>(_ => ni.style.backgroundColor = darkBg);
            }
            nameField.RegisterValueChangedCallback(evt =>
            {
                customLabelProp.stringValue = evt.newValue;
                customLabelProp.serializedObject.ApplyModifiedProperties();
            });
            hdr.Add(nameField);

            var rmBtn = new Button(() =>
            {
                RemoveListenerAt(listenersProp, index);
                rebuild();
            });
            rmBtn.text = "\u2715";
            rmBtn.AddToClassList("remove-button");
            hdr.Add(rmBtn);

            card.Add(hdr);

            card.Add(BuildGameObjectRow(goProp, targetProp, methodNameProp, methodDisplayProp, paramsProp, rebuild));
            card.Add(BuildComponentRow(targetProp, goProp, methodNameProp, methodDisplayProp, paramsProp, rebuild));
            card.Add(BuildMethodRow(targetProp, methodNameProp, methodDisplayProp, paramsProp, rebuild, eventArgTypes));

            if (paramsProp.arraySize > 0)
            {
                var paramSection = BuildParameterSection(lp, paramsProp, rebuild);
                card.Add(paramSection);
            }

            return card;
        }

        private static void SetCardAccent(VisualElement card, VisualElement accent, bool enabled)
        {
            if (enabled)
            {
                card.RemoveFromClassList("disabled");
                accent.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            }
            else
            {
                card.AddToClassList("disabled");
                accent.style.backgroundColor = new Color(0.388f, 0.388f, 0.400f);
            }
        }

        private static VisualElement BuildGameObjectRow(SerializedProperty goProp, SerializedProperty targetProp,
            SerializedProperty methodNameProp, SerializedProperty methodDisplayProp,
            SerializedProperty paramsProp, Action rebuild)
        {
            var row = new VisualElement();
            row.AddToClassList("field-row");
            row.Add(new Label("Object") { style = { width = 56, minWidth = 56 } });

            Component ct = targetProp.objectReferenceValue as Component;
            var go = ct != null ? ct.gameObject : goProp.objectReferenceValue as GameObject;

            var goField = new ObjectField { objectType = typeof(GameObject), value = go };
            goField.RegisterValueChangedCallback(evt =>
            {
                var newGO = evt.newValue as GameObject;
                goProp.objectReferenceValue = newGO;
                targetProp.objectReferenceValue = null;
                if (newGO != null && ct != null)
                {
                    var same = newGO.GetComponent(ct.GetType());
                    if (same != null)
                        targetProp.objectReferenceValue = same;
                }
                ClearMethod(methodNameProp, methodDisplayProp, paramsProp);
                targetProp.serializedObject.ApplyModifiedProperties();
                rebuild();
            });
            row.Add(goField);
            return row;
        }

        private static GameObject GetActiveGameObject(SerializedProperty targetProp, SerializedProperty goProp)
        {
            var ct = targetProp.objectReferenceValue as Component;
            return ct != null ? ct.gameObject : goProp.objectReferenceValue as GameObject;
        }

        private static VisualElement BuildFieldRow(string label, VisualElement field)
        {
            var row = new VisualElement();
            row.AddToClassList("field-row");
            row.Add(new Label(label) { style = { width = 56, minWidth = 56 } });
            row.Add(field);
            return row;
        }

        private static VisualElement BuildComponentRow(SerializedProperty targetProp, SerializedProperty goProp,
            SerializedProperty methodNameProp, SerializedProperty methodDisplayProp,
            SerializedProperty paramsProp, Action rebuild)
        {
            var go = GetActiveGameObject(targetProp, goProp);
            var currentTarget = targetProp.objectReferenceValue as Component;

            var comps = go != null
                ? go.GetComponents<Component>().Where(c => c != null).ToList()
                : new List<Component>();

            var choices = new List<string>();
            int idx = 0;

            if (go == null || comps.Count == 0)
            {
                choices.Add(go != null ? "Select Script..." : "No Script Selected");
            }
            else
            {
                for (int i = 0; i < comps.Count; i++)
                {
                    choices.Add(comps[i].GetType().Name);
                    if (comps[i] == currentTarget)
                        idx = i;
                }
            }

            var dropdown = new DropdownField();
            dropdown.choices = choices;
            dropdown.index = idx;
            dropdown.RegisterValueChangedCallback(evt =>
            {
                int i = dropdown.index;
                if (i >= 0 && i < comps.Count && comps[i] != null)
                {
                    targetProp.objectReferenceValue = comps[i];
                    goProp.objectReferenceValue = null;
                    ClearMethod(methodNameProp, methodDisplayProp, paramsProp);
                    targetProp.serializedObject.ApplyModifiedProperties();
                    rebuild();
                }
            });

            return BuildFieldRow("Script", dropdown);
        }

        private static VisualElement BuildMethodRow(SerializedProperty targetProp,
            SerializedProperty methodNameProp, SerializedProperty methodDisplayProp,
            SerializedProperty paramsProp, Action rebuild, Type[] eventArgTypes = null)
        {
            var target = targetProp.objectReferenceValue as Component;
            var methods = target != null ? GetBindableMethods(target.GetType()) : new List<MethodInfo>();

            var choices = new List<string>();
            var methodItems = new List<MethodInfo>();
            int idx = 0;
            string currentName = methodNameProp.stringValue;
            bool hasNoSelection = string.IsNullOrEmpty(currentName);

            if (hasNoSelection)
            {
                choices.Add(target != null ? "No Method Selected" : "No Script Selected");
                methodItems.Add(null);
            }

            foreach (var m in methods)
            {
                choices.Add(FormatMethodSignature(m));
                methodItems.Add(m);
                if (!hasNoSelection && m.Name == currentName)
                    idx = choices.Count - 1;
            }

            if (choices.Count == 0)
            {
                choices.Add(target != null ? "No Method Selected" : "No Script Selected");
                methodItems.Add(null);
            }

            var dropdown = new DropdownField();
            dropdown.choices = choices;
            dropdown.index = idx;
            dropdown.RegisterValueChangedCallback(evt =>
            {
                int i = dropdown.index;
                if (i >= 0 && i < methodItems.Count && methodItems[i] != null)
                {
                    SelectMethod(methodItems[i], methodNameProp, methodDisplayProp, paramsProp, eventArgTypes);
                    targetProp.serializedObject.ApplyModifiedProperties();
                    rebuild();
                }
            });

            return BuildFieldRow("Method", dropdown);
        }

        private VisualElement BuildParameterSection(SerializedProperty listenerProp,
            SerializedProperty paramsProp, Action rebuild)
        {
            var section = new VisualElement();
            section.AddToClassList("param-section");

            var key = $"{listenerProp.propertyPath}_params";

            var foldout = new Foldout
            {
                text = $"  Parameters  ({paramsProp.arraySize})",
                value = _paramFoldouts.TryGetValue(key, out var pe) && pe
            };
            foldout.AddToClassList("param-section-foldout");
            foldout.RegisterValueChangedCallback(evt => { _paramFoldouts[key] = evt.newValue; });

            for (int p = 0; p < paramsProp.arraySize; p++)
            {
                var pp = paramsProp.GetArrayElementAtIndex(p);
                var typeNameProp = pp.FindPropertyRelative("_parameterTypeName");
                var paramType = ResolveType(typeNameProp.stringValue);
                foldout.Add(BuildParameterEntry(pp, paramType, rebuild));
            }

            section.Add(foldout);
            return section;
        }

        private static readonly Dictionary<string, bool> _paramFoldouts = new Dictionary<string, bool>();

        private VisualElement BuildParameterEntry(SerializedProperty pp, Type paramType, Action rebuild)
        {
            var entry = new VisualElement();
            entry.AddToClassList("param-entry");

            var nameProp = pp.FindPropertyRelative("_parameterName");
            var sourceProp = pp.FindPropertyRelative("_source");
            var sourceComponentProp = pp.FindPropertyRelative("_sourceComponent");
            var sourceMemberProp = pp.FindPropertyRelative("_sourceMemberName");

            var accent = new VisualElement();
            accent.AddToClassList("param-accent-strip");
            entry.Add(accent);

            var headerRow = new VisualElement();
            headerRow.AddToClassList("param-header-row");

            var paramNameLabel = new Label(nameProp.stringValue);
            paramNameLabel.AddToClassList("param-header-name");
            headerRow.Add(paramNameLabel);

            string typeDisplay = paramType != null ? GetTypeDisplayName(paramType) : "unknown";
            var paramTypeLabel = new Label($"({typeDisplay})");
            paramTypeLabel.AddToClassList("param-header-type");
            headerRow.Add(paramTypeLabel);

            entry.Add(headerRow);

            var sourceRow = new VisualElement();
            sourceRow.AddToClassList("source-row");

            sourceRow.Add(new Label("Source") { style = { width = 44, minWidth = 44 } });

            var sourceField = new EnumField((ArgumentSource)sourceProp.enumValueIndex);
            sourceField.style.width = 72;
            sourceField.RegisterValueChangedCallback(evt =>
            {
                sourceProp.enumValueIndex = (int)(ArgumentSource)evt.newValue;
                sourceProp.serializedObject.ApplyModifiedProperties();
                RebuildParamValue(sourceRow, pp, sourceProp, paramType, sourceComponentProp, sourceMemberProp, rebuild);
                SetParamAccent(accent, (ArgumentSource)evt.newValue);
            });
            sourceRow.Add(sourceField);

            var valueContainer = new VisualElement { name = "param-value" };
            valueContainer.AddToClassList("value-container");
            sourceRow.Add(valueContainer);

            entry.Add(sourceRow);
            RebuildParamValue(sourceRow, pp, sourceProp, paramType, sourceComponentProp, sourceMemberProp, rebuild);
            SetParamAccent(accent, (ArgumentSource)sourceProp.enumValueIndex);

            return entry;
        }

        private void RebuildParamValue(VisualElement sourceRow, SerializedProperty pp, SerializedProperty sourceProp,
            Type paramType, SerializedProperty sourceComponentProp, SerializedProperty sourceMemberProp, Action rebuild)
        {
            var container = sourceRow.Q<VisualElement>("param-value");
            if (container == null) return;
            container.Clear();

            var source = (ArgumentSource)sourceProp.enumValueIndex;

            if (source == ArgumentSource.Constant)
            {
                container.Add(BuildConstantValueField(pp, paramType));
            }
            else if (source == ArgumentSource.Event)
            {
                container.Add(BuildEventSourceField(pp, paramType));
            }
            else
            {
                container.Add(BuildScriptSourceField(sourceComponentProp, sourceMemberProp, paramType, rebuild));
            }
        }

        private static void SetParamAccent(VisualElement accent, ArgumentSource source)
        {
            accent.style.backgroundColor = source == ArgumentSource.Constant
                ? new Color(0.039f, 0.518f, 1.000f, 0.6f)
                : source == ArgumentSource.Event
                    ? new Color(0.039f, 1.000f, 0.624f, 0.6f)
                    : new Color(1.000f, 0.624f, 0.039f, 0.6f);
        }

        private static VisualElement BuildConstantValueField(SerializedProperty pp, Type paramType)
        {
            if (paramType == null)
                return new Label("Unresolved type") { style = { color = new Color(0.6f, 0.1f, 0.1f) } };

            if (paramType == typeof(int))
            {
                var f = new IntegerField { value = pp.FindPropertyRelative("_intValue").intValue };
                f.RegisterValueChangedCallback(evt =>
                {
                    pp.FindPropertyRelative("_intValue").intValue = evt.newValue;
                    pp.serializedObject.ApplyModifiedProperties();
                });
                return f;
            }
            if (paramType == typeof(float))
            {
                var f = new FloatField { value = pp.FindPropertyRelative("_floatValue").floatValue };
                f.RegisterValueChangedCallback(evt =>
                {
                    pp.FindPropertyRelative("_floatValue").floatValue = evt.newValue;
                    pp.serializedObject.ApplyModifiedProperties();
                });
                return f;
            }
            if (paramType == typeof(double))
            {
                var f = new DoubleField { value = pp.FindPropertyRelative("_doubleValue").doubleValue };
                f.RegisterValueChangedCallback(evt =>
                {
                    pp.FindPropertyRelative("_doubleValue").doubleValue = evt.newValue;
                    pp.serializedObject.ApplyModifiedProperties();
                });
                return f;
            }
            if (paramType == typeof(long))
            {
                var f = new LongField { value = pp.FindPropertyRelative("_longValue").longValue };
                f.RegisterValueChangedCallback(evt =>
                {
                    pp.FindPropertyRelative("_longValue").longValue = evt.newValue;
                    pp.serializedObject.ApplyModifiedProperties();
                });
                return f;
            }
            if (paramType == typeof(bool))
            {
                var f = new Toggle { value = pp.FindPropertyRelative("_boolValue").boolValue };
                f.RegisterValueChangedCallback(evt =>
                {
                    pp.FindPropertyRelative("_boolValue").boolValue = evt.newValue;
                    pp.serializedObject.ApplyModifiedProperties();
                });
                return f;
            }
            if (paramType == typeof(string))
            {
                var f = new TextField { value = pp.FindPropertyRelative("_stringValue").stringValue ?? "" };
                f.RegisterValueChangedCallback(evt =>
                {
                    pp.FindPropertyRelative("_stringValue").stringValue = evt.newValue;
                    pp.serializedObject.ApplyModifiedProperties();
                });
                return f;
            }
            if (paramType == typeof(char))
            {
                var f = new TextField { value = pp.FindPropertyRelative("_stringValue").stringValue ?? "" };
                f.RegisterValueChangedCallback(evt =>
                {
                    string v = evt.newValue;
                    pp.FindPropertyRelative("_stringValue").stringValue = v.Length > 0 ? v[0].ToString() : "";
                    pp.serializedObject.ApplyModifiedProperties();
                });
                return f;
            }
            if (paramType == typeof(Vector2))
            {
                var f = new Vector2Field { value = pp.FindPropertyRelative("_vector2Value").vector2Value };
                f.RegisterValueChangedCallback(evt =>
                {
                    pp.FindPropertyRelative("_vector2Value").vector2Value = evt.newValue;
                    pp.serializedObject.ApplyModifiedProperties();
                });
                return f;
            }
            if (paramType == typeof(Vector3))
            {
                var f = new Vector3Field { value = pp.FindPropertyRelative("_vector3Value").vector3Value };
                f.RegisterValueChangedCallback(evt =>
                {
                    pp.FindPropertyRelative("_vector3Value").vector3Value = evt.newValue;
                    pp.serializedObject.ApplyModifiedProperties();
                });
                return f;
            }
            if (paramType == typeof(Vector4))
            {
                var f = new Vector4Field { value = pp.FindPropertyRelative("_vector4Value").vector4Value };
                f.RegisterValueChangedCallback(evt =>
                {
                    pp.FindPropertyRelative("_vector4Value").vector4Value = evt.newValue;
                    pp.serializedObject.ApplyModifiedProperties();
                });
                return f;
            }
            if (paramType == typeof(Quaternion))
            {
                var f = new Vector3Field { value = pp.FindPropertyRelative("_vector3Value").vector3Value };
                f.label = "Euler";
                f.RegisterValueChangedCallback(evt =>
                {
                    pp.FindPropertyRelative("_vector3Value").vector3Value = evt.newValue;
                    pp.serializedObject.ApplyModifiedProperties();
                });
                return f;
            }
            if (paramType == typeof(Color))
            {
                var f = new ColorField { value = pp.FindPropertyRelative("_colorValue").colorValue };
                f.RegisterValueChangedCallback(evt =>
                {
                    pp.FindPropertyRelative("_colorValue").colorValue = evt.newValue;
                    pp.serializedObject.ApplyModifiedProperties();
                });
                return f;
            }
            if (paramType == typeof(Rect))
            {
                var f = new RectField { value = pp.FindPropertyRelative("_rectValue").rectValue };
                f.RegisterValueChangedCallback(evt =>
                {
                    pp.FindPropertyRelative("_rectValue").rectValue = evt.newValue;
                    pp.serializedObject.ApplyModifiedProperties();
                });
                return f;
            }
            if (paramType == typeof(Bounds))
            {
                var f = new BoundsField { value = pp.FindPropertyRelative("_boundsValue").boundsValue };
                f.RegisterValueChangedCallback(evt =>
                {
                    pp.FindPropertyRelative("_boundsValue").boundsValue = evt.newValue;
                    pp.serializedObject.ApplyModifiedProperties();
                });
                return f;
            }
            if (paramType == typeof(AnimationCurve))
            {
                var f = new CurveField { value = pp.FindPropertyRelative("_curveValue").animationCurveValue };
                f.RegisterValueChangedCallback(evt =>
                {
                    pp.FindPropertyRelative("_curveValue").animationCurveValue = evt.newValue;
                    pp.serializedObject.ApplyModifiedProperties();
                });
                return f;
            }
            if (paramType == typeof(Gradient))
            {
                var f = new GradientField { value = pp.FindPropertyRelative("_gradientValue").gradientValue };
                f.RegisterValueChangedCallback(evt =>
                {
                    pp.FindPropertyRelative("_gradientValue").gradientValue = evt.newValue;
                    pp.serializedObject.ApplyModifiedProperties();
                });
                return f;
            }
            if (paramType == typeof(LayerMask))
            {
                var f = new LayerMaskField { value = (LayerMask)pp.FindPropertyRelative("_layerMaskValue").intValue };
                f.RegisterValueChangedCallback(evt =>
                {
                    pp.FindPropertyRelative("_layerMaskValue").intValue = evt.newValue;
                    pp.serializedObject.ApplyModifiedProperties();
                });
                return f;
            }
            if (paramType.IsEnum)
            {
                var intProp = pp.FindPropertyRelative("_intValue");
                var names = Enum.GetNames(paramType);
                var vals = Enum.GetValues(paramType);
                int idx = -1;
                for (int i = 0; i < vals.Length; i++)
                    if (Equals(vals.GetValue(i), Enum.ToObject(paramType, intProp.intValue))) { idx = i; break; }
                var f = new DropdownField();
                f.choices = new List<string>(names);
                f.index = idx >= 0 ? idx : 0;
                f.RegisterValueChangedCallback(evt =>
                {
                    int newIdx = f.index;
                    if (newIdx >= 0 && newIdx < vals.Length)
                        intProp.intValue = (int)vals.GetValue(newIdx);
                    intProp.serializedObject.ApplyModifiedProperties();
                });
                return f;
            }
            if (paramType == typeof(GameObject))
            {
                var prop = pp.FindPropertyRelative("_objectValue");
                var f = new ObjectField { objectType = typeof(GameObject), value = prop.objectReferenceValue as GameObject };
                f.RegisterValueChangedCallback(evt =>
                {
                    prop.objectReferenceValue = evt.newValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });
                return f;
            }
            if (typeof(Component).IsAssignableFrom(paramType))
            {
                var prop = pp.FindPropertyRelative("_objectValue");
                var f = new ObjectField { objectType = paramType, value = prop.objectReferenceValue as Component };
                f.RegisterValueChangedCallback(evt =>
                {
                    prop.objectReferenceValue = evt.newValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });
                return f;
            }
            if (typeof(Object).IsAssignableFrom(paramType))
            {
                var prop = pp.FindPropertyRelative("_objectValue");
                var f = new ObjectField { objectType = paramType, value = prop.objectReferenceValue };
                f.RegisterValueChangedCallback(evt =>
                {
                    prop.objectReferenceValue = evt.newValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });
                return f;
            }
            if (paramType == typeof(short))
            {
                var fp = pp.FindPropertyRelative("_intValue");
                var f = new IntegerField { value = fp.intValue };
                f.RegisterValueChangedCallback(evt =>
                {
                    fp.intValue = Mathf.Clamp(evt.newValue, short.MinValue, short.MaxValue);
                    fp.serializedObject.ApplyModifiedProperties();
                });
                return f;
            }
            if (paramType == typeof(byte))
            {
                var fp = pp.FindPropertyRelative("_intValue");
                var f = new IntegerField { value = fp.intValue };
                f.RegisterValueChangedCallback(evt =>
                {
                    fp.intValue = Mathf.Clamp(evt.newValue, byte.MinValue, byte.MaxValue);
                    fp.serializedObject.ApplyModifiedProperties();
                });
                return f;
            }

            var fp2 = pp.FindPropertyRelative("_stringValue");
            var ff = new TextField { value = fp2.stringValue, label = paramType.Name };
            ff.RegisterValueChangedCallback(evt =>
            {
                fp2.stringValue = evt.newValue;
                fp2.serializedObject.ApplyModifiedProperties();
            });
            return ff;
        }

        private static VisualElement BuildScriptSourceField(SerializedProperty sourceComponentProp,
            SerializedProperty sourceMemberProp, Type paramType, Action rebuild)
        {
            var container = new VisualElement();
            container.AddToClassList("script-source-container");

            var innerRow = new VisualElement();
            innerRow.style.flexDirection = FlexDirection.Row;

            var compField = new ObjectField
            {
                objectType = typeof(Component),
                value = sourceComponentProp.objectReferenceValue as Component
            };
            compField.style.flexGrow = 1;
            compField.RegisterValueChangedCallback(evt =>
            {
                sourceComponentProp.objectReferenceValue = evt.newValue;
                sourceMemberProp.stringValue = "";
                sourceComponentProp.serializedObject.ApplyModifiedProperties();
                rebuild();
            });
            innerRow.Add(compField);

            var comp = sourceComponentProp.objectReferenceValue as Component;
            var members = new List<string> { "Select..." };
            var memberNames = new List<string> { null };
            int memberIdx = 0;

            if (comp != null)
            {
                members.Clear();
                memberNames.Clear();
                var type = comp.GetType();
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                    .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                    .Where(p => p.Name != "enabled" && p.Name != "name" && p.Name != "tag"
                        && p.Name != "gameObject" && p.Name != "transform" && p.Name != "hideFlags")
                    .ToList();

                foreach (var f in fields)
                {
                    members.Add($"{f.Name} : {GetTypeDisplayName(f.FieldType)}");
                    memberNames.Add(f.Name);
                }
                foreach (var p in props)
                {
                    members.Add($"{p.Name} : {GetTypeDisplayName(p.PropertyType)}");
                    memberNames.Add(p.Name);
                }

                string currentMember = sourceMemberProp.stringValue;
                for (int i = 0; i < memberNames.Count; i++)
                {
                    if (memberNames[i] == currentMember)
                    {
                        memberIdx = i;
                        break;
                    }
                }
            }

            var memberDropdown = new DropdownField();
            memberDropdown.choices = members;
            memberDropdown.index = memberIdx;
            memberDropdown.style.width = 100;
            memberDropdown.RegisterValueChangedCallback(evt =>
            {
                int i = memberDropdown.index;
                if (i >= 0 && i < memberNames.Count && memberNames[i] != null)
                {
                    sourceMemberProp.stringValue = memberNames[i];
                    sourceMemberProp.serializedObject.ApplyModifiedProperties();
                    rebuild();
                }
            });

            innerRow.Add(memberDropdown);
            container.Add(innerRow);

            if (sourceComponentProp.objectReferenceValue != null && !string.IsNullOrEmpty(sourceMemberProp.stringValue))
            {
                var info = new Label($"  \u2192  {sourceComponentProp.objectReferenceValue.name}.{sourceMemberProp.stringValue}");
                info.AddToClassList("script-info");
                container.Add(info);
            }

            return container;
        }

        private VisualElement BuildEventSourceField(SerializedProperty pp, Type paramType)
        {
            var eventVarProp = pp.FindPropertyRelative("_eventVariableName");
            var eventArgIdxProp = pp.FindPropertyRelative("_eventArgIndex");

            if (_eventArgTypes.Length == 0)
            {
                var label = new Label("Event argument type unavailable");
                label.style.unityFontStyleAndWeight = FontStyle.Italic;
                label.style.color = new Color(0.6f, 0.1f, 0.1f);
                return label;
            }

            if (paramType == null)
            {
                var label = new Label("Unresolved parameter type");
                label.style.color = new Color(0.6f, 0.1f, 0.1f);
                return label;
            }

            var choices = new List<string>();
            var choiceIndices = new List<int>();
            var choiceNames = new List<string>();

            for (int ai = 0; ai < _eventArgTypes.Length; ai++)
            {
                var argType = _eventArgTypes[ai];
                if (argType == null) continue;

                bool addedHeader = false;

                if (paramType.IsAssignableFrom(argType) || paramType == typeof(object))
                {
                    choices.Add($"Arg {ai} : {GetTypeDisplayName(argType)}");
                    choiceIndices.Add(ai);
                    choiceNames.Add("");
                    addedHeader = true;
                }

                foreach (var field in argType.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (paramType.IsAssignableFrom(field.FieldType))
                    {
                        choices.Add($"  {field.Name} : {GetTypeDisplayName(field.FieldType)}");
                        choiceIndices.Add(ai);
                        choiceNames.Add(field.Name);
                    }
                }

                foreach (var prop in argType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                             .Where(p => p.CanRead && p.GetIndexParameters().Length == 0))
                {
                    if (paramType.IsAssignableFrom(prop.PropertyType))
                    {
                        choices.Add($"  {prop.Name} : {GetTypeDisplayName(prop.PropertyType)}");
                        choiceIndices.Add(ai);
                        choiceNames.Add(prop.Name);
                    }
                }
            }

            var dropdown = new DropdownField();
            dropdown.style.flexGrow = 1;

            if (choices.Count > 0)
            {
                int currentIdx = eventArgIdxProp.intValue;
                string currentVar = eventVarProp.stringValue;

                int idx = 0;
                for (int i = 0; i < choiceIndices.Count; i++)
                {
                    if (choiceIndices[i] == currentIdx && choiceNames[i] == (currentVar ?? ""))
                    {
                        idx = i;
                        break;
                    }
                }

                dropdown.choices = choices;
                dropdown.index = idx;
                dropdown.RegisterValueChangedCallback(evt =>
                {
                    int i = dropdown.index;
                    if (i >= 0 && i < choiceIndices.Count)
                    {
                        eventArgIdxProp.intValue = choiceIndices[i];
                        eventVarProp.stringValue = choiceNames[i];
                        eventArgIdxProp.serializedObject.ApplyModifiedProperties();
                    }
                });
            }
            else
            {
                dropdown.choices = new List<string> { "No matching variables" };
                dropdown.SetEnabled(false);
            }

            return dropdown;
        }

        private static string GetListenerDisplayName(SerializedProperty lp)
        {
            var targetProp = lp.FindPropertyRelative("_target");
            var goProp = lp.FindPropertyRelative("_gameObject");
            var displayProp = lp.FindPropertyRelative("_methodDisplayName");

            var target = targetProp.objectReferenceValue as Component;
            var go = goProp.objectReferenceValue as GameObject;

            if (target != null)
                return $"{target.gameObject.name} ({target.GetType().Name})";
            if (go != null)
                return $"{go.name} (No Script)";
            return "No Target Selected";
        }

        private static void AddNewListener(SerializedProperty listenersProp)
        {
            int idx = listenersProp.arraySize;
            listenersProp.arraySize++;
            var lp = listenersProp.GetArrayElementAtIndex(idx);
            lp.FindPropertyRelative("_enabled").boolValue = true;
            lp.FindPropertyRelative("_gameObject").objectReferenceValue = null;
            lp.FindPropertyRelative("_methodName").stringValue = "";
            lp.FindPropertyRelative("_methodDisplayName").stringValue = "";
            lp.FindPropertyRelative("_customLabel").stringValue = "";
            lp.FindPropertyRelative("_target").objectReferenceValue = null;
            var pp = lp.FindPropertyRelative("_parameters");
            pp.ClearArray();
            pp.arraySize = 0;
            listenersProp.serializedObject.ApplyModifiedProperties();
        }

        private static void RemoveListenerAt(SerializedProperty listenersProp, int index)
        {
            if (index < 0 || index >= listenersProp.arraySize) return;
            listenersProp.serializedObject.Update();
            int before = listenersProp.arraySize;
            listenersProp.DeleteArrayElementAtIndex(index);
            if (listenersProp.arraySize == before)
                listenersProp.DeleteArrayElementAtIndex(index);
            listenersProp.serializedObject.ApplyModifiedProperties();
        }

        private static void ClearMethod(SerializedProperty methodNameProp, SerializedProperty methodDisplayProp,
            SerializedProperty paramsProp)
        {
            methodNameProp.stringValue = "";
            methodDisplayProp.stringValue = "";
            paramsProp.ClearArray();
            paramsProp.arraySize = 0;
        }

        private static List<MethodInfo> GetBindableMethods(Type type)
        {
            return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                .Where(m =>
                {
                    if (m.IsSpecialName) return false;
                    if (m.IsGenericMethod || m.ContainsGenericParameters) return false;
                    if (UnitySpecialMethods.Contains(m.Name)) return false;
                    if (m.GetParameters().Any(p => p.IsOut || p.ParameterType.IsByRef)) return false;
                    if (m.IsDefined(typeof(ObsoleteAttribute), true)) return false;
                    Type dt = m.DeclaringType;
                    if (dt == typeof(object) || dt == typeof(Object) ||
                        dt == typeof(Component) || dt == typeof(Behaviour) ||
                        dt == typeof(MonoBehaviour))
                        return false;
                    return true;
                })
                .OrderBy(m => m.Name)
                .ToList();
        }

        private static string FormatMethodSignature(MethodInfo method)
        {
            var pars = method.GetParameters();
            string ret = method.ReturnType.Name;
            string args = string.Join(", ", pars.Select(p => $"{GetTypeDisplayName(p.ParameterType)} {p.Name}"));
            return string.IsNullOrEmpty(args) ? $"{method.Name}() : {ret}" : $"{method.Name}({args}) : {ret}";
        }

        internal static string GetTypeDisplayName(Type type)
        {
            if (type == typeof(int)) return "int";
            if (type == typeof(float)) return "float";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(string)) return "string";
            if (type == typeof(double)) return "double";
            if (type == typeof(long)) return "long";
            if (type == typeof(short)) return "short";
            if (type == typeof(byte)) return "byte";
            if (type == typeof(uint)) return "uint";
            if (type == typeof(ulong)) return "ulong";
            if (type == typeof(ushort)) return "ushort";
            if (type == typeof(sbyte)) return "sbyte";
            if (type == typeof(void)) return "void";
            if (type == typeof(Vector2)) return "Vector2";
            if (type == typeof(Vector3)) return "Vector3";
            if (type == typeof(Vector4)) return "Vector4";
            if (type == typeof(Color)) return "Color";
            if (type == typeof(Rect)) return "Rect";
            if (type == typeof(Bounds)) return "Bounds";
            if (type == typeof(AnimationCurve)) return "AnimationCurve";
            if (type == typeof(Gradient)) return "Gradient";
            if (type == typeof(LayerMask)) return "LayerMask";
            if (type == typeof(Quaternion)) return "Quaternion";
            if (type == typeof(GameObject)) return "GameObject";
            if (type == typeof(Transform)) return "Transform";
            return type.Name;
        }

        private static void SelectMethod(MethodInfo method, SerializedProperty methodNameProp,
            SerializedProperty methodDisplayProp, SerializedProperty paramsProp, Type[] eventArgTypes = null)
        {
            methodNameProp.stringValue = method.Name;
            methodDisplayProp.stringValue = FormatMethodSignature(method);
            var pars = method.GetParameters();
            paramsProp.ClearArray();
            paramsProp.arraySize = pars.Length;
            for (int i = 0; i < pars.Length; i++)
            {
                var pp = paramsProp.GetArrayElementAtIndex(i);
                pp.FindPropertyRelative("_parameterName").stringValue = pars[i].Name ?? $"param{i}";
                pp.FindPropertyRelative("_parameterTypeName").stringValue = pars[i].ParameterType.AssemblyQualifiedName;

                bool matched = TryMatchEventVariable(pars[i].ParameterType, eventArgTypes,
                    out int matchedIdx, out string matchedVar);

                pp.FindPropertyRelative("_source").enumValueIndex = matched
                    ? (int)ArgumentSource.Event
                    : (int)ArgumentSource.Constant;
                pp.FindPropertyRelative("_eventArgIndex").intValue = matched ? matchedIdx : 0;
                pp.FindPropertyRelative("_eventVariableName").stringValue = matchedVar ?? "";
                ResetParamValues(pp);
            }
        }

        private static bool TryMatchEventVariable(Type paramType, Type[] eventArgTypes,
            out int argIndex, out string varName)
        {
            argIndex = 0;
            varName = null;

            if (eventArgTypes == null || eventArgTypes.Length == 0 || paramType == null)
                return false;

            for (int ai = 0; ai < eventArgTypes.Length; ai++)
            {
                var argType = eventArgTypes[ai];
                if (argType == null) continue;

                if (paramType.IsAssignableFrom(argType) || paramType == typeof(object))
                {
                    argIndex = ai;
                    varName = "";
                    return true;
                }

                foreach (var field in argType.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (paramType.IsAssignableFrom(field.FieldType))
                    {
                        argIndex = ai;
                        varName = field.Name;
                        return true;
                    }
                }

                foreach (var prop in argType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                             .Where(p => p.CanRead && p.GetIndexParameters().Length == 0))
                {
                    if (paramType.IsAssignableFrom(prop.PropertyType))
                    {
                        argIndex = ai;
                        varName = prop.Name;
                        return true;
                    }
                }
            }

            return false;
        }

        private static void ResetParamValues(SerializedProperty pp)
        {
            pp.FindPropertyRelative("_sourceComponent").objectReferenceValue = null;
            pp.FindPropertyRelative("_sourceMemberName").stringValue = "";
            pp.FindPropertyRelative("_eventVariableName").stringValue = "";
            pp.FindPropertyRelative("_eventArgIndex").intValue = 0;
            pp.FindPropertyRelative("_intValue").intValue = 0;
            pp.FindPropertyRelative("_floatValue").floatValue = 0f;
            pp.FindPropertyRelative("_doubleValue").doubleValue = 0.0;
            pp.FindPropertyRelative("_longValue").longValue = 0L;
            pp.FindPropertyRelative("_boolValue").boolValue = false;
            pp.FindPropertyRelative("_stringValue").stringValue = "";
            pp.FindPropertyRelative("_vector2Value").vector2Value = Vector2.zero;
            pp.FindPropertyRelative("_vector3Value").vector3Value = Vector3.zero;
            pp.FindPropertyRelative("_vector4Value").vector4Value = Vector4.zero;
            pp.FindPropertyRelative("_colorValue").colorValue = Color.white;
            pp.FindPropertyRelative("_rectValue").rectValue = new Rect(0, 0, 1, 1);
            pp.FindPropertyRelative("_boundsValue").boundsValue = new Bounds(Vector3.zero, Vector3.one);
            pp.FindPropertyRelative("_layerMaskValue").intValue = 0;
            pp.FindPropertyRelative("_objectValue").objectReferenceValue = null;
        }

        private static Type ResolveType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;
            var t = Type.GetType(typeName);
            if (t != null) return t;
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var found = asm.GetType(typeName);
                    if (found != null) return found;
                    int dot = typeName.LastIndexOf('.');
                    if (dot > 0)
                    {
                        string simple = typeName.Substring(dot + 1);
                        found = asm.GetTypes().FirstOrDefault(x => x.Name == simple && x.FullName == typeName);
                        if (found != null) return found;
                    }
                }
            }
            catch { }
            return null;
        }

    }
}
