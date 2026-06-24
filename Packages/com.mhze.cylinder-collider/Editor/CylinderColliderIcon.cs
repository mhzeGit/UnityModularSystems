using UnityEditor;
using UnityEngine;

namespace MHZE.CylinderCollider.Editor
{
    [InitializeOnLoad]
    internal static class CylinderColliderIcon
    {
        static CylinderColliderIcon()
        {
            // Don't use delayCall here — domain reload is fine
            // if the icon hasn't been set yet (single reimport only).
            AssignIcon();
        }

        private static void AssignIcon()
        {
            var content = EditorGUIUtility.IconContent("d_PreMatCylinder");
            if (content?.image == null)
                return;

            var tex = content.image as Texture2D;
            if (tex == null)
                return;

            var guids = AssetDatabase.FindAssets("t:MonoScript CylinderCollider");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.GetClass() == typeof(CylinderCollider))
                {
                    var importer = AssetImporter.GetAtPath(path) as MonoImporter;
                    if (importer != null && importer.GetIcon() == null)
                    {
                        importer.SetIcon(tex);
                        importer.SaveAndReimport();
                    }
                    return;
                }
            }
        }
    }
}
