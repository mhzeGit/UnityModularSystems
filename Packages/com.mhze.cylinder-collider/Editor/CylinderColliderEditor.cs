using UnityEditor;
using UnityEngine;

namespace MHZE.CylinderCollider.Editor
{
    [CustomEditor(typeof(CylinderCollider))]
    [CanEditMultipleObjects]
    public class CylinderColliderEditor : UnityEditor.Editor
    {
        private CylinderCollider m_Target;
        private SerializedProperty m_Center;
        private SerializedProperty m_Radius;
        private SerializedProperty m_Height;
        private SerializedProperty m_Sides;
        private SerializedProperty m_Direction;
        private SerializedProperty m_Material;
        private SerializedProperty m_IsTrigger;
        private SerializedProperty m_ProvidesContacts;
        private SerializedProperty m_LayerOverridePriority;
        private SerializedProperty m_IncludeLayers;
        private SerializedProperty m_ExcludeLayers;

        private static readonly string[] m_DirectionNames = { "X-Axis", "Y-Axis", "Z-Axis" };
        private static readonly int[] m_DirectionValues = { 0, 1, 2 };

        private bool m_Editing;
        private bool m_ShowLayerOverrides = true;

        private void OnEnable()
        {
            m_Target = (CylinderCollider)target;

            m_Center = serializedObject.FindProperty("m_Center");
            m_Radius = serializedObject.FindProperty("m_Radius");
            m_Height = serializedObject.FindProperty("m_Height");
            m_Sides = serializedObject.FindProperty("m_Sides");
            m_Direction = serializedObject.FindProperty("m_Direction");
            m_Material = serializedObject.FindProperty("m_Material");
            m_IsTrigger = serializedObject.FindProperty("m_IsTrigger");
            m_ProvidesContacts = serializedObject.FindProperty("m_ProvidesContacts");
            m_LayerOverridePriority = serializedObject.FindProperty("m_LayerOverridePriority");
            m_IncludeLayers = serializedObject.FindProperty("m_IncludeLayers");
            m_ExcludeLayers = serializedObject.FindProperty("m_ExcludeLayers");
        }

        private void OnDisable()
        {
            m_Editing = false;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawEditColliderField();

            EditorGUILayout.PropertyField(m_IsTrigger);
            EditorGUILayout.PropertyField(m_ProvidesContacts);
            EditorGUILayout.PropertyField(m_Material);
            EditorGUILayout.PropertyField(m_Center);
            EditorGUILayout.PropertyField(m_Radius);
            EditorGUILayout.PropertyField(m_Height);
            EditorGUILayout.PropertyField(m_Sides);
            m_Direction.intValue = EditorGUILayout.IntPopup("Direction", m_Direction.intValue, m_DirectionNames, m_DirectionValues);
            m_ShowLayerOverrides = EditorGUILayout.Foldout(m_ShowLayerOverrides, "Layer Overrides", true);
            if (m_ShowLayerOverrides)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_LayerOverridePriority);
                EditorGUILayout.PropertyField(m_IncludeLayers);
                EditorGUILayout.PropertyField(m_ExcludeLayers);
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawEditColliderField()
        {
            var icon = EditorGUIUtility.IconContent("EditCollider");
            icon.tooltip = "Edit Collider";

            var btnStyle = new GUIStyle("Button");
            btnStyle.margin = new RectOffset(0, 0, 0, 0);
            btnStyle.padding = new RectOffset(0, 0, 0, 0);

            var labelStyle = new GUIStyle(EditorStyles.label);
            labelStyle.padding.top = -6;

            GUILayout.Space(1);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Edit Collider"), EditorStyles.label, labelStyle);

            var prevBg = GUI.backgroundColor;
            if (!m_Editing)
                GUI.backgroundColor = new Color32(14, 14, 14, 0xFF);

            m_Editing = GUILayout.Toggle(m_Editing, icon, btnStyle, GUILayout.Width(34), GUILayout.Height(21));
            if (GUI.changed)
                SceneView.RepaintAll();

            GUI.backgroundColor = prevBg;

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(3);
        }

        private void OnSceneGUI()
        {
            if (!m_Editing)
                return;

            var transform = m_Target.transform;

            Vector3 axisLS, b1, b2;
            switch (m_Target.direction)
            {
                case 0: axisLS = Vector3.right; b1 = Vector3.up; b2 = Vector3.forward; break;
                case 2: axisLS = Vector3.forward; b1 = Vector3.right; b2 = Vector3.up; break;
                default: axisLS = Vector3.up; b1 = Vector3.right; b2 = Vector3.forward; break;
            }

            var matrix = transform.localToWorldMatrix;

            float halfH = m_Target.height * 0.5f;
            Vector3 topPos = m_Target.center + axisLS * halfH;
            Vector3 botPos = m_Target.center - axisLS * halfH;

            using (new Handles.DrawingScope(new Color(0f, 1f, 0.2f, 0.2f), matrix))
            {
                Handles.DrawWireDisc(topPos, axisLS, m_Target.radius);
                Handles.DrawWireDisc(botPos, axisLS, m_Target.radius);

                for (int i = 0; i < m_Target.sides; i++)
                {
                    float angle = 2f * Mathf.PI * i / m_Target.sides;
                    Vector3 dir = b1 * Mathf.Cos(angle) + b2 * Mathf.Sin(angle);
                    Handles.DrawLine(topPos + dir * m_Target.radius, botPos + dir * m_Target.radius);
                }
            }

            using (new Handles.DrawingScope(new Color(0.4f, 1f, 0.4f, 0.5f), matrix))
            {
                Handles.DrawDottedLine(topPos, botPos, 2f);

                EditorGUI.BeginChangeCheck();
                Vector3 newTop = Handles.Slider(topPos, axisLS, 0.015f, Handles.DotHandleCap, 0f);
                Vector3 newBot = Handles.Slider(botPos, -axisLS, 0.015f, Handles.DotHandleCap, 0f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(m_Target, "Change Cylinder Height");
                    float newTopDist = Vector3.Dot(newTop - m_Target.center, axisLS);
                    float newBotDist = Vector3.Dot(m_Target.center - newBot, axisLS);

                    if (!Mathf.Approximately(newTopDist, halfH))
                    {
                        newTopDist = Mathf.Max(newTopDist, 0.001f - halfH);
                        m_Target.center += axisLS * (newTopDist - halfH) * 0.5f;
                        m_Target.height = newTopDist + halfH;
                    }
                    else if (!Mathf.Approximately(newBotDist, halfH))
                    {
                        newBotDist = Mathf.Max(newBotDist, 0.001f - halfH);
                        m_Target.center -= axisLS * (newBotDist - halfH) * 0.5f;
                        m_Target.height = halfH + newBotDist;
                    }
                }
            }

            using (new Handles.DrawingScope(new Color(0.4f, 1f, 0.4f, 0.5f), matrix))
            {
                Vector3 radiusPos = m_Target.center + b1 * m_Target.radius;
                EditorGUI.BeginChangeCheck();
                Vector3 newRadiusPos = Handles.Slider(radiusPos, b1, 0.015f, Handles.DotHandleCap, 0f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(m_Target, "Change Cylinder Radius");
                    m_Target.radius = Mathf.Max(0.001f, Vector3.Dot(newRadiusPos - m_Target.center, b1));
                }
            }
        }
    }
}
