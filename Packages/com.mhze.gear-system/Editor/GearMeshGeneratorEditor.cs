using UnityEditor;
using UnityEngine;
using System.IO;

namespace MHZE.GearSystem.Editor
{
    [CustomEditor(typeof(GearMeshGenerator))]
    [CanEditMultipleObjects]
    public class GearMeshGeneratorEditor : UnityEditor.Editor
    {
        private SerializedProperty m_GearDensity;
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
        private bool m_IsAutoGenerating;
        private string[] m_PreviousGeometryHashes;

        private void OnEnable()
        {
            m_GearDensity = serializedObject.FindProperty("gearDensity");
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

            var targetsArray = targets;
            m_PreviousGeometryHashes = new string[targetsArray.Length];
            for (int i = 0; i < targetsArray.Length; i++)
                m_PreviousGeometryHashes[i] = ((GearMeshGenerator)targetsArray[i]).GetGeometryHash();
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

            AutoSaveIfGeometryChanged();

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
            EditorGUILayout.PropertyField(m_GearDensity, new GUIContent("Gear Density", "Teeth per unit of pitch radius. Same density = same tooth spacing regardless of radius."));
            EditorGUILayout.PropertyField(m_PitchRadius, new GUIContent("Pitch Radius"));
            EditorGUILayout.PropertyField(m_ToothHeight, new GUIContent("Tooth Height"));
                EditorGUILayout.PropertyField(m_ToothWidth, new GUIContent("Tooth Width", "Absolute width of one tooth at the pitch circle (world units). Stays constant regardless of radius and tooth count."));
            EditorGUILayout.PropertyField(m_Thickness, new GUIContent("Thickness"));
            EditorGUILayout.PropertyField(m_Axis, new GUIContent("Axis"));
                EditorGUILayout.PropertyField(m_CenterHoleRadiusFraction, new GUIContent("Center Hole Size", "Fraction of pitch radius (0 = solid disc)"));
                EditorGUILayout.Slider(m_RotationOffset, 0f, 1f, new GUIContent("Rotation Offset", "Fraction of one tooth pitch to offset all teeth. Useful for aligning teeth between meshing gears."));
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
            string oldAssetPath = gen.m_GeneratedMeshAssetPath;

            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gen);
            if (!string.IsNullOrEmpty(prefabPath))
            {
                // For prefabs, clean up old asset before generating sub-asset
                if (!string.IsNullOrEmpty(oldAssetPath))
                {
                    gen.generatedMesh = null;
                    gen.m_GeneratedMeshAssetPath = null;
                    if (AssetDatabase.LoadAssetAtPath<Mesh>(oldAssetPath) != null)
                        AssetDatabase.DeleteAsset(oldAssetPath);
                }
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

            // Cache hit — same path and asset exists, just ensure it's assigned
            if (gen.m_GeneratedMeshAssetPath == cachePath)
            {
                Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(cachePath);
                if (existingMesh != null)
                {
                    MeshFilter mf = gen.GetComponent<MeshFilter>();
                    if (mf == null) mf = gen.gameObject.AddComponent<MeshFilter>();
                    MeshRenderer mr = gen.GetComponent<MeshRenderer>();
                    if (mr == null) mr = gen.gameObject.AddComponent<MeshRenderer>();
                    mf.sharedMesh = existingMesh;
                    gen.generatedMesh = existingMesh;
                    EditorUtility.SetDirty(gen);
                    return;
                }
            }

            string oldAssetPath = gen.m_GeneratedMeshAssetPath;

            // Load or generate the new mesh FIRST, before touching sharedMesh
            Mesh newMesh = AssetDatabase.LoadAssetAtPath<Mesh>(cachePath);
            if (newMesh == null)
            {
                gen.Generate();
                newMesh = gen.generatedMesh;
                if (newMesh == null) return;
                newMesh.name = Path.GetFileNameWithoutExtension(cachePath);
                AssetDatabase.CreateAsset(newMesh, cachePath);
            }

            // Assign the new mesh (no null window in scene view)
            gen.generatedMesh = newMesh;
            gen.m_GeneratedMeshAssetPath = cachePath;

            MeshFilter mf2 = gen.GetComponent<MeshFilter>();
            if (mf2 == null) mf2 = gen.gameObject.AddComponent<MeshFilter>();
            MeshRenderer mr2 = gen.GetComponent<MeshRenderer>();
            if (mr2 == null) mr2 = gen.gameObject.AddComponent<MeshRenderer>();
            mf2.sharedMesh = newMesh;

            // Delete old asset only after new mesh is live
            if (!string.IsNullOrEmpty(oldAssetPath) && oldAssetPath != cachePath)
            {
                if (AssetDatabase.LoadAssetAtPath<Mesh>(oldAssetPath) != null)
                    AssetDatabase.DeleteAsset(oldAssetPath);
            }

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

        private void AutoSaveIfGeometryChanged()
        {
            if (EditorApplication.isPlaying || m_IsAutoGenerating) return;

            var targetsArray = targets;
            if (m_PreviousGeometryHashes == null || m_PreviousGeometryHashes.Length != targetsArray.Length)
                m_PreviousGeometryHashes = new string[targetsArray.Length];

            bool changed = false;
            for (int i = 0; i < targetsArray.Length; i++)
            {
                var gen = (GearMeshGenerator)targetsArray[i];
                string hash = gen.GetGeometryHash();
                if (hash != m_PreviousGeometryHashes[i])
                {
                    m_PreviousGeometryHashes[i] = hash;
                    changed = true;
                }
            }

            if (!changed) return;

            m_IsAutoGenerating = true;
            foreach (var t in targetsArray)
            {
                var gen = (GearMeshGenerator)t;
                string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gen);
                if (string.IsNullOrEmpty(prefabPath))
                    GenerateNewOrReuseStandalone(gen);
            }
            m_IsAutoGenerating = false;
        }
    }
}
