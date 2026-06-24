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

            Vector3 axisLS;
            Quaternion radiusRotLS;
            switch (m_Target.direction)
            {
                case 0:
                    axisLS = Vector3.right;
                    radiusRotLS = Quaternion.LookRotation(Vector3.right, Vector3.forward);
                    break;
                case 2:
                    axisLS = Vector3.forward;
                    radiusRotLS = Quaternion.LookRotation(Vector3.forward, Vector3.up);
                    break;
                default:
                    axisLS = Vector3.up;
                    radiusRotLS = Quaternion.LookRotation(Vector3.up, Vector3.forward);
                    break;
            }

            Vector3 b1, b2;
            switch (m_Target.direction)
            {
                case 0: b1 = Vector3.up; b2 = Vector3.forward; break;
                case 2: b1 = Vector3.right; b2 = Vector3.up; break;
                default: b1 = Vector3.right; b2 = Vector3.forward; break;
            }

            var matrix = transform.localToWorldMatrix;

            using (new Handles.DrawingScope(new Color(0f, 1f, 0.2f, 0.4f), matrix))
            {
                float halfH = m_Target.height * 0.5f;
                Vector3 topCenter = m_Target.center + axisLS * halfH;
                Vector3 botCenter = m_Target.center - axisLS * halfH;

                Handles.DrawWireDisc(topCenter, axisLS, m_Target.radius);
                Handles.DrawWireDisc(botCenter, axisLS, m_Target.radius);

                for (int i = 0; i < 4; i++)
                {
                    float angle = 2f * Mathf.PI * i / 4;
                    Vector3 dir = b1 * Mathf.Cos(angle) + b2 * Mathf.Sin(angle);
                    Handles.DrawLine(topCenter + dir * m_Target.radius, botCenter + dir * m_Target.radius);
                }
            }

            using (new Handles.DrawingScope(new Color(0f, 1f, 0.2f, 0.8f), matrix))
            {
                EditorGUI.BeginChangeCheck();
                float newRadius = Handles.RadiusHandle(radiusRotLS, m_Target.center, m_Target.radius);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(m_Target, "Change Cylinder Radius");
                    m_Target.radius = Mathf.Max(0.001f, newRadius);
                }
            }

            using (new Handles.DrawingScope(new Color(0f, 1f, 0.2f, 1f), matrix))
            {
                float halfH = m_Target.height * 0.5f;
                Vector3 topPos = m_Target.center + axisLS * halfH;
                Vector3 botPos = m_Target.center - axisLS * halfH;

                Handles.DrawDottedLine(topPos, botPos, 2f);

                EditorGUI.BeginChangeCheck();
                Vector3 newTop = Handles.Slider(topPos, axisLS, 0.05f, Handles.SphereHandleCap, 0f);
                Vector3 newBot = Handles.Slider(botPos, -axisLS, 0.05f, Handles.SphereHandleCap, 0f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(m_Target, "Change Cylinder Height");
                    float topDist = Vector3.Dot(newTop - m_Target.center, axisLS);
                    float botDist = Vector3.Dot(m_Target.center - newBot, axisLS);
                    m_Target.height = Mathf.Max(0.001f, topDist + botDist);
                }
            }
        }
    }
}
