using System;
using System.IO;
using MHZE.PickupSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MHZE.ItemIcons
{
    /// <summary>The menu entries. Each one only calls <see cref="ItemIconGenerator"/>.</summary>
    public static class ItemIconMenu
    {
        const string Root = "Tools/MHZE/Item Icons/";

        [MenuItem(Root + "Generate All Icons")]
        static void GenerateAll() => Show(ItemIconGenerator.GenerateAll());

        [MenuItem(Root + "Generate Missing Icons")]
        static void GenerateMissing() => Show(ItemIconGenerator.GenerateAll(null, true));

        [MenuItem(Root + "Report (What Would Be Drawn)")]
        static void ReportOnly() => Show(ItemIconGenerator.Plan());

        [MenuItem(Root + "Select Settings (Create If Missing)")]
        static void SelectSettings()
        {
            ItemIconSettings settings = ItemIconSettings.Find();
            if (settings == null)
            {
                const string folder = "Assets/ItemIcons";
                Directory.CreateDirectory(folder);
                settings = ScriptableObject.CreateInstance<ItemIconSettings>();
                AssetDatabase.CreateAsset(settings, folder + "/ItemIconSettings.asset");
                AssetDatabase.SaveAssets();
            }

            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        [MenuItem("Assets/Item Icons/Generate Icon For Selected Items")]
        static void GenerateSelected()
        {
            var items = new System.Collections.Generic.List<PickableItemData>();
            foreach (UnityEngine.Object selected in Selection.objects)
                if (selected is PickableItemData data) items.Add(data);
            Show(ItemIconGenerator.Generate(items));
        }

        [MenuItem("Assets/Item Icons/Generate Icon For Selected Items", true)]
        static bool CanGenerateSelected()
        {
            foreach (UnityEngine.Object selected in Selection.objects)
                if (selected is PickableItemData) return true;
            return false;
        }

        static void Show(ItemIconReport report)
        {
            string text = report.ToString();
            if (report.HasProblems) Debug.LogWarning(text);
            else Debug.Log(text);
        }
    }

    [CustomEditor(typeof(ItemIconSettings))]
    public sealed class ItemIconSettingsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            var settings = (ItemIconSettings)target;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate All")) Log(ItemIconGenerator.GenerateAll(settings));
                if (GUILayout.Button("Generate Missing")) Log(ItemIconGenerator.GenerateAll(settings, true));
                if (GUILayout.Button("Report")) Log(ItemIconGenerator.Plan(settings));
            }
        }

        static void Log(ItemIconReport report)
        {
            if (report.HasProblems) Debug.LogWarning(report.ToString());
            else Debug.Log(report.ToString());
        }
    }

    /// <summary>
    /// Entry points for the command line: <c>Unity -batchmode -projectPath P -executeMethod MHZE.ItemIcons.ItemIconBatch.GenerateAll</c>
    /// (no <c>-nographics</c>: the icons are drawn on the graphics card). The report goes to the log and to a file
    /// (<c>ITEM_ICONS_REPORT</c>, default <c>Logs/ItemIcons/report.txt</c>). The exit code is 0, or 1 when an item failed.
    /// </summary>
    public static class ItemIconBatch
    {
        public static void GenerateAll() => Run(() => ItemIconGenerator.GenerateAll());

        public static void GenerateMissing() => Run(() => ItemIconGenerator.GenerateAll(null, true));

        public static void Plan() => Run(() => ItemIconGenerator.Plan());

        static void Run(Func<ItemIconReport> action)
        {
            int code = 0;
            try
            {
                EnsureSavedSceneIsOpen();
                ItemIconReport report = action();
                string text = report.ToString();
                Debug.Log(text);
                string path = Environment.GetEnvironmentVariable("ITEM_ICONS_REPORT");
                if (string.IsNullOrEmpty(path)) path = "Logs/ItemIcons/report.txt";
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                File.WriteAllText(path, text);
                if (report.HasProblems) code = 1;
            }
            catch (Exception exception)
            {
                Debug.LogError("[ItemIcons] " + exception);
                code = 2;
            }

            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        // A batch editor that has no scene history starts on an untitled scene, and Unity cannot add a scene beside that. Open the
        // first scene of the build (or any scene of the project) instead; the tool never saves it.
        static void EnsureSavedSceneIsOpen()
        {
            if (!Application.isBatchMode || !string.IsNullOrEmpty(SceneManager.GetActiveScene().path)) return;

            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled) continue;
                EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
                return;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
            {
                EditorSceneManager.OpenScene(AssetDatabase.GUIDToAssetPath(guid), OpenSceneMode.Single);
                return;
            }
        }
    }
}
