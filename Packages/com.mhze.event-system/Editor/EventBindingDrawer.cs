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
        private const float CardPadding = 8f;
        private const float CardSpacing = 4f;
        private const float HeaderHeight = 20f;
        private const float LineHeight = 18f;
        private const float CompactLineHeight = 16f;
        private const float IndentWidth = 12f;

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

            float height = EditorGUIUtility.singleLineHeight + 4f;

            var listenersProp = property.FindPropertyRelative("_listeners");
            for (int i = 0; i < listenersProp.arraySize; i++)
            {
                height += GetListenerHeight(listenersProp.GetArrayElementAtIndex(i), pathKey, i) + CardSpacing;
            }

            height += LineHeight + 6f;

            return height;
        }

        private float GetListenerHeight(SerializedProperty listenerProp, string bindingKey, int index)
        {
            string key = $"{bindingKey}_listener_{index}";

            float h = 4f;
            h += HeaderHeight + 4f;
            h += LineHeight + 2f;
            h += LineHeight + 2f;
            h += LineHeight + 2f;
            h += LineHeight + 2f;

            bool paramsExpanded = _paramFoldouts.TryGetValue(key, out var pe) && pe;
            if (paramsExpanded)
            {
                var paramsProp = listenerProp.FindPropertyRelative("_parameters");
                for (int p = 0; p < paramsProp.arraySize; p++)
                {
                    h += GetParamHeight(paramsProp.GetArrayElementAtIndex(p)) + 2f;
                }
            }

            h += 4f;
            return h;
        }

        private float GetParamHeight(SerializedProperty paramProp)
        {
            var sourceProp = paramProp.FindPropertyRelative("_source");
            bool isScript = (ArgumentSource)sourceProp.enumValueIndex == ArgumentSource.Script;

            float h = 2f;
            h += LineHeight + 2f;
            h += CompactLineHeight + 2f;
            if (isScript)
                h += CompactLineHeight;
            h += 2f;

            return h;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var listenersProp = property.FindPropertyRelative("_listeners");
            string pathKey = property.propertyPath;

            position.xMin += 3f;
            position.width = Mathf.Max(position.width - 3f, 50f);

            Rect headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            DrawHeader(headerRect, property, label, listenersProp, pathKey);

            if (_bindingFoldouts.TryGetValue(pathKey, out var expanded) && expanded)
            {
                float y = headerRect.yMax + 2f;

                for (int i = 0; i < listenersProp.arraySize; i++)
                {
                    var listenerProp = listenersProp.GetArrayElementAtIndex(i);
                    float h = GetListenerHeight(listenerProp, pathKey, i);
                    Rect cardRect = new Rect(position.x + 2f, y, position.width - 4f, h);

                    DrawListenerCard(cardRect, listenerProp, pathKey, i, listenersProp);

                    y += h + CardSpacing;
                }

                Rect addBtnRect = new Rect(position.x + 2f, y, position.width - 4f, LineHeight + 4f);
                DrawAddButton(addBtnRect, listenersProp);
            }

            EditorGUI.EndProperty();
        }

        private void DrawHeader(Rect rect, SerializedProperty property, GUIContent label,
            SerializedProperty listenersProp, string pathKey)
        {
            bool expanded = _bindingFoldouts.TryGetValue(pathKey, out var e) && e;

            Rect foldoutRect = new Rect(rect.x, rect.y, rect.width - 100f, rect.height);

            EditorGUI.BeginChangeCheck();
            expanded = EditorGUI.Foldout(foldoutRect, expanded, label, true, Styles.Foldout);
            if (EditorGUI.EndChangeCheck())
                _bindingFoldouts[pathKey] = expanded;

            int count = listenersProp.arraySize;
            Rect countRect = new Rect(foldoutRect.xMax + 4f, rect.y, 80f, rect.height);
            EditorGUI.LabelField(countRect, $"({count} listener{(count == 1 ? "" : "s")})", Styles.ListenerLabel);

            Rect addBtnRect = new Rect(rect.xMax - 88f, rect.y, 88f, rect.height);
            if (GUI.Button(addBtnRect, "+ Add Listener", Styles.AddButton))
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
            if (GUI.Button(rect, "+ Add Listener", Styles.AddButton))
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

            Styles.DrawCardBackground(rect);

            Rect innerRect = new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, rect.height - 8f);

            float y = innerRect.y;

            Rect headerBgRect = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, HeaderHeight);
            EditorGUI.DrawRect(headerBgRect, Styles.DarkHeaderBg);

            Rect enabledRect = new Rect(innerRect.x, y, 16f, HeaderHeight);
            enabledProp.boolValue = EditorGUI.Toggle(enabledRect, enabledProp.boolValue, Styles.Toggle);

            string displayLabel = GetListenerDisplayName(listenerProp);
            Rect labelRect = new Rect(enabledRect.xMax + 4f, y, innerRect.width - enabledRect.xMax - 70f, HeaderHeight);
            EditorGUI.LabelField(labelRect, displayLabel, Styles.CardHeaderLabel);

            Rect removeRect = new Rect(innerRect.xMax - 20f, y + 2f, 20f, 16f);
            if (GUI.Button(removeRect, "\u2715", Styles.RemoveButton))
            {
                RemoveListenerAt(listenersProp, index);
                GUIUtility.ExitGUI();
                return;
            }

            y += HeaderHeight + 4f;

            y = DrawTargetSelection(new Rect(innerRect.x, y, innerRect.width, 0),
                targetProp, methodNameProp, methodDisplayProp, paramsProp);

            Rect methodRect = new Rect(innerRect.x, y, innerRect.width, LineHeight);
            DrawMethodField(methodRect, targetProp, methodNameProp, methodDisplayProp, paramsProp, listenerProp);
            y += LineHeight + 2f;

            Rect foldoutRect = new Rect(innerRect.x + 12f, y, innerRect.width - 12f, LineHeight);
            EditorGUI.BeginChangeCheck();
            bool paramsExpanded = _paramFoldouts.TryGetValue(key, out var pe) && pe;
            paramsExpanded = EditorGUI.Foldout(foldoutRect, paramsExpanded, "Parameters", true);
            if (EditorGUI.EndChangeCheck())
                _paramFoldouts[key] = paramsExpanded;
            y += LineHeight + 2f;

            if (paramsExpanded)
            {
                for (int p = 0; p < paramsProp.arraySize; p++)
                {
                    var paramProp = paramsProp.GetArrayElementAtIndex(p);
                    float ph = GetParamHeight(paramProp);

                    var typeNameProp = paramProp.FindPropertyRelative("_parameterTypeName");
                    Type paramType = ResolveType(typeNameProp.stringValue);

                    Rect paramRect = new Rect(innerRect.x + 12f, y, innerRect.width - 12f, ph);
                    DrawParameterEntry(paramRect, paramProp, paramType);
                    y += ph + 2f;
                }
            }

            if (!_paramFoldouts.ContainsKey(key))
                _paramFoldouts[key] = false;
        }

        private string GetListenerDisplayName(SerializedProperty listenerProp)
        {
            var targetProp = listenerProp.FindPropertyRelative("_target");
            var methodDisplayProp = listenerProp.FindPropertyRelative("_methodDisplayName");

            Component target = targetProp.objectReferenceValue as Component;

            if (target == null)
                return "No Target";

            string scriptName = target.GetType().Name;
            string methodName = !string.IsNullOrEmpty(methodDisplayProp.stringValue)
                ? methodDisplayProp.stringValue.Split(':')[0].Trim()
                : "No Method";

            return $"{target.gameObject.name} ({scriptName}).{methodName}";
        }

        private void RemoveListenerAt(SerializedProperty listenersProp, int index)
        {
            if (index < 0 || index >= listenersProp.arraySize)
                return;

            int sizeBefore = listenersProp.arraySize;
            listenersProp.DeleteArrayElementAtIndex(index);
            if (listenersProp.arraySize == sizeBefore)
                listenersProp.DeleteArrayElementAtIndex(index);

            listenersProp.serializedObject.ApplyModifiedProperties();
        }

        private float DrawTargetSelection(Rect rect, SerializedProperty targetProp,
            SerializedProperty methodNameProp, SerializedProperty methodDisplayProp, SerializedProperty paramsProp)
        {
            float y = rect.y;
            Component currentTarget = targetProp.objectReferenceValue as Component;
            GameObject currentGO = currentTarget != null ? currentTarget.gameObject : null;

            Rect objLabel = new Rect(rect.x, y, 58f, LineHeight);
            EditorGUI.LabelField(objLabel, "Object", Styles.ParamLabel);

            Rect objField = new Rect(objLabel.xMax + 4f, y, rect.width - objLabel.xMax - 4f, LineHeight);

            EditorGUI.BeginChangeCheck();
            Object newObj = EditorGUI.ObjectField(objField, currentGO, typeof(Object), true);
            if (EditorGUI.EndChangeCheck())
            {
                Component resolvedComp = newObj as Component;
                GameObject resolvedGO = resolvedComp?.gameObject ?? newObj as GameObject;

                if (resolvedComp != null)
                {
                    targetProp.objectReferenceValue = resolvedComp;
                }
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
                {
                    targetProp.objectReferenceValue = null;
                }

                ClearMethod(methodNameProp, methodDisplayProp, paramsProp);
                targetProp.serializedObject.ApplyModifiedProperties();
            }

            GameObject newGO = newObj as GameObject ?? (newObj as Component)?.gameObject;

            y += LineHeight + 2f;

            currentTarget = targetProp.objectReferenceValue as Component;
            GameObject goForDropdown = currentTarget != null ? currentTarget.gameObject : (newGO != null ? newGO : null);

            Rect scriptLabel = new Rect(rect.x, y, 58f, LineHeight);
            EditorGUI.LabelField(scriptLabel, "Script", Styles.ParamLabel);

            Rect scriptField = new Rect(scriptLabel.xMax + 4f, y, rect.width - scriptLabel.xMax - 4f, LineHeight);

            string displayName = "No Script Selected";
            if (currentTarget != null)
                displayName = currentTarget.GetType().Name;
            else if (goForDropdown != null)
                displayName = "Select Script...";

            if (GUI.Button(scriptField, displayName, EditorStyles.popup))
            {
                if (goForDropdown != null)
                    ShowComponentDropdown(scriptField, goForDropdown, targetProp, methodNameProp, methodDisplayProp, paramsProp);
            }

            y += LineHeight + 2f;

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
            {
                menu.AddDisabledItem(new GUIContent("No components found"));
            }

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

        private void DrawMethodField(Rect rect, SerializedProperty targetProp, SerializedProperty methodNameProp,
            SerializedProperty methodDisplayProp, SerializedProperty paramsProp, SerializedProperty listenerProp)
        {
            Rect labelRect = new Rect(rect.x, rect.y, 52f, rect.height);
            EditorGUI.LabelField(labelRect, "Method", Styles.ParamLabel);

            Rect fieldRect = new Rect(labelRect.xMax + 4f, rect.y, rect.width - labelRect.width - 4f, rect.height);

            var target = targetProp.objectReferenceValue as Component;
            string display = !string.IsNullOrEmpty(methodDisplayProp.stringValue)
                ? methodDisplayProp.stringValue
                : "No Method";

            if (GUI.Button(fieldRect, display, EditorStyles.popup))
            {
                if (target != null)
                {
                    ShowMethodDropdown(fieldRect, target, targetProp, methodNameProp, methodDisplayProp,
                        paramsProp, listenerProp);
                }
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
            {
                menu.AddDisabledItem(new GUIContent("No bindable methods found"));
            }

            menu.DropDown(rect);
        }

        private List<MethodInfo> GetBindableMethods(Type type)
        {
            return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                .Where(m =>
                {
                    if (m.IsSpecialName)
                        return false;
                    if (m.IsGenericMethod || m.ContainsGenericParameters)
                        return false;
                    if (UnitySpecialMethods.Contains(m.Name))
                        return false;
                    if (m.GetParameters().Any(p => p.IsOut || p.ParameterType.IsByRef))
                        return false;
                    if (m.IsDefined(typeof(ObsoleteAttribute), true))
                        return false;

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

        private void DrawParameterEntry(Rect rect, SerializedProperty paramProp, Type paramType)
        {
            var nameProp = paramProp.FindPropertyRelative("_parameterName");
            var typeNameProp = paramProp.FindPropertyRelative("_parameterTypeName");
            var sourceProp = paramProp.FindPropertyRelative("_source");
            var sourceComponentProp = paramProp.FindPropertyRelative("_sourceComponent");
            var sourceMemberProp = paramProp.FindPropertyRelative("_sourceMemberName");

            var source = (ArgumentSource)sourceProp.enumValueIndex;
            Color accentColor = source == ArgumentSource.Constant ? Styles.ConstantAccent : Styles.ScriptAccent;

            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, rect.height), Styles.ParamBg);

            Rect accentStrip = new Rect(rect.x, rect.y, 3f, rect.height);
            EditorGUI.DrawRect(accentStrip, accentColor);

            Rect innerRect = new Rect(rect.x + 8f, rect.y + 2f, rect.width - 12f, rect.height - 4f);
            float y = innerRect.y;

            string typeDisplay = paramType != null ? GetTypeDisplayName(paramType) : "unknown";
            string headerText = $"{nameProp.stringValue}  ({typeDisplay})";

            Rect headerRect = new Rect(innerRect.x, y, innerRect.width, LineHeight);
            EditorGUI.LabelField(headerRect, headerText, Styles.ParamLabel);
            y += LineHeight + 2f;

            Rect sourceRect = new Rect(innerRect.x, y, 48f, CompactLineHeight);
            EditorGUI.LabelField(sourceRect, "Source", Styles.ParamLabel);

            Rect sourcePopupRect = new Rect(sourceRect.xMax + 2f, y, 72f, CompactLineHeight);
            EditorGUI.PropertyField(sourcePopupRect, sourceProp, GUIContent.none);

            if (source == ArgumentSource.Constant)
            {
                Rect valueRect = new Rect(sourcePopupRect.xMax + 8f, y,
                    innerRect.xMax - sourcePopupRect.xMax - 8f, CompactLineHeight);
                DrawConstantValueField(valueRect, paramProp, paramType);
            }
            else
            {
                float remainingWidth = innerRect.xMax - sourcePopupRect.xMax - 8f;

                Rect compRect = new Rect(sourcePopupRect.xMax + 8f, y, remainingWidth * 0.5f, CompactLineHeight);
                EditorGUI.ObjectField(compRect, sourceComponentProp, typeof(Component), GUIContent.none);

                Rect memberRect = new Rect(compRect.xMax + 4f, y, remainingWidth * 0.5f - 4f, CompactLineHeight);
                DrawScriptMemberDropdown(memberRect, sourceComponentProp, sourceMemberProp, paramType);
            }

            y += CompactLineHeight + 2f;

            if (source == ArgumentSource.Script && sourceComponentProp.objectReferenceValue != null)
            {
                string componentName = sourceComponentProp.objectReferenceValue.name;
                string memberName = !string.IsNullOrEmpty(sourceMemberProp.stringValue)
                    ? sourceMemberProp.stringValue
                    : "?";
                Rect infoRect = new Rect(innerRect.x, y, innerRect.width, CompactLineHeight);
                EditorGUI.LabelField(infoRect, $"\u2192 {componentName}.{memberName}", Styles.ParamLabel);
            }
        }

        private void DrawConstantValueField(Rect rect, SerializedProperty paramProp, Type paramType)
        {
            if (paramType == null)
            {
                EditorGUI.LabelField(rect, "Unresolved type", Styles.ParamLabel);
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
                EditorGUI.LabelField(rect, paramType.Name, prop.stringValue, Styles.ParamLabel);
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
                {
                    intProp.intValue = (int)values.GetValue(newIndex);
                }
            }
            catch
            {
                EditorGUI.LabelField(rect, "Invalid enum value", Styles.ParamLabel);
            }
        }

        private void DrawScriptMemberDropdown(Rect rect, SerializedProperty sourceComponentProp,
            SerializedProperty sourceMemberProp, Type paramType)
        {
            var comp = sourceComponentProp.objectReferenceValue as Component;

            string display = !string.IsNullOrEmpty(sourceMemberProp.stringValue)
                ? sourceMemberProp.stringValue
                : "Select Member...";

            if (GUI.Button(rect, display, EditorStyles.popup))
            {
                if (comp != null)
                {
                    ShowMemberDropdown(rect, comp, sourceMemberProp, paramType);
                }
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
            {
                menu.AddDisabledItem(new GUIContent("No public fields or properties"));
            }

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
