// Inspector and menu tools for RoundedCorners: regenerate the procedural sprite, and add the component to any selected UI Image.

using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MHZE.DialogSystem.Editor
{
[CustomEditor(typeof(RoundedCorners))]
[CanEditMultipleObjects]
public class RoundedCornersEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        if (GUILayout.Button("Regenerate Rounded Sprite"))
        {
            foreach (Object item in targets)
            {
                ((RoundedCorners)item).Apply();
            }
        }

        EditorGUILayout.HelpBox(
            "The rounded shape is generated in code and applied as a 9-sliced sprite, so it stretches " +
            "to any size. Change Radius to preview it live in the editor; 0 restores the original sprite.",
            MessageType.Info);

        if (targets.Length == 1)
        {
            RoundedCorners corners = (RoundedCorners)target;
            if (corners.GetComponent<Image>() == null)
            {
                EditorGUILayout.HelpBox(
                    "No UI Image on this GameObject. Add an Image component (or attach RoundedCorners to " +
                    "the GameObject that has one).",
                    MessageType.Warning);
            }
        }
    }

    [MenuItem("Tools/Dialog System/Add Rounded Corners To Selected UI Images")]
    private static void AddToSelection()
    {
        foreach (GameObject gameObject in Selection.gameObjects)
        {
            foreach (Image image in gameObject.GetComponents<Image>())
            {
                if (image.GetComponent<RoundedCorners>() == null)
                {
                    Undo.AddComponent<RoundedCorners>(image.gameObject);
                }
            }
        }
    }

    [MenuItem("Tools/Dialog System/Add Rounded Corners To Selected UI Images", true)]
    private static bool AddToSelectionValidate()
    {
        foreach (GameObject gameObject in Selection.gameObjects)
        {
            if (gameObject.GetComponent<Image>() != null)
            {
                return true;
            }
        }

        return false;
    }

    [MenuItem("GameObject/UI/Rounded Corners (Procedural)", false, 20)]
    private static void CreateRoundedPanel(MenuCommand command)
    {
        GameObject parent = command.context as GameObject;
        if (parent == null)
        {
            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            parent = canvas != null ? canvas.gameObject : null;
        }

        GameObject gameObject = new GameObject("RoundedPanel", typeof(RectTransform), typeof(Image), typeof(RoundedCorners));
        gameObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);

        GameObjectUtility.SetParentAndAlign(gameObject, parent);
        Undo.RegisterCreatedObjectUndo(gameObject, "Create Rounded Panel");
        Selection.activeGameObject = gameObject;
    }
}
}
