using UnityEditor;
using UnityEngine;

namespace MHZE.CylinderCollider.Editor
{
    [InitializeOnLoad]
    internal static class CylinderColliderIcon
    {
        private static Texture2D s_Icon;
        private static bool s_IconLoading;

        static CylinderColliderIcon()
        {
            s_Icon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Packages/com.mhze.cylinder-collider/d_CylinderColliderIcon.png");
            if (s_Icon != null)
            {
                SetIcon();
            }
            else
            {
                s_IconLoading = true;
                EditorApplication.delayCall += LoadAndSetIcon;
            }
        }

        private static void LoadAndSetIcon()
        {
            s_Icon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Packages/com.mhze.cylinder-collider/d_CylinderColliderIcon.png");
            if (s_Icon != null)
            {
                s_IconLoading = false;
                SetIcon();
            }
        }

        private static void SetIcon()
        {
            var guids = AssetDatabase.FindAssets("t:MonoScript CylinderCollider");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.GetClass() == typeof(CylinderCollider))
                {
                    var importer = AssetImporter.GetAtPath(path) as MonoImporter;
                    if (importer != null)
                    {
                        var current = importer.GetIcon();
                        if (current != s_Icon)
                        {
                            importer.SetIcon(s_Icon);
                            importer.SaveAndReimport();
                        }
                    }
                    return;
                }
            }
        }
    }
}
