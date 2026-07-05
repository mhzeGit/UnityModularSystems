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
        private SerializedProperty m_ToothWidth;
        private SerializedProperty m_Thickness;
        private SerializedProperty m_Axis;
        private SerializedProperty m_CenterHoleRadiusFraction;
        private SerializedProperty m_SegmentsPerTooth;
        private SerializedProperty m_RotationOffset;
        private SerializedProperty m_GenerateOnAwake;
        private SerializedProperty m_GeneratedMesh;

        private bool m_ShowGeometry = true;
        private bool m_ShowQuality = true;
        private bool m_ShowAuto = true;

        private void OnEnable()
        {
            m_ToothCount = serializedObject.FindProperty("toothCount");
            m_PitchRadius = serializedObject.FindProperty("pitchRadius");
            m_ToothHeight = serializedObject.FindProperty("toothHeight");
            m_ToothWidth = serializedObject.FindProperty("toothWidth");
            m_Thickness = serializedObject.FindProperty("thickness");
            m_Axis = serializedObject.FindProperty("axis");
            m_CenterHoleRadiusFraction = serializedObject.FindProperty("centerHoleRadiusFraction");
            m_SegmentsPerTooth = serializedObject.FindProperty("segmentsPerTooth");
            m_RotationOffset = serializedObject.FindProperty("rotationOffset");
            m_GenerateOnAwake = serializedObject.FindProperty("generateOnAwake");
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

            serializedObject.ApplyModifiedProperties();

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
        }

        private void DrawGeometryFoldout()
        {
            m_ShowGeometry = EditorGUILayout.Foldout(m_ShowGeometry, "Gear Geometry", true, EditorStyles.foldoutHeader);
            if (!m_ShowGeometry) return;
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(m_ToothCount, new GUIContent("Tooth Count"));
            EditorGUILayout.PropertyField(m_PitchRadius, new GUIContent("Pitch Radius"));
            EditorGUILayout.PropertyField(m_ToothHeight, new GUIContent("Tooth Height"));
                EditorGUILayout.PropertyField(m_ToothWidth, new GUIContent("Tooth Width", "Absolute width of one tooth at the pitch circle (world units). Stays constant regardless of radius and tooth count."));
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
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(2);
        }

        private void GenerateAndSave(GearMeshGenerator gen)
        {
            Mesh oldMesh = gen.generatedMesh;
            string oldPath = oldMesh != null ? AssetDatabase.GetAssetPath(oldMesh) : "";
            bool isOwned = !string.IsNullOrEmpty(oldPath) && oldPath == gen.m_GeneratedMeshAssetPath;

            // ── Delete our old asset (if we own it) ──
            // If the mesh was inherited from a duplicate (path mismatch),
            // leave the original's asset alone.
            if (isOwned)
            {
                gen.generatedMesh = null;

                MeshFilter mf = gen.GetComponent<MeshFilter>();
                if (mf != null) mf.sharedMesh = null;

                AssetDatabase.DeleteAsset(oldPath);
            }
            else if (!string.IsNullOrEmpty(oldPath))
            {
                gen.generatedMesh = null;

                MeshFilter mf = gen.GetComponent<MeshFilter>();
                if (mf != null) mf.sharedMesh = null;
            }

            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gen);
            if (!string.IsNullOrEmpty(prefabPath))
            {
                GenerateNewSubAsset(gen, prefabPath);
            }
            else
            {
                GenerateNewOrReuseStandalone(gen);
            }
        }

        private void GenerateNewOrReuseStandalone(GearMeshGenerator gen)
        {
            string dir = "Assets/GeneratedMeshes";
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string hash = gen.GetGeometryHash();
            string cachePath = Path.Combine(dir, $"GearMesh_{hash}.asset");

            // Try to reuse existing cached mesh with matching geometry
            Mesh cachedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(cachePath);
            if (cachedMesh != null)
            {
                MeshFilter mf = gen.GetComponent<MeshFilter>();
                if (mf == null) mf = gen.gameObject.AddComponent<MeshFilter>();
                MeshRenderer mr = gen.GetComponent<MeshRenderer>();
                if (mr == null) mr = gen.gameObject.AddComponent<MeshRenderer>();

                mf.sharedMesh = cachedMesh;
                gen.generatedMesh = cachedMesh;
                gen.m_GeneratedMeshAssetPath = cachePath;

                AssetDatabase.SaveAssets();
                EditorUtility.SetDirty(gen);
                return;
            }

            // Generate new mesh and save with hash-based name
            gen.Generate();
            Mesh newMesh = gen.generatedMesh;
            if (newMesh == null) return;

            newMesh.name = Path.GetFileNameWithoutExtension(cachePath);
            AssetDatabase.CreateAsset(newMesh, cachePath);
            gen.m_GeneratedMeshAssetPath = cachePath;

            AssetDatabase.SaveAssets();
            EditorUtility.SetDirty(gen);
        }

        private void GenerateNewSubAsset(GearMeshGenerator gen, string prefabPath)
        {
            gen.Generate();
            Mesh newMesh = gen.generatedMesh;
            if (newMesh == null) return;

            var prefabAsset = AssetDatabase.LoadAssetAtPath<Object>(prefabPath);
            if (prefabAsset == null) return;

            newMesh.name = $"{gen.gameObject.name}_GearMesh";
            AssetDatabase.AddObjectToAsset(newMesh, prefabAsset);
            gen.m_GeneratedMeshAssetPath = prefabPath;

            AssetDatabase.SaveAssets();
            EditorUtility.SetDirty(gen);
        }

        private void ClearMesh(GearMeshGenerator gen)
        {
            if (gen.generatedMesh != null)
            {
                string path = AssetDatabase.GetAssetPath(gen.generatedMesh);
                if (!string.IsNullOrEmpty(path))
                {
                    gen.generatedMesh = null;
                    gen.m_GeneratedMeshAssetPath = null;
                    AssetDatabase.DeleteAsset(path);
                }
                else
                {
                    DestroyImmediate(gen.generatedMesh);
                    gen.generatedMesh = null;
                    gen.m_GeneratedMeshAssetPath = null;
                }
            }

            MeshFilter mf = gen.GetComponent<MeshFilter>();
            if (mf != null) mf.sharedMesh = null;

            EditorUtility.SetDirty(gen);
        }
    }
}
