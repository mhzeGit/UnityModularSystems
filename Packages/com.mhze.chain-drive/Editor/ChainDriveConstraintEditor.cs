using UnityEditor;
using UnityEngine;

namespace MHZE.ChainDrive.Editor
{
    [CustomEditor(typeof(ChainDriveConstraint), true)]
    [CanEditMultipleObjects]
    public class ChainDriveConstraintEditor : UnityEditor.Editor
    {
        private SerializedProperty m_Gears;
        private SerializedProperty m_Axis;
        private SerializedProperty m_ChainBallRadius;
        private SerializedProperty m_ChainBallMass;
        private SerializedProperty m_ChainLinkCount;
        private SerializedProperty m_JointSpring;
        private SerializedProperty m_JointDamper;
        private SerializedProperty m_JointMaxForce;
        private SerializedProperty m_CreateGearJoints;
        private SerializedProperty m_ToothHeight;
        private SerializedProperty m_ToothWidth;
        private SerializedProperty m_OverlapSphereRadius;
        private SerializedProperty m_OverlapCheckInterval;
        private SerializedProperty m_DebugDraw;

        private bool m_ShowGears = true;
        private bool m_ShowChainLinks = true;
        private bool m_ShowJoints = true;
        private bool m_ShowGearTeeth = true;

        private void OnEnable()
        {
            m_Gears = serializedObject.FindProperty("gears");
            m_Axis = serializedObject.FindProperty("axis");
            m_ChainBallRadius = serializedObject.FindProperty("chainBallRadius");
            m_ChainBallMass = serializedObject.FindProperty("chainBallMass");
            m_ChainLinkCount = serializedObject.FindProperty("chainLinkCount");
            m_JointSpring = serializedObject.FindProperty("jointSpring");
            m_JointDamper = serializedObject.FindProperty("jointDamper");
            m_JointMaxForce = serializedObject.FindProperty("jointMaxForce");
            m_CreateGearJoints = serializedObject.FindProperty("createGearJoints");
            m_ToothHeight = serializedObject.FindProperty("toothHeight");
            m_ToothWidth = serializedObject.FindProperty("toothWidth");
            m_OverlapSphereRadius = serializedObject.FindProperty("overlapSphereRadius");
            m_OverlapCheckInterval = serializedObject.FindProperty("overlapCheckInterval");
            m_DebugDraw = serializedObject.FindProperty("debugDraw");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(4);

            m_ShowGears = EditorGUILayout.Foldout(m_ShowGears, "Gears", true, EditorStyles.foldoutHeader);
            if (m_ShowGears)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_Gears, new GUIContent("Gear Definitions"), true);
                EditorGUILayout.PropertyField(m_Axis, new GUIContent("Chain Axis", "Axis of the chain drive plane."));
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(2);
            }

            m_ShowChainLinks = EditorGUILayout.Foldout(m_ShowChainLinks, "Chain Links", true, EditorStyles.foldoutHeader);
            if (m_ShowChainLinks)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_ChainBallRadius, new GUIContent("Ball Radius", "Radius of each chain link sphere."));
                EditorGUILayout.PropertyField(m_ChainBallMass, new GUIContent("Ball Mass", "Mass of each chain link."));
                EditorGUILayout.PropertyField(m_ChainLinkCount, new GUIContent("Link Count", "Number of chain links in the loop."));
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(2);
            }

            m_ShowJoints = EditorGUILayout.Foldout(m_ShowJoints, "Joint Physics", true, EditorStyles.foldoutHeader);
            if (m_ShowJoints)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_JointSpring, new GUIContent("Spring", "Spring force keeping chain links at fixed distance."));
                EditorGUILayout.PropertyField(m_JointDamper, new GUIContent("Damper", "Damping for the joint spring."));
                EditorGUILayout.PropertyField(m_JointMaxForce, new GUIContent("Max Force", "Maximum force for gear-chain joints."));
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(2);
            }

            m_ShowGearTeeth = EditorGUILayout.Foldout(m_ShowGearTeeth, "Gear Teeth Overlap", true, EditorStyles.foldoutHeader);
            if (m_ShowGearTeeth)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_CreateGearJoints, new GUIContent("Create Gear Joints", "Automatically create joints between chain links and overlapping gear teeth."));
                if (m_CreateGearJoints.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(m_ToothHeight, new GUIContent("Tooth Height", "Height of gear teeth for sphere position offset."));
                    EditorGUILayout.PropertyField(m_ToothWidth, new GUIContent("Tooth Width", "Angular width of one tooth for alignment."));
                    EditorGUILayout.PropertyField(m_OverlapSphereRadius, new GUIContent("Overlap Sphere Radius", "Radius of virtual gear tooth spheres."));
                    EditorGUILayout.PropertyField(m_OverlapCheckInterval, new GUIContent("Check Interval", "Frames between overlap checks."));
                    EditorGUI.indentLevel--;
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.Space(2);
            EditorGUILayout.PropertyField(m_DebugDraw, new GUIContent("Debug Draw", "Draw the belt path, gear circles, and tooth positions in the Scene view."));

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Build Chain", GUILayout.Height(30)))
            {
                foreach (var t in targets)
                {
                    ChainDriveConstraint c = (ChainDriveConstraint)t;
                    if (Application.isPlaying)
                        c.BuildChain();
                    else
                        Debug.Log("Chain can only be built in Play mode.");
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
