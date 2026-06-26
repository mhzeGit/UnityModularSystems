using UnityEditor;
using UnityEngine;

namespace MHZE.GearSystem.Editor
{
    [CustomEditor(typeof(GearConstraint))]
    [CanEditMultipleObjects]
    public class GearConstraintEditor : UnityEditor.Editor
    {
        private SerializedProperty m_GearA;
        private SerializedProperty m_GearB;
        private SerializedProperty m_MeshA;
        private SerializedProperty m_MeshB;
        private SerializedProperty m_RadiusA;
        private SerializedProperty m_RadiusB;
        private SerializedProperty m_AxisA;
        private SerializedProperty m_AxisB;
        private SerializedProperty m_ToothDensity;
        private SerializedProperty m_ToothHeight;
        private SerializedProperty m_DebugDraw;
        private SerializedProperty m_MaxTorque;
        private SerializedProperty m_DebugLog;

        private bool m_ShowGearA = true;
        private bool m_ShowGearB = true;
        private bool m_ShowVisual;
        private bool m_ShowDebug;

        private void OnEnable()
        {
            m_GearA = serializedObject.FindProperty("m_GearA");
            m_GearB = serializedObject.FindProperty("m_GearB");
            m_MeshA = serializedObject.FindProperty("m_MeshA");
            m_MeshB = serializedObject.FindProperty("m_MeshB");
            m_RadiusA = serializedObject.FindProperty("m_RadiusA");
            m_RadiusB = serializedObject.FindProperty("m_RadiusB");
            m_AxisA = serializedObject.FindProperty("m_AxisA");
            m_AxisB = serializedObject.FindProperty("m_AxisB");
            m_ToothDensity = serializedObject.FindProperty("m_ToothDensity");
            m_ToothHeight = serializedObject.FindProperty("m_ToothHeight");
            m_DebugDraw = serializedObject.FindProperty("m_DebugDraw");
            m_MaxTorque = serializedObject.FindProperty("m_MaxTorque");
            m_DebugLog = serializedObject.FindProperty("m_DebugLog");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(4);

            DrawGearSection("Gear A", m_GearA, m_MeshA, m_RadiusA, m_AxisA, ref m_ShowGearA);
            DrawGearSection("Gear B", m_GearB, m_MeshB, m_RadiusB, m_AxisB, ref m_ShowGearB);

            m_ShowVisual = EditorGUILayout.Foldout(m_ShowVisual, "Visual", true, EditorStyles.foldoutHeader);
            if (m_ShowVisual)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_ToothDensity, new GUIContent("Tooth Density"));
                EditorGUILayout.PropertyField(m_ToothHeight, new GUIContent("Tooth Height"));
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.Space(2);
            EditorGUILayout.PropertyField(m_MaxTorque, new GUIContent("Max Torque", "Maximum constraint torque (Nm). 0 = unlimited."));

            m_ShowDebug = EditorGUILayout.Foldout(m_ShowDebug, "Debug", true, EditorStyles.foldoutHeader);
            if (m_ShowDebug)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_DebugDraw, new GUIContent("Debug Draw", "Draw gear gizmos in the Scene view."));
                EditorGUILayout.PropertyField(m_DebugLog, new GUIContent("Debug Log", "Log constraint values to console every 60 frames."));
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawGearSection(string label, SerializedProperty transformProp, SerializedProperty meshProp, SerializedProperty radiusProp, SerializedProperty axisProp, ref bool show)
        {
            show = EditorGUILayout.Foldout(show, label, true, EditorStyles.foldoutHeader);
            if (!show) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(transformProp, new GUIContent("Transform"));
            EditorGUILayout.PropertyField(meshProp, new GUIContent("Mesh Transform", "Visual mesh transform for gizmo drawing."));
            EditorGUILayout.PropertyField(radiusProp, new GUIContent("Radius"));
            EditorGUILayout.PropertyField(axisProp, new GUIContent("Axis"));
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(2);
        }
    }
}
