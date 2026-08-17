// Manual cleanup helpers for global variables. The system never deletes anything on its own — use these to clean up when asked.

using UnityEditor;

namespace MHZE.GlobalVariables.Editor
{
    public static class GlobalVariableMenu
    {
        [MenuItem("Tools/MHZE/Global Variables/Delete All")]
        public static void DeleteAllGlobalVariables()
        {
            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(GlobalVariable)}"))
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
