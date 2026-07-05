using UnityEditor;
using UnityEngine;
using System.IO;

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
        private SerializedProperty m_RotationOffset;
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
            m_RotationOffset = serializedObject.FindProperty("rotationOffset");
            m_GenerateOnAwake = serializedObject.FindProperty("generateOnAwake");
            m_AssignMeshCollider = serializedObject.FindProperty("assignMeshCollider");
            m_GeneratedMesh = serializedObject.FindProperty("generatedMesh");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(4);

            DrawGeometryFoldout();
            DrawQualityFoldout();
            DrawAutoFoldout();

            EditorGUILayout.PropertyField(m_GeneratedMesh, new GUIContent("Generated Mesh"));

            EditorGUILayout.Space(6);

            if (GUILayout.Button("Generate Mesh", GUILayout.Height(32)))
            {
                foreach (var t in targets)
                    GenerateAndSave((GearMeshGenerator)t);
            }

            EditorGUILayout.Space(2);

            if (GUILayout.Button("Clear Mesh", GUILayout.Height(24)))
            {
                foreach (var t in targets)
                    ClearMesh((GearMeshGenerator)t);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawGeometryFoldout()
        {
            m_ShowGeometry = EditorGUILayout.Foldout(m_ShowGeometry, "Gear Geometry", true, EditorStyles.foldoutHeader);
            if (!m_ShowGeometry) return;
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(m_ToothCount, new GUIContent("Tooth Count"));
            EditorGUILayout.PropertyField(m_PitchRadius, new GUIContent("Pitch Radius"));
            EditorGUILayout.PropertyField(m_ToothHeight, new GUIContent("Tooth Height"));
            EditorGUILayout.PropertyField(m_ToothWidthAngle, new GUIContent("Tooth Width (deg)"));
            EditorGUILayout.PropertyField(m_Thickness, new GUIContent("Thickness"));
            EditorGUILayout.PropertyField(m_Axis, new GUIContent("Axis"));
                EditorGUILayout.PropertyField(m_CenterHoleRadiusFraction, new GUIContent("Center Hole Size", "Fraction of pitch radius (0 = solid disc)"));
                EditorGUILayout.PropertyField(m_RotationOffset, new GUIContent("Rotation Offset", "Rotate all teeth around the gear axis (degrees). Useful for aligning teeth between meshing gears."));
                EditorGUI.indentLevel--;
            EditorGUILayout.Space(2);
        }

        private void DrawQualityFoldout()
        {
            m_ShowQuality = EditorGUILayout.Foldout(m_ShowQuality, "Mesh Quality", true, EditorStyles.foldoutHeader);
            if (!m_ShowQuality) return;
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(m_SegmentsPerTooth, new GUIContent("Segments Per Tooth"));
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(2);
        }

        private void DrawAutoFoldout()
        {
            m_ShowAuto = EditorGUILayout.Foldout(m_ShowAuto, "Auto Generation", true, EditorStyles.foldoutHeader);
            if (!m_ShowAuto) return;
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(m_GenerateOnAwake, new GUIContent("Generate On Awake"));
            EditorGUILayout.PropertyField(m_AssignMeshCollider, new GUIContent("Assign Mesh Collider"));
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(2);
        }

        private void GenerateAndSave(GearMeshGenerator gen)
        {
            // 1. Delete old mesh asset if it exists
            Mesh oldMesh = gen.generatedMesh;
            if (oldMesh != null)
            {
                string oldPath = AssetDatabase.GetAssetPath(oldMesh);
                if (!string.IsNullOrEmpty(oldPath))
                {
                    // It's a saved asset — delete the file first, then clear references.
                    // Setting generatedMesh to null prevents Generate() from trying to
                    // DestroyImmediate an already-deleted object.
                    gen.generatedMesh = null;

                    MeshFilter mf = gen.GetComponent<MeshFilter>();
                    if (mf != null) mf.sharedMesh = null;

                    MeshCollider mc = gen.GetComponent<MeshCollider>();
                    if (mc != null) mc.sharedMesh = null;

                    AssetDatabase.DeleteAsset(oldPath);
                    AssetDatabase.Refresh();
                }
                // else: not an asset — Generate() will DestroyImmediate it
            }

            // 2. Generate new mesh
            gen.Generate();

            // 3. Save as asset so it persists in prefabs
            Mesh newMesh = gen.generatedMesh;
            if (newMesh == null || !AssetDatabase.GetAssetPath(newMesh).Equals(""))
                return;

            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gen);
            if (!string.IsNullOrEmpty(prefabPath))
            {
                // Save as sub-asset of the prefab
                var prefabAsset = AssetDatabase.LoadAssetAtPath<Object>(prefabPath);
                if (prefabAsset != null)
                {
                    newMesh.name = $"{gen.gameObject.name}_GearMesh";
                    AssetDatabase.AddObjectToAsset(newMesh, prefabAsset);
                }
            }
            else
            {
                // Not part of a prefab — save as a standalone asset
                string dir = "Assets/GeneratedMeshes";
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string path = Path.Combine(dir, $"{gen.gameObject.name}_GearMesh.asset");
                path = AssetDatabase.GenerateUniqueAssetPath(path);
                newMesh.name = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(newMesh, path);
            }

            AssetDatabase.SaveAssets();
            EditorUtility.SetDirty(gen);
        }

        private void ClearMesh(GearMeshGenerator gen)
        {
            // If the mesh is a saved asset, delete it
            if (gen.generatedMesh != null)
            {
                string path = AssetDatabase.GetAssetPath(gen.generatedMesh);
                if (!string.IsNullOrEmpty(path))
                {
                    gen.generatedMesh = null;
                    AssetDatabase.DeleteAsset(path);
                }
                else
                {
                    DestroyImmediate(gen.generatedMesh);
                    gen.generatedMesh = null;
                }
            }

            MeshFilter mf = gen.GetComponent<MeshFilter>();
            if (mf != null) mf.sharedMesh = null;

            MeshCollider mc = gen.GetComponent<MeshCollider>();
            if (mc != null) mc.sharedMesh = null;

            EditorUtility.SetDirty(gen);
        }
    }
}
