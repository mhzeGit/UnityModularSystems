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

        private bool m_Editing;

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

            DrawHeaderEditButton();

            EditorGUILayout.PropertyField(m_IsTrigger);
            EditorGUILayout.PropertyField(m_ProvidesContacts);
            EditorGUILayout.PropertyField(m_Material);
            EditorGUILayout.PropertyField(m_Center);
            EditorGUILayout.PropertyField(m_Radius);
            EditorGUILayout.PropertyField(m_Height);
            EditorGUILayout.PropertyField(m_Direction);
            EditorGUILayout.PropertyField(m_LayerOverridePriority);
            EditorGUILayout.PropertyField(m_IncludeLayers);
            EditorGUILayout.PropertyField(m_ExcludeLayers);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawHeaderEditButton()
        {
            var icon = EditorGUIUtility.IconContent("EditCollider");
            icon.tooltip = "Edit Collider";

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            var prevBg = GUI.backgroundColor;
            if (!m_Editing)
                GUI.backgroundColor = new Color32(0x14, 0x14, 0x14, 0xFF);

            m_Editing = GUILayout.Toggle(m_Editing, icon, "Button", GUILayout.Width(44), GUILayout.Height(22));
            if (GUI.changed)
                SceneView.RepaintAll();

            GUI.backgroundColor = prevBg;

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
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

                int n = Mathf.Min(m_Target.sides, 24);
                Vector3 prevTop = topCenter + b1 * m_Target.radius;
                Vector3 prevBot = botCenter + b1 * m_Target.radius;

                for (int i = 1; i <= n; i++)
                {
                    float angle = 2f * Mathf.PI * i / n;
                    Vector3 dir = b1 * Mathf.Cos(angle) + b2 * Mathf.Sin(angle);

                    Vector3 currTop = topCenter + dir * m_Target.radius;
                    Vector3 currBot = botCenter + dir * m_Target.radius;

                    Handles.DrawLine(prevTop, currTop);
                    Handles.DrawLine(prevBot, currBot);
                    Handles.DrawLine(prevTop, prevBot);

                    prevTop = currTop;
                    prevBot = currBot;
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
