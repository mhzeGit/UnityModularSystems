using UnityEditor;
using UnityEngine;

namespace MHZE.GearSystem.Editor
{
    [CustomEditor(typeof(GearConstraint), true)]
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
        private SerializedProperty m_GearDensityA;
        private SerializedProperty m_GearDensityB;
        private SerializedProperty m_ToothHeight;
        private SerializedProperty m_ToothWidth;
        private SerializedProperty m_MeshOffsetA;
        private SerializedProperty m_MeshOffsetB;
        private SerializedProperty m_OverlapSphereRadius;
        private SerializedProperty m_OverlapCheckInterval;
        private SerializedProperty m_SphereRadiusOffsetA;
        private SerializedProperty m_SphereRadiusOffsetB;
        private SerializedProperty m_CreateJoints;
        private SerializedProperty m_JointSpring;
        private SerializedProperty m_JointDamper;
        private SerializedProperty m_JointMaxForce;
        private SerializedProperty m_DebugColorA;
        private SerializedProperty m_DebugColorB;
        private SerializedProperty m_DebugShowOverlaps;
        private SerializedProperty m_DebugDraw;
        private SerializedProperty m_DebugLog;
        private SerializedProperty m_SpawnLookAt;

        private bool m_ShowGearA = true;
        private bool m_ShowGearB = true;
        private bool m_ShowVisual;
        private bool m_ShowDebug;
        private bool m_ShowLookAt;

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
            m_GearDensityA = serializedObject.FindProperty("gearDensityA");
            m_GearDensityB = serializedObject.FindProperty("gearDensityB");
            m_ToothHeight = serializedObject.FindProperty("toothHeight");
            m_ToothWidth = serializedObject.FindProperty("toothWidth");
            m_MeshOffsetA = serializedObject.FindProperty("meshOffsetA");
            m_MeshOffsetB = serializedObject.FindProperty("meshOffsetB");
            m_OverlapSphereRadius = serializedObject.FindProperty("overlapSphereRadius");
            m_OverlapCheckInterval = serializedObject.FindProperty("overlapCheckInterval");
            m_SphereRadiusOffsetA = serializedObject.FindProperty("sphereRadiusOffsetA");
            m_SphereRadiusOffsetB = serializedObject.FindProperty("sphereRadiusOffsetB");
            m_CreateJoints = serializedObject.FindProperty("createJoints");
            m_JointSpring = serializedObject.FindProperty("jointSpring");
            m_JointDamper = serializedObject.FindProperty("jointDamper");
            m_JointMaxForce = serializedObject.FindProperty("jointMaxForce");
            m_DebugColorA = serializedObject.FindProperty("debugColorA");
            m_DebugColorB = serializedObject.FindProperty("debugColorB");
            m_DebugShowOverlaps = serializedObject.FindProperty("debugShowOverlaps");
            m_DebugDraw = serializedObject.FindProperty("debugDraw");
            m_DebugLog = serializedObject.FindProperty("debugLog");
            m_SpawnLookAt = serializedObject.FindProperty("spawnLookAt");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(4);

            DrawGearSection("Gear A", m_GearA, m_MeshA, m_RadiusA, m_AxisA, m_GearDensityA, m_SphereRadiusOffsetA, m_MeshOffsetA, ref m_ShowGearA);
            DrawGearSection("Gear B", m_GearB, m_MeshB, m_RadiusB, m_AxisB, m_GearDensityB, m_SphereRadiusOffsetB, m_MeshOffsetB, ref m_ShowGearB);

            m_ShowVisual = EditorGUILayout.Foldout(m_ShowVisual, "Visual", true, EditorStyles.foldoutHeader);
            if (m_ShowVisual)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_ToothHeight, new GUIContent("Tooth Height"));
                EditorGUILayout.PropertyField(m_ToothWidth, new GUIContent("Tooth Width"));
                EditorGUILayout.PropertyField(m_OverlapSphereRadius, new GUIContent("Overlap Sphere Radius"));
                EditorGUILayout.PropertyField(m_OverlapCheckInterval, new GUIContent("Overlap Check Interval", "Frames between overlap checks. 0 = disabled."));
                EditorGUILayout.PropertyField(m_CreateJoints, new GUIContent("Create Joints", "Create spring joints at overlapping tooth sphere positions."));
                if (m_CreateJoints.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(m_JointSpring, new GUIContent("Spring", "Spring force pulling contact spheres together."));
                    EditorGUILayout.PropertyField(m_JointDamper, new GUIContent("Damper", "Damping for the joint spring."));
                    EditorGUILayout.PropertyField(m_JointMaxForce, new GUIContent("Max Force", "Maximum force the spring can apply."));
                    EditorGUI.indentLevel--;
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.Space(2);
            m_ShowDebug = EditorGUILayout.Foldout(m_ShowDebug, "Debug", true, EditorStyles.foldoutHeader);
            if (m_ShowDebug)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_DebugColorA, new GUIContent("Debug Color A"));
                EditorGUILayout.PropertyField(m_DebugColorB, new GUIContent("Debug Color B"));
                EditorGUILayout.PropertyField(m_DebugDraw, new GUIContent("Debug Draw", "Draw gear gizmos in the Scene view."));
                EditorGUILayout.PropertyField(m_DebugShowOverlaps, new GUIContent("Debug Show Overlaps", "Highlight overlapping spheres."));
                EditorGUILayout.PropertyField(m_DebugLog, new GUIContent("Debug Log", "Log constraint values to console every 60 frames."));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(2);
            m_ShowLookAt = EditorGUILayout.Foldout(m_ShowLookAt, "Look At", true, EditorStyles.foldoutHeader);
            if (m_ShowLookAt)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_SpawnLookAt, new GUIContent("Spawn Look At",
                    "Required: spawns a helper GameObject that defines the joint's drive axis. The joint only applies spring force along the UP/DOWN axis of this LookAt transform."));
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawGearSection(string label, SerializedProperty transformProp, SerializedProperty meshProp, SerializedProperty radiusProp, SerializedProperty axisProp, SerializedProperty gearDensityProp, SerializedProperty sphereOffsetProp, SerializedProperty meshOffsetProp, ref bool show)
        {
            show = EditorGUILayout.Foldout(show, label, true, EditorStyles.foldoutHeader);
            if (!show) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(transformProp, new GUIContent("Transform"));
            EditorGUILayout.PropertyField(meshProp, new GUIContent("Mesh Transform"));
            EditorGUILayout.PropertyField(radiusProp, new GUIContent("Radius"));
            EditorGUILayout.PropertyField(axisProp, new GUIContent("Axis"));
            EditorGUILayout.PropertyField(gearDensityProp, new GUIContent("Gear Density", "Teeth per unit of pitch radius. Same density = same tooth spacing regardless of radius."));
            EditorGUILayout.Slider(sphereOffsetProp, 0f, 1f, new GUIContent("Sphere Offset", "0 = at radius, 1 = at tooth tip."));
            EditorGUILayout.Slider(meshOffsetProp, 0f, 1f, new GUIContent("Mesh Offset", "Fraction of one tooth pitch for mesh alignment."));
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(2);
        }
    }
}
