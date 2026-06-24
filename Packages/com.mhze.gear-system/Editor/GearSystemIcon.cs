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
            var guids = AssetDatabase.FindAssets("t:Texture2D d_GearSystemIcon");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (icon != null)
                {
                    var scriptGuids = AssetDatabase.FindAssets("t:MonoScript GearSystem");
                    foreach (var scriptGuid in scriptGuids)
                    {
                        var scriptPath = AssetDatabase.GUIDToAssetPath(scriptGuid);
                        var script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
                        var importer = AssetImporter.GetAtPath(scriptPath) as MonoImporter;
                        if (importer != null)
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
