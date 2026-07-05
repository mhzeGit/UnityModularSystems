using UnityEditor;
using UnityEngine;

namespace MHZE.GearSystem.Editor
{
    [InitializeOnLoad]
    internal static class GearSystemIcon
    {
        static GearSystemIcon()
        {
            ClearIcon();
        }

        private static void ClearIcon()
        {
            var iconGuids = AssetDatabase.FindAssets("d_GearIcon t:Texture2D");
            if (iconGuids.Length == 0) return;

            var iconPath = AssetDatabase.GUIDToAssetPath(iconGuids[0]);
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
            if (icon == null) return;

            SetIconForType<GearConstraint>("GearConstraint", icon);
            SetIconForType<GearMeshGenerator>("GearMeshGenerator", icon);
        }

        private static void SetIconForType<T>(string searchFilter, Texture2D icon)
        {
            var scriptGuids = AssetDatabase.FindAssets($"t:MonoScript {searchFilter}");
            foreach (var scriptGuid in scriptGuids)
            {
                var scriptPath = AssetDatabase.GUIDToAssetPath(scriptGuid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
                if (script == null) continue;
                var scriptType = script.GetClass();
                if (scriptType != null && typeof(T).IsAssignableFrom(scriptType))
                {
                    var importer = AssetImporter.GetAtPath(scriptPath) as MonoImporter;
                    if (importer != null && importer.GetIcon() != icon)
                    {
                        importer.SetIcon(icon);
                        importer.SaveAndReimport();
                    }
                }
            }
        }
    }
}
