using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MHZE.EventSystem.Editor
{
    [CustomPropertyDrawer(typeof(EventBinding))]
    public class EventBindingDrawer : PropertyDrawer
    {
        private const float BindingHeaderH = 24f;
        private const float CardHPad = 8f;
        private const float CardVPad = 6f;
        private const float HeaderH = 24f;
        private const float RowH = 18f;
        private const float RowCompact = 16f;
        private const float FieldGap = 4f;
        private const float CardGap = 6f;

        private static readonly Dictionary<string, bool> _bindingFoldouts = new Dictionary<string, bool>();
        private static readonly Dictionary<string, bool> _paramFoldouts = new Dictionary<string, bool>();

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

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            string pathKey = property.propertyPath;
            if (!_bindingFoldouts.TryGetValue(pathKey, out var expanded) || !expanded)
                return EditorGUIUtility.singleLineHeight + 2f;

            float h = BindingHeaderH + 4f;

            var listenersProp = property.FindPropertyRelative("_listeners");
            for (int i = 0; i < listenersProp.arraySize; i++)
            {
                h += GetListenerHeight(listenersProp.GetArrayElementAtIndex(i), pathKey, i);
                h += CardGap;
            }

            h += 28f;
            return h;
        }

        private float GetListenerHeight(SerializedProperty listenerProp, string bindingKey, int index)
        {
            string key = $"{bindingKey}_listener_{index}";

            float h = CardVPad * 2;
            h += HeaderH + 4f;
            h += RowH + 2f;
            h += RowH + 2f;
            h += RowH + 2f;
            h += RowH + 4f;

            bool paramsExpanded = _paramFoldouts.TryGetValue(key, out var pe) && pe;
            if (paramsExpanded)
            {
                var paramsProp = listenerProp.FindPropertyRelative("_parameters");
                for (int p = 0; p < paramsProp.arraySize; p++)
                {
                    h += GetParamHeight(paramsProp.GetArrayElementAtIndex(p)) + 2f;
                }
            }

            return h;
        }

        private static float GetParamHeight(SerializedProperty paramProp)
        {
            var sourceProp = paramProp.FindPropertyRelative("_source");
            bool isScript = (ArgumentSource)sourceProp.enumValueIndex == ArgumentSource.Script;

            float h = 6f;
            h += RowH + 2f;
            h += RowCompact + 2f;
            if (isScript)
                h += RowCompact;
            h += 4f;
            return h;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var listenersProp = property.FindPropertyRelative("_listeners");
            string pathKey = property.propertyPath;

            Rect headerRect = new Rect(position.x, position.y, position.width, BindingHeaderH);
            DrawBindingHeader(headerRect, property, label, listenersProp, pathKey);

            if (_bindingFoldouts.TryGetValue(pathKey, out var expanded) && expanded)
            {
                float y = headerRect.yMax + 4f;

                for (int i = 0; i < listenersProp.arraySize; i++)
                {
                    var listenerProp = listenersProp.GetArrayElementAtIndex(i);
                    float h = GetListenerHeight(listenerProp, pathKey, i);
                    Rect cardRect = new Rect(position.x + 2f, y, position.width - 4f, h);
                    DrawListenerCard(cardRect, listenerProp, pathKey, i, listenersProp);
                    y += h + CardGap;
                }

                Rect addBtnRect = new Rect(position.x + 2f, y, position.width - 4f, 26f);
                DrawAddButton(addBtnRect, listenersProp);
            }

            EditorGUI.EndProperty();
        }

        private void DrawBindingHeader(Rect rect, SerializedProperty property, GUIContent label,
            SerializedProperty listenersProp, string pathKey)
        {
            bool expanded = _bindingFoldouts.TryGetValue(pathKey, out var e) && e;
            int count = listenersProp.arraySize;

            float iconSize = 18f;
            Rect iconRect = new Rect(rect.x + 2f, rect.y + (rect.height - iconSize) * 0.5f, iconSize, iconSize);
            EditorGUI.LabelField(iconRect, Styles.GetIconString("bolt"), new GUIStyle
            {
                fontSize = 13,
                normal = { textColor = Styles.Colors.Purple },
                alignment = TextAnchor.MiddleCenter,
                fixedWidth = iconSize,
                fixedHeight = iconSize
            });

            float addBtnWidth = 100f;
            float foldoutAreaWidth = rect.width - iconSize - 6f - addBtnWidth;
            Rect foldoutRect = new Rect(iconRect.xMax + 4f, rect.y, foldoutAreaWidth, rect.height);

            var foldoutContent = new GUIContent(label.text);
            float contentWidth = Styles.BindingFoldout.CalcSize(foldoutContent).x;

            EditorGUI.BeginChangeCheck();
            expanded = EditorGUI.Foldout(foldoutRect, expanded, label.text, true, Styles.BindingFoldout);
            if (EditorGUI.EndChangeCheck())
                _bindingFoldouts[pathKey] = expanded;

            float badgeX = rect.x + 4f + iconSize + 4f + contentWidth + 8f;
            float maxBadgeX = rect.xMax - addBtnWidth - 8f;
            badgeX = Mathf.Min(badgeX, maxBadgeX);

            if (badgeX + 20f < rect.xMax - addBtnWidth)
            {
                Styles.DrawBadge(new Rect(badgeX, rect.y, 80f, rect.height),
                    $"{count} listener{(count == 1 ? "" : "s")}", Styles.ListenerCountBadge);
            }

            Rect addBtnRect = new Rect(rect.xMax - addBtnWidth, rect.y, addBtnWidth, rect.height);
            if (GUI.Button(addBtnRect, Styles.GetIconString("plus") + "  Listener", Styles.AddButtonMain))
            {
                AddNewListener(listenersProp);
                _bindingFoldouts[pathKey] = true;
            }
        }

        private void AddNewListener(SerializedProperty listenersProp)
        {
            int index = listenersProp.arraySize;
            listenersProp.arraySize++;
            var newListener = listenersProp.GetArrayElementAtIndex(index);

            newListener.FindPropertyRelative("_enabled").boolValue = true;
            newListener.FindPropertyRelative("_methodName").stringValue = "";
            newListener.FindPropertyRelative("_methodDisplayName").stringValue = "";
            newListener.FindPropertyRelative("_target").objectReferenceValue = null;

            var paramsProp = newListener.FindPropertyRelative("_parameters");
            paramsProp.ClearArray();
            paramsProp.arraySize = 0;

            listenersProp.serializedObject.ApplyModifiedProperties();
            GUI.FocusControl(null);
        }

        private void DrawAddButton(Rect rect, SerializedProperty listenersProp)
        {
            Rect innerRect = new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f);
            if (GUI.Button(innerRect, "  +  Add Listener", Styles.AddButtonFooter))
            {
                AddNewListener(listenersProp);
            }
        }

        private void DrawListenerCard(Rect rect, SerializedProperty listenerProp, string bindingKey,
            int index, SerializedProperty listenersProp)
        {
            string key = $"{bindingKey}_listener_{index}";

            var enabledProp = listenerProp.FindPropertyRelative("_enabled");
            var targetProp = listenerProp.FindPropertyRelative("_target");
            var methodNameProp = listenerProp.FindPropertyRelative("_methodName");
            var methodDisplayProp = listenerProp.FindPropertyRelative("_methodDisplayName");
            var paramsProp = listenerProp.FindPropertyRelative("_parameters");

            bool isEnabled = enabledProp.boolValue;
            Color accentColor = isEnabled ? Styles.Colors.Green : Styles.Colors.TextMuted;

            if (Event.current.type == EventType.Repaint)
            {
                if (isEnabled)
                    Styles.DrawCardBackground(rect);
                else
                    Styles.DrawCardBackgroundDim(rect);
            }

            Styles.DrawAccentStrip(rect, accentColor);
            Rect innerRect = new Rect(rect.x + CardHPad, rect.y + CardVPad, rect.width - CardHPad * 2, rect.height - CardVPad * 2);
            float y = innerRect.y;

            DrawCardHeader(new Rect(innerRect.x, y, innerRect.width, HeaderH),
                listenerProp, key, index, listenersProp, bindingKey);
            y += HeaderH + 4f;

            y = DrawTargetFields(new Rect(innerRect.x, y, innerRect.width, 0),
                enabledProp, targetProp, methodNameProp, methodDisplayProp, paramsProp);

            DrawMethodRow(new Rect(innerRect.x, y, innerRect.width, RowH),
                targetProp, methodNameProp, methodDisplayProp, paramsProp, listenerProp);
            y += RowH + 2f;

            DrawParameterSection(new Rect(innerRect.x, y, innerRect.width, 0),
                paramsProp, key);
        }

        private void DrawCardHeader(Rect rect, SerializedProperty listenerProp, string key,
            int index, SerializedProperty listenersProp, string bindingKey)
        {
            var enabledProp = listenerProp.FindPropertyRelative("_enabled");
            var targetProp = listenerProp.FindPropertyRelative("_target");
            var methodDisplayProp = listenerProp.FindPropertyRelative("_methodDisplayName");

            GUI.BeginGroup(rect);

            float toggleSize = 18f;
            Rect toggleRect = new Rect(0, (rect.height - toggleSize) * 0.5f, toggleSize, toggleSize);
            EditorGUI.BeginChangeCheck();
            bool newEnabled = EditorGUI.Toggle(toggleRect, enabledProp.boolValue, Styles.ToggleStyle);
            if (EditorGUI.EndChangeCheck())
            {
                enabledProp.boolValue = newEnabled;
                enabledProp.serializedObject.ApplyModifiedProperties();
            }

            string displayName = GetListenerDisplayName(listenerProp);
            var displayStyle = enabledProp.boolValue ? Styles.CardHeaderLabel : Styles.CardHeaderLabelDim;
            Rect labelRect = new Rect(toggleRect.xMax + 4f, 0, rect.width - toggleRect.xMax - 28f, rect.height);
            EditorGUI.LabelField(labelRect, "  " + displayName, displayStyle);

            Rect removeRect = new Rect(rect.width - 22f, 0, 22f, rect.height);
            if (GUI.Button(removeRect, Styles.GetIconString("xmark"), Styles.RemoveButton))
            {
                RemoveListenerAt(listenersProp, index);
                GUIUtility.ExitGUI();
            }

            GUI.EndGroup();
        }

        private string GetListenerDisplayName(SerializedProperty listenerProp)
        {
            var targetProp = listenerProp.FindPropertyRelative("_target");
            var methodDisplayProp = listenerProp.FindPropertyRelative("_methodDisplayName");

            Component target = targetProp.objectReferenceValue as Component;

            if (target == null)
                return "No Target Selected";

            string scriptName = target.GetType().Name;
            string methodName = !string.IsNullOrEmpty(methodDisplayProp.stringValue)
                ? methodDisplayProp.stringValue.Split(':')[0].Trim()
                : "No Method Selected";

            return $"{target.gameObject.name} ({scriptName})";
        }

        private void RemoveListenerAt(SerializedProperty listenersProp, int index)
        {
            if (index < 0 || index >= listenersProp.arraySize)
                return;
            listenersProp.serializedObject.Update();
            int sizeBefore = listenersProp.arraySize;
            listenersProp.DeleteArrayElementAtIndex(index);
            if (listenersProp.arraySize == sizeBefore)
                listenersProp.DeleteArrayElementAtIndex(index);
            listenersProp.serializedObject.ApplyModifiedProperties();
        }

        private float DrawTargetFields(Rect rect, SerializedProperty enabledProp,
            SerializedProperty targetProp, SerializedProperty methodNameProp,
            SerializedProperty methodDisplayProp, SerializedProperty paramsProp)
        {
            float y = rect.y;
            Component currentTarget = targetProp.objectReferenceValue as Component;
            GameObject currentGO = currentTarget != null ? currentTarget.gameObject : null;

            Rect labelRect = new Rect(rect.x, y, 56f, RowH);
            EditorGUI.LabelField(labelRect, "Object", Styles.FieldLabel);

            Rect fieldRect = new Rect(labelRect.xMax + FieldGap, y, rect.width - labelRect.width - FieldGap, RowH);

            EditorGUI.BeginChangeCheck();
            Object newObj = EditorGUI.ObjectField(fieldRect, currentGO, typeof(Object), true);
            if (EditorGUI.EndChangeCheck())
            {
                Component resolvedComp = newObj as Component;
                GameObject resolvedGO = resolvedComp?.gameObject ?? newObj as GameObject;

                if (resolvedComp != null)
                    targetProp.objectReferenceValue = resolvedComp;
                else if (resolvedGO != null)
                {
                    targetProp.objectReferenceValue = null;
                    if (currentTarget != null)
                    {
                        Component same = resolvedGO.GetComponent(currentTarget.GetType());
                        if (same != null)
                            targetProp.objectReferenceValue = same;
                    }
                }
                else
                    targetProp.objectReferenceValue = null;

                ClearMethod(methodNameProp, methodDisplayProp, paramsProp);
                targetProp.serializedObject.ApplyModifiedProperties();
            }

            y += RowH + 2f;

            GameObject goForDropdown = currentTarget != null
                ? currentTarget.gameObject
                : (newObj as GameObject ?? (newObj as Component)?.gameObject);

            currentTarget = targetProp.objectReferenceValue as Component;

            EditorGUI.LabelField(new Rect(rect.x, y, 56f, RowH), "Script", Styles.FieldLabel);

            Rect scriptFieldRect = new Rect(labelRect.xMax + FieldGap, y, rect.width - labelRect.width - FieldGap, RowH);

            string displayName = "No Script Selected";
            if (currentTarget != null)
                displayName = currentTarget.GetType().Name;
            else if (goForDropdown != null)
                displayName = "Select Script...";

            if (GUI.Button(scriptFieldRect, displayName, Styles.PopupButton))
            {
                if (goForDropdown != null)
                    ShowComponentDropdown(scriptFieldRect, goForDropdown, targetProp, methodNameProp, methodDisplayProp, paramsProp);
            }

            y += RowH + 2f;

            return y;
        }

        private void ShowComponentDropdown(Rect rect, GameObject go, SerializedProperty targetProp,
            SerializedProperty methodNameProp, SerializedProperty methodDisplayProp, SerializedProperty paramsProp)
        {
            var components = go.GetComponents<Component>();
            var grouped = components
                .Where(c => c != null)
                .GroupBy(c => c.GetType().Namespace ?? "")
                .OrderBy(g =>
                {
                    if (string.IsNullOrEmpty(g.Key)) return 1;
                    if (g.Key.StartsWith("UnityEngine")) return 2;
                    return 0;
                })
                .ToList();

            var menu = new GenericMenu();

            foreach (var group in grouped)
            {
                string header = string.IsNullOrEmpty(group.Key) ? "Scripts" : group.Key;
                foreach (var comp in group.OrderBy(c => c.GetType().Name))
                {
                    var type = comp.GetType();
                    string label = $"{header}/{type.Name}";
                    bool isSelected = targetProp.objectReferenceValue == comp;

                    var capturedComp = comp;
                    menu.AddItem(new GUIContent(label), isSelected, () =>
                    {
                        targetProp.objectReferenceValue = capturedComp;
                        ClearMethod(methodNameProp, methodDisplayProp, paramsProp);
                        targetProp.serializedObject.ApplyModifiedProperties();
                    });
                }
            }

            if (components.Length == 0 || components.All(c => c == null))
                menu.AddDisabledItem(new GUIContent("No components found"));

            menu.DropDown(rect);
        }

        private void ClearMethod(SerializedProperty methodNameProp, SerializedProperty methodDisplayProp,
            SerializedProperty paramsProp)
        {
            methodNameProp.stringValue = "";
            methodDisplayProp.stringValue = "";
            paramsProp.ClearArray();
            paramsProp.arraySize = 0;
        }

        private void DrawMethodRow(Rect rect, SerializedProperty targetProp,
            SerializedProperty methodNameProp, SerializedProperty methodDisplayProp,
            SerializedProperty paramsProp, SerializedProperty listenerProp)
        {
            EditorGUI.LabelField(new Rect(rect.x, rect.y, 56f, rect.height), "Method", Styles.FieldLabel);

            Rect fieldRect = new Rect(rect.x + 60f, rect.y, rect.width - 60f, rect.height);

            var target = targetProp.objectReferenceValue as Component;
            string display = !string.IsNullOrEmpty(methodDisplayProp.stringValue)
                ? methodDisplayProp.stringValue
                : "No Method Selected";

            if (GUI.Button(fieldRect, display, Styles.PopupButton))
            {
                if (target != null)
                    ShowMethodDropdown(fieldRect, target, targetProp, methodNameProp, methodDisplayProp,
                        paramsProp, listenerProp);
            }
        }

        private void ShowMethodDropdown(Rect rect, Component target, SerializedProperty targetProp,
            SerializedProperty methodNameProp, SerializedProperty methodDisplayProp,
            SerializedProperty paramsProp, SerializedProperty listenerProp)
        {
            var methods = GetBindableMethods(target.GetType());
            var menu = new GenericMenu();

            foreach (var method in methods)
            {
                string sig = FormatMethodSignature(method);
                bool isSelected = method.Name == methodNameProp.stringValue;
                var capturedMethod = method;
                menu.AddItem(new GUIContent(sig), isSelected, () =>
                {
                    SelectMethod(capturedMethod, methodNameProp, methodDisplayProp, paramsProp, listenerProp);
                });
            }

            if (methods.Count == 0)
                menu.AddDisabledItem(new GUIContent("No bindable methods found"));

            menu.DropDown(rect);
        }

        private List<MethodInfo> GetBindableMethods(Type type)
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
                    if (dt == typeof(object) || dt == typeof(UnityEngine.Object) ||
                        dt == typeof(Component) || dt == typeof(Behaviour) ||
                        dt == typeof(MonoBehaviour))
                        return false;

                    return true;
                })
                .OrderBy(m => m.Name)
                .ToList();
        }

        private string FormatMethodSignature(MethodInfo method)
        {
            var parameters = method.GetParameters();
            string returnType = method.ReturnType.Name;
            string paramList = string.Join(", ",
                parameters.Select(p => $"{GetTypeDisplayName(p.ParameterType)} {p.Name}"));

            if (string.IsNullOrEmpty(paramList))
                return $"{method.Name}() : {returnType}";

            return $"{method.Name}({paramList}) : {returnType}";
        }

        private string GetTypeDisplayName(Type type)
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

        private void SelectMethod(MethodInfo method, SerializedProperty methodNameProp,
            SerializedProperty methodDisplayProp, SerializedProperty paramsProp,
            SerializedProperty listenerProp)
        {
            methodNameProp.stringValue = method.Name;
            methodDisplayProp.stringValue = FormatMethodSignature(method);

            var parameters = method.GetParameters();
            paramsProp.ClearArray();
            paramsProp.arraySize = parameters.Length;

            for (int i = 0; i < parameters.Length; i++)
            {
                var paramProp = paramsProp.GetArrayElementAtIndex(i);
                paramProp.FindPropertyRelative("_parameterName").stringValue = parameters[i].Name ?? $"param{i}";
                paramProp.FindPropertyRelative("_parameterTypeName").stringValue =
                    parameters[i].ParameterType.AssemblyQualifiedName;
                paramProp.FindPropertyRelative("_source").enumValueIndex = (int)ArgumentSource.Constant;

                ResetParamValues(paramProp);
            }

            paramsProp.serializedObject.ApplyModifiedProperties();
        }

        private void ResetParamValues(SerializedProperty paramProp)
        {
            paramProp.FindPropertyRelative("_sourceComponent").objectReferenceValue = null;
            paramProp.FindPropertyRelative("_sourceMemberName").stringValue = "";
            paramProp.FindPropertyRelative("_intValue").intValue = 0;
            paramProp.FindPropertyRelative("_floatValue").floatValue = 0f;
            paramProp.FindPropertyRelative("_doubleValue").doubleValue = 0.0;
            paramProp.FindPropertyRelative("_longValue").longValue = 0L;
            paramProp.FindPropertyRelative("_boolValue").boolValue = false;
            paramProp.FindPropertyRelative("_stringValue").stringValue = "";
            paramProp.FindPropertyRelative("_vector2Value").vector2Value = Vector2.zero;
            paramProp.FindPropertyRelative("_vector3Value").vector3Value = Vector3.zero;
            paramProp.FindPropertyRelative("_vector4Value").vector4Value = Vector4.zero;
            paramProp.FindPropertyRelative("_colorValue").colorValue = Color.white;
            paramProp.FindPropertyRelative("_rectValue").rectValue = new Rect(0, 0, 1, 1);
            paramProp.FindPropertyRelative("_boundsValue").boundsValue = new Bounds(Vector3.zero, Vector3.one);
            paramProp.FindPropertyRelative("_layerMaskValue").intValue = 0;
            paramProp.FindPropertyRelative("_objectValue").objectReferenceValue = null;
        }

        private void DrawParameterSection(Rect rect, SerializedProperty paramsProp, string key)
        {
            float y = rect.y;

            bool paramsExpanded = _paramFoldouts.TryGetValue(key, out var pe) && pe;

            Rect foldoutRect = new Rect(rect.x + 2f, y, rect.width - 2f, RowH);
            EditorGUI.BeginChangeCheck();
            paramsExpanded = EditorGUI.Foldout(foldoutRect, paramsExpanded,
                $"  Parameters  ({paramsProp.arraySize})", true, Styles.ParamSectionFoldout);
            if (EditorGUI.EndChangeCheck())
                _paramFoldouts[key] = paramsExpanded;

            if (!_paramFoldouts.ContainsKey(key))
                _paramFoldouts[key] = false;

            y += RowH + 4f;

            if (paramsExpanded)
            {
                for (int p = 0; p < paramsProp.arraySize; p++)
                {
                    var paramProp = paramsProp.GetArrayElementAtIndex(p);
                    var typeNameProp = paramProp.FindPropertyRelative("_parameterTypeName");
                    Type paramType = ResolveType(typeNameProp.stringValue);

                    float ph = GetParamHeight(paramProp);
                    Rect paramRect = new Rect(rect.x + 4f, y, rect.width - 4f, ph);
                    DrawParameterEntry(paramRect, paramProp, paramType);
                    y += ph + 2f;
                }
            }
        }

        private void DrawParameterEntry(Rect rect, SerializedProperty paramProp, Type paramType)
        {
            var nameProp = paramProp.FindPropertyRelative("_parameterName");
            var sourceProp = paramProp.FindPropertyRelative("_source");
            var sourceComponentProp = paramProp.FindPropertyRelative("_sourceComponent");
            var sourceMemberProp = paramProp.FindPropertyRelative("_sourceMemberName");

            var source = (ArgumentSource)sourceProp.enumValueIndex;
            Color accentColor = source == ArgumentSource.Constant
                ? Styles.Colors.BlueDim
                : Styles.Colors.OrangeDim;
            Color solidAccent = source == ArgumentSource.Constant
                ? Styles.Colors.Blue
                : Styles.Colors.Orange;

            if (Event.current.type == EventType.Repaint)
                Styles.DrawParamBackground(rect);

            Styles.DrawAccentStrip(rect, solidAccent);

            Rect innerRect = new Rect(rect.x + 10f, rect.y + 3f, rect.width - 14f, rect.height - 6f);
            float y = innerRect.y;

            string typeDisplay = paramType != null ? GetTypeDisplayName(paramType) : "unknown";
            string headerText = $"<color=#{ColorUtility.ToHtmlStringRGB(Styles.Colors.TextPrimary)}>{nameProp.stringValue}</color>  <color=#{ColorUtility.ToHtmlStringRGB(Styles.Colors.TextMuted)}>({typeDisplay})</color>";

            Rect headerRect = new Rect(innerRect.x, y, innerRect.width, RowH);
            EditorGUI.LabelField(headerRect, headerText, Styles.ParamHeaderLabel);
            y += RowH + 2f;

            Rect sourceLabel = new Rect(innerRect.x, y, 44f, RowCompact);
            EditorGUI.LabelField(sourceLabel, "Source", Styles.FieldLabelCompact);

            Rect sourcePopupRect = new Rect(sourceLabel.xMax + FieldGap, y, 68f, RowCompact);
            EditorGUI.PropertyField(sourcePopupRect, sourceProp, GUIContent.none);

            if (source == ArgumentSource.Constant)
            {
                Rect valueRect = new Rect(sourcePopupRect.xMax + 8f, y,
                    innerRect.xMax - sourcePopupRect.xMax - 8f, RowCompact);
                DrawConstantValueField(valueRect, paramProp, paramType);
            }
            else
            {
                float remainingWidth = innerRect.xMax - sourcePopupRect.xMax - 8f;
                float halfWidth = (remainingWidth - FieldGap) * 0.5f;

                Rect compRect = new Rect(sourcePopupRect.xMax + 8f, y, halfWidth, RowCompact);
                EditorGUI.ObjectField(compRect, sourceComponentProp, typeof(Component), GUIContent.none);

                Rect memberRect = new Rect(compRect.xMax + FieldGap, y, halfWidth, RowCompact);
                DrawScriptMemberDropdown(memberRect, sourceComponentProp, sourceMemberProp, paramType);
            }

            y += RowCompact + 2f;

            if (source == ArgumentSource.Script && sourceComponentProp.objectReferenceValue != null)
            {
                string componentName = sourceComponentProp.objectReferenceValue.name;
                string memberName = !string.IsNullOrEmpty(sourceMemberProp.stringValue)
                    ? sourceMemberProp.stringValue
                    : "?";
                Rect infoRect = new Rect(innerRect.x, y, innerRect.width, RowCompact);
                var infoStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontSize = 10,
                    normal = { textColor = Styles.Colors.TextMuted },
                    padding = new RectOffset(0, 0, 1, 1)
                };
                EditorGUI.LabelField(infoRect,
                    $"  {Styles.GetIconString("arrow")}  {componentName}.{memberName}", infoStyle);
            }
        }

        private void DrawConstantValueField(Rect rect, SerializedProperty paramProp, Type paramType)
        {
            if (paramType == null)
            {
                EditorGUI.LabelField(rect, "Unresolved type", Styles.ParamHeaderLabel);
                return;
            }

            if (paramType == typeof(int))
            {
                var prop = paramProp.FindPropertyRelative("_intValue");
                EditorGUI.PropertyField(rect, prop, GUIContent.none);
            }
            else if (paramType == typeof(float))
            {
                var prop = paramProp.FindPropertyRelative("_floatValue");
                EditorGUI.PropertyField(rect, prop, GUIContent.none);
            }
            else if (paramType == typeof(double))
            {
                var prop = paramProp.FindPropertyRelative("_doubleValue");
                EditorGUI.PropertyField(rect, prop, GUIContent.none);
            }
            else if (paramType == typeof(long))
            {
                var prop = paramProp.FindPropertyRelative("_longValue");
                EditorGUI.PropertyField(rect, prop, GUIContent.none);
            }
            else if (paramType == typeof(bool))
            {
                var prop = paramProp.FindPropertyRelative("_boolValue");
                EditorGUI.PropertyField(rect, prop, GUIContent.none);
            }
            else if (paramType == typeof(string))
            {
                var prop = paramProp.FindPropertyRelative("_stringValue");
                EditorGUI.PropertyField(rect, prop, GUIContent.none);
            }
            else if (paramType == typeof(char))
            {
                var prop = paramProp.FindPropertyRelative("_stringValue");
                string val = prop.stringValue ?? "";
                val = EditorGUI.TextField(rect, val);
                prop.stringValue = val.Length > 0 ? val[0].ToString() : "";
            }
            else if (paramType == typeof(Vector2))
            {
                var prop = paramProp.FindPropertyRelative("_vector2Value");
                EditorGUI.PropertyField(rect, prop, GUIContent.none);
            }
            else if (paramType == typeof(Vector3))
            {
                var prop = paramProp.FindPropertyRelative("_vector3Value");
                EditorGUI.PropertyField(rect, prop, GUIContent.none);
            }
            else if (paramType == typeof(Vector4))
            {
                var prop = paramProp.FindPropertyRelative("_vector4Value");
                EditorGUI.PropertyField(rect, prop, GUIContent.none);
            }
            else if (paramType == typeof(Quaternion))
            {
                var prop = paramProp.FindPropertyRelative("_vector3Value");
                EditorGUI.PropertyField(rect, prop, GUIContent.none);
            }
            else if (paramType == typeof(Color))
            {
                var prop = paramProp.FindPropertyRelative("_colorValue");
                EditorGUI.PropertyField(rect, prop, GUIContent.none);
            }
            else if (paramType == typeof(Rect))
            {
                var prop = paramProp.FindPropertyRelative("_rectValue");
                EditorGUI.PropertyField(rect, prop, GUIContent.none);
            }
            else if (paramType == typeof(Bounds))
            {
                var prop = paramProp.FindPropertyRelative("_boundsValue");
                EditorGUI.PropertyField(rect, prop, GUIContent.none);
            }
            else if (paramType == typeof(AnimationCurve))
            {
                var prop = paramProp.FindPropertyRelative("_curveValue");
                EditorGUI.PropertyField(rect, prop, GUIContent.none);
            }
            else if (paramType == typeof(Gradient))
            {
                var prop = paramProp.FindPropertyRelative("_gradientValue");
                EditorGUI.PropertyField(rect, prop, GUIContent.none);
            }
            else if (paramType == typeof(LayerMask))
            {
                var prop = paramProp.FindPropertyRelative("_layerMaskValue");
                EditorGUI.PropertyField(rect, prop, GUIContent.none);
            }
            else if (paramType.IsEnum)
            {
                var intProp = paramProp.FindPropertyRelative("_intValue");
                DrawEnumField(rect, intProp, paramType);
            }
            else if (paramType == typeof(GameObject))
            {
                var prop = paramProp.FindPropertyRelative("_objectValue");
                EditorGUI.ObjectField(rect, prop, typeof(GameObject), GUIContent.none);
            }
            else if (typeof(Component).IsAssignableFrom(paramType))
            {
                var prop = paramProp.FindPropertyRelative("_objectValue");
                EditorGUI.ObjectField(rect, prop, paramType, GUIContent.none);
            }
            else if (typeof(Object).IsAssignableFrom(paramType))
            {
                var prop = paramProp.FindPropertyRelative("_objectValue");
                EditorGUI.ObjectField(rect, prop, paramType, GUIContent.none);
            }
            else if (paramType == typeof(short))
            {
                var prop = paramProp.FindPropertyRelative("_intValue");
                int val = EditorGUI.IntField(rect, prop.intValue);
                prop.intValue = Mathf.Clamp(val, short.MinValue, short.MaxValue);
            }
            else if (paramType == typeof(byte))
            {
                var prop = paramProp.FindPropertyRelative("_intValue");
                int val = EditorGUI.IntField(rect, prop.intValue);
                prop.intValue = Mathf.Clamp(val, byte.MinValue, byte.MaxValue);
            }
            else
            {
                var prop = paramProp.FindPropertyRelative("_stringValue");
                EditorGUI.LabelField(rect, paramType.Name, prop.stringValue, Styles.ParamHeaderLabel);
            }
        }

        private void DrawEnumField(Rect rect, SerializedProperty intProp, Type enumType)
        {
            int currentValue = intProp.intValue;
            try
            {
                var enumObj = Enum.ToObject(enumType, currentValue);
                var names = Enum.GetNames(enumType);
                var values = Enum.GetValues(enumType);
                int currentIndex = -1;
                for (int i = 0; i < values.Length; i++)
                {
                    if (Equals(values.GetValue(i), enumObj))
                    {
                        currentIndex = i;
                        break;
                    }
                }

                int newIndex = EditorGUI.Popup(rect, currentIndex, names);
                if (newIndex >= 0 && newIndex < values.Length)
                    intProp.intValue = (int)values.GetValue(newIndex);
            }
            catch
            {
                EditorGUI.LabelField(rect, "Invalid enum value");
            }
        }

        private void DrawScriptMemberDropdown(Rect rect, SerializedProperty sourceComponentProp,
            SerializedProperty sourceMemberProp, Type paramType)
        {
            var comp = sourceComponentProp.objectReferenceValue as Component;

            string display = !string.IsNullOrEmpty(sourceMemberProp.stringValue)
                ? sourceMemberProp.stringValue
                : "Select...";

            if (GUI.Button(rect, display, Styles.PopupButton))
            {
                if (comp != null)
                    ShowMemberDropdown(rect, comp, sourceMemberProp, paramType);
            }
        }

        private void ShowMemberDropdown(Rect rect, Component component, SerializedProperty sourceMemberProp,
            Type paramType)
        {
            var type = component.GetType();
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0);

            var menu = new GenericMenu();

            foreach (var field in fields)
            {
                string label = $"{field.Name} : {GetTypeDisplayName(field.FieldType)}";
                bool isSelected = field.Name == sourceMemberProp.stringValue;
                var capturedName = field.Name;
                menu.AddItem(new GUIContent(label), isSelected, () =>
                {
                    sourceMemberProp.stringValue = capturedName;
                    sourceMemberProp.serializedObject.ApplyModifiedProperties();
                });
            }

            foreach (var prop in properties)
            {
                if (prop.Name == "enabled" || prop.Name == "name" || prop.Name == "tag" || prop.Name == "gameObject" ||
                    prop.Name == "transform" || prop.Name == "hideFlags")
                    continue;

                string label = $"{prop.Name} : {GetTypeDisplayName(prop.PropertyType)}";
                bool isSelected = prop.Name == sourceMemberProp.stringValue;
                var capturedName = prop.Name;
                menu.AddItem(new GUIContent(label), isSelected, () =>
                {
                    sourceMemberProp.stringValue = capturedName;
                    sourceMemberProp.serializedObject.ApplyModifiedProperties();
                });
            }

            if (fields.Length == 0 && !properties.Any())
                menu.AddDisabledItem(new GUIContent("No public fields or properties"));

            menu.DropDown(rect);
        }

        private static Type ResolveType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return null;

            Type resolved = Type.GetType(typeName);
            if (resolved != null)
                return resolved;

            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type found = assembly.GetType(typeName);
                    if (found != null)
                        return found;

                    int lastDot = typeName.LastIndexOf('.');
                    if (lastDot > 0)
                    {
                        string simpleName = typeName.Substring(lastDot + 1);
                        found = assembly.GetTypes()
                            .FirstOrDefault(t => t.Name == simpleName && t.FullName == typeName);
                        if (found != null)
                            return found;
                    }
                }
            }
            catch
            {
            }

            return null;
        }
    }
}
