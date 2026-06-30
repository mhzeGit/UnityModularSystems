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
            foreach (var iconGuid in iconGuids)
            {
                var iconPath = AssetDatabase.GUIDToAssetPath(iconGuid);
                var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
                if (icon == null) continue;

                var scriptGuids = AssetDatabase.FindAssets("t:MonoScript GearConstraint");
                foreach (var scriptGuid in scriptGuids)
                {
                    var scriptPath = AssetDatabase.GUIDToAssetPath(scriptGuid);
                    var script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
                    if (script == null) continue;
                    var scriptType = script.GetClass();
                    if (scriptType != null && typeof(GearConstraint).IsAssignableFrom(scriptType))
                    {
                        var importer = AssetImporter.GetAtPath(scriptPath) as MonoImporter;
                        if (importer != null && importer.GetIcon() != icon)
                        {
                            importer.SetIcon(icon);
                            importer.SaveAndReimport();
                            return;
                        }
                    }
                }
            }
        }
    }
}
