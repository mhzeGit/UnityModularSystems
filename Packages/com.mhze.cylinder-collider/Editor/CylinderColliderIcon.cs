using UnityEditor;
using UnityEngine;

namespace MHZE.CylinderCollider.Editor
{
    [InitializeOnLoad]
    internal static class CylinderColliderIcon
    {
        static CylinderColliderIcon()
        {
            ClearIcon();
        }

        private static void ClearIcon()
        {
            var guids = AssetDatabase.FindAssets("t:MonoScript CylinderCollider");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.GetClass() == typeof(CylinderCollider))
                {
                    var importer = AssetImporter.GetAtPath(path) as MonoImporter;
                    if (importer != null && importer.GetIcon() != null)
                    {
                        importer.SetIcon(null);
                        importer.SaveAndReimport();
                    }
                    return;
                }
            }
        }
    }
}
