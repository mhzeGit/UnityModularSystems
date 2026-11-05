using UnityEditor;
using UnityEngine;

namespace MHZE.WallBreak.Editor
{
    /// <summary>Inspector for BreakableWall: warns when the wall's mesh cannot be read at runtime and fixes it on request.</summary>
    [CustomEditor(typeof(BreakableWall))]
    public sealed class BreakableWallInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var wall = (BreakableWall)target;
            var filter = wall.GetComponentInChildren<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
            {
                EditorGUILayout.HelpBox("No wall mesh found. Put this on an object with a MeshFilter and MeshRenderer (or a child that has them).", MessageType.Error);
            }
            else if (!filter.sharedMesh.isReadable)
            {
                EditorGUILayout.HelpBox("The wall's mesh '" + filter.sharedMesh.name + "' is not readable, so it cannot be sliced when it breaks.", MessageType.Error);
                string path = AssetDatabase.GetAssetPath(filter.sharedMesh);
                var importer = string.IsNullOrEmpty(path) ? null : AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer != null && GUILayout.Button("Enable Read/Write on " + System.IO.Path.GetFileName(path)))
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                }
            }
            if (wall.GetComponentInChildren<Collider>() == null)
                EditorGUILayout.HelpBox("The wall has no collider. Add one (a MeshCollider or BoxCollider) so a tool can hit it.", MessageType.Warning);
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("The orange box in the Scene view is the hole. Move the Hole Center marker to place it.", MessageType.None);
        }
    }
}
