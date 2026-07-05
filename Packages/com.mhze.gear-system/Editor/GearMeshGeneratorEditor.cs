using UnityEditor;
using UnityEngine;

namespace MHZE.GearSystem.Editor
{
    [CustomEditor(typeof(GearMeshGenerator))]
    [CanEditMultipleObjects]
    public class GearMeshGeneratorEditor : UnityEditor.Editor
    {
        private SerializedProperty m_ToothCount;
        private SerializedProperty m_PitchRadius;
        private SerializedProperty m_ToothHeight;
        private SerializedProperty m_ToothWidthAngle;
        private SerializedProperty m_Thickness;
        private SerializedProperty m_Axis;
        private SerializedProperty m_CenterHoleRadiusFraction;
        private SerializedProperty m_SegmentsPerTooth;
        private SerializedProperty m_GenerateOnAwake;
        private SerializedProperty m_AssignMeshCollider;
        private SerializedProperty m_GeneratedMesh;

        private bool m_ShowGeometry = true;
        private bool m_ShowQuality = true;
        private bool m_ShowAuto = true;

        private void OnEnable()
        {
            m_ToothCount = serializedObject.FindProperty("toothCount");
            m_PitchRadius = serializedObject.FindProperty("pitchRadius");
            m_ToothHeight = serializedObject.FindProperty("toothHeight");
            m_ToothWidthAngle = serializedObject.FindProperty("toothWidthAngle");
            m_Thickness = serializedObject.FindProperty("thickness");
            m_Axis = serializedObject.FindProperty("axis");
            m_CenterHoleRadiusFraction = serializedObject.FindProperty("centerHoleRadiusFraction");
            m_SegmentsPerTooth = serializedObject.FindProperty("segmentsPerTooth");
            m_GenerateOnAwake = serializedObject.FindProperty("generateOnAwake");
            m_AssignMeshCollider = serializedObject.FindProperty("assignMeshCollider");
            m_GeneratedMesh = serializedObject.FindProperty("generatedMesh");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(4);

            m_ShowGeometry = EditorGUILayout.Foldout(m_ShowGeometry, "Gear Geometry", true, EditorStyles.foldoutHeader);
            if (m_ShowGeometry)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_ToothCount, new GUIContent("Tooth Count"));
                EditorGUILayout.PropertyField(m_PitchRadius, new GUIContent("Pitch Radius"));
                EditorGUILayout.PropertyField(m_ToothHeight, new GUIContent("Tooth Height"));
                EditorGUILayout.PropertyField(m_ToothWidthAngle, new GUIContent("Tooth Width (deg)"));
                EditorGUILayout.PropertyField(m_Thickness, new GUIContent("Thickness"));
                EditorGUILayout.PropertyField(m_Axis, new GUIContent("Axis"));
                EditorGUILayout.PropertyField(m_CenterHoleRadiusFraction, new GUIContent("Center Hole Size", "Fraction of pitch radius (0 = solid disc)"));
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(2);
            }

            m_ShowQuality = EditorGUILayout.Foldout(m_ShowQuality, "Mesh Quality", true, EditorStyles.foldoutHeader);
            if (m_ShowQuality)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_SegmentsPerTooth, new GUIContent("Segments Per Tooth"));
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(2);
            }

            m_ShowAuto = EditorGUILayout.Foldout(m_ShowAuto, "Auto Generation", true, EditorStyles.foldoutHeader);
            if (m_ShowAuto)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_GenerateOnAwake, new GUIContent("Generate On Awake"));
                EditorGUILayout.PropertyField(m_AssignMeshCollider, new GUIContent("Assign Mesh Collider"));
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.PropertyField(m_GeneratedMesh, new GUIContent("Generated Mesh"));

            EditorGUILayout.Space(6);

            if (GUILayout.Button("Generate Mesh", GUILayout.Height(32)))
            {
                foreach (var t in targets)
                {
                    ((GearMeshGenerator)t).Generate();
                }
            }

            EditorGUILayout.Space(2);

            if (GUILayout.Button("Clear Mesh", GUILayout.Height(24)))
            {
                foreach (var t in targets)
                {
                    var gen = (GearMeshGenerator)t;
                    if (gen.generatedMesh != null)
                    {
                        DestroyImmediate(gen.generatedMesh);
                        gen.generatedMesh = null;
                    }

                    MeshFilter mf = gen.GetComponent<MeshFilter>();
                    if (mf != null)
                        mf.sharedMesh = null;

                    MeshCollider mc = gen.GetComponent<MeshCollider>();
                    if (mc != null)
                        mc.sharedMesh = null;
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
