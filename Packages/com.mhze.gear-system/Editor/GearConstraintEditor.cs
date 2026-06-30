using UnityEditor;
using UnityEngine;

namespace MHZE.GearSystem.Editor
{
    [CustomEditor(typeof(GearConstraintBase), true)]
    [CanEditMultipleObjects]
    public class GearConstraintEditor : UnityEditor.Editor
    {
        private SerializedProperty m_GearA;
        private SerializedProperty m_MeshA;
        private SerializedProperty m_GearB;
        private SerializedProperty m_MeshB;
        private SerializedProperty m_RadiusA;
        private SerializedProperty m_RadiusB;
        private SerializedProperty m_AxisA;
        private SerializedProperty m_AxisB;
        private SerializedProperty m_ToothCountA;
        private SerializedProperty m_ToothCountB;
        private SerializedProperty m_ToothHeight;
        private SerializedProperty m_ToothWidth;
        private SerializedProperty m_MeshOffset;
        private SerializedProperty m_DebugDraw;
        private SerializedProperty m_DebugLog;

        private bool m_ShowGearA = true;
        private bool m_ShowGearB = true;
        private bool m_ShowVisual;
        private bool m_ShowDebug;

        private void OnEnable()
        {
            m_GearA = serializedObject.FindProperty("gearA");
            m_MeshA = serializedObject.FindProperty("meshA");
            m_GearB = serializedObject.FindProperty("gearB");
            m_MeshB = serializedObject.FindProperty("meshB");
            m_RadiusA = serializedObject.FindProperty("radiusA");
            m_RadiusB = serializedObject.FindProperty("radiusB");
            m_AxisA = serializedObject.FindProperty("axisA");
            m_AxisB = serializedObject.FindProperty("axisB");
            m_ToothCountA = serializedObject.FindProperty("toothCountA");
            m_ToothCountB = serializedObject.FindProperty("toothCountB");
            m_ToothHeight = serializedObject.FindProperty("toothHeight");
            m_ToothWidth = serializedObject.FindProperty("toothWidth");
            m_MeshOffset = serializedObject.FindProperty("meshOffset");
            m_DebugDraw = serializedObject.FindProperty("debugDraw");
            m_DebugLog = serializedObject.FindProperty("debugLog");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(4);

            DrawGearSection("Gear A", m_GearA, m_MeshA, m_RadiusA, m_AxisA, m_ToothCountA, ref m_ShowGearA);
            DrawGearSection("Gear B", m_GearB, m_MeshB, m_RadiusB, m_AxisB, m_ToothCountB, ref m_ShowGearB);

            m_ShowVisual = EditorGUILayout.Foldout(m_ShowVisual, "Visual", true, EditorStyles.foldoutHeader);
            if (m_ShowVisual)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_ToothHeight, new GUIContent("Tooth Height"));
                EditorGUILayout.PropertyField(m_ToothWidth, new GUIContent("Tooth Width"));
                EditorGUILayout.PropertyField(m_MeshOffset, new GUIContent("Mesh Offset", "Angular offset for gear mesh alignment (degrees)."));
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.Space(2);
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

        private static void DrawGearSection(string label, SerializedProperty transformProp, SerializedProperty meshProp, SerializedProperty radiusProp, SerializedProperty axisProp, SerializedProperty toothCountProp, ref bool show)
        {
            show = EditorGUILayout.Foldout(show, label, true, EditorStyles.foldoutHeader);
            if (!show) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(transformProp, new GUIContent("Transform"));
            EditorGUILayout.PropertyField(meshProp, new GUIContent("Mesh Transform"));
            EditorGUILayout.PropertyField(radiusProp, new GUIContent("Radius"));
            EditorGUILayout.PropertyField(axisProp, new GUIContent("Axis"));
            EditorGUILayout.PropertyField(toothCountProp, new GUIContent("Tooth Count"));
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(2);
        }
    }
}
