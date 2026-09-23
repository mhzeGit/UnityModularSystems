// Editor wizard that builds the Canvas dialog UI and a DialogManager in the open scene, wiring every dialog view reference. Run it from Tools > Dialog System > Setup Dialog Canvas. Uses legacy uGUI Text with the built-in font so it works without importing TextMeshPro essentials.

using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MHZE.DialogSystem.Editor
{
public static class DialogSetupWizard
{
    private const string CanvasName = "DialogCanvas";

    private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    [MenuItem("Tools/Dialog System/Setup Dialog Canvas", false, 100)]
    public static void SetupDialogCanvas()
    {
        Undo.SetCurrentGroupName("Setup Dialog System");
        int group = Undo.GetCurrentGroup();

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        DialogView view = FindOrCreateView(font);
        DialogManager manager = FindOrCreateManager(view);

        Undo.CollapseUndoOperations(group);

        Selection.activeGameObject = manager.gameObject;
        EditorGUIUtility.PingObject(view.gameObject);

        Debug.Log("[Dialog System] Setup complete. Add a DialogProximityTrigger to an NPC and assign a DialogSequence.");
    }

    private static DialogView FindOrCreateView(Font font)
    {
        DialogView existing = Object.FindAnyObjectByType<DialogView>();
        if (existing != null)
        {
            EnsureViewHierarchy(existing, font);
            return existing;
        }

        GameObject canvasObject = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasObject, "Create Dialog Canvas");

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        DialogView view = Undo.AddComponent<DialogView>(canvasObject);
        EnsureViewHierarchy(view, font);
        return view;
    }

    private static void EnsureViewHierarchy(DialogView view, Font font)
    {
        Transform root = view.transform;

        RectTransform panel = root.Find("DialogPanel") as RectTransform;
        if (panel == null)
        {
            panel = CreateUIObject("DialogPanel", root);
            AnchorToBottom(panel, 320f, 120f, 40f);
            Image background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.78f);
            background.raycastTarget = false;
        }

        Transform legacySpeaker = panel.Find("SpeakerText");
        if (legacySpeaker != null)
        {
            Undo.DestroyObjectImmediate(legacySpeaker.gameObject);
        }

        if (panel.GetComponent<RoundedCorners>() == null)
        {
            Undo.AddComponent<RoundedCorners>(panel.gameObject);
        }

        Text body = EnsureText(panel, "DialogText", font, 26, TextAnchor.MiddleCenter, FontStyle.Normal, Color.white);
        RectTransform bodyRect = (RectTransform)body.transform;
        bodyRect.anchorMin = Vector2.zero;
        bodyRect.anchorMax = Vector2.one;
        bodyRect.pivot = new Vector2(0.5f, 0.5f);
        bodyRect.offsetMin = new Vector2(28f, 20f);
        bodyRect.offsetMax = new Vector2(-28f, -20f);

        Transform legacyIndicator = panel.Find("ContinueIndicator");
        if (legacyIndicator != null)
        {
            Undo.DestroyObjectImmediate(legacyIndicator.gameObject);
        }

        Text indicator = EnsureText(root, "ContinueHint", font, 22, TextAnchor.LowerRight, FontStyle.Normal, new Color(1f, 1f, 1f, 0.85f));
        indicator.text = "Click LMB to continue";
        RectTransform indicatorRect = (RectTransform)indicator.transform;
        indicatorRect.anchorMin = new Vector2(1f, 0f);
        indicatorRect.anchorMax = new Vector2(1f, 0f);
        indicatorRect.pivot = new Vector2(1f, 0f);
        indicatorRect.anchoredPosition = new Vector2(-32f, 28f);
        indicatorRect.sizeDelta = new Vector2(360f, 32f);

        SerializedObject serialized = new SerializedObject(view);
        serialized.FindProperty("panelRoot").objectReferenceValue = panel.gameObject;
        serialized.FindProperty("speakerText").objectReferenceValue = null;
        serialized.FindProperty("dialogText").objectReferenceValue = body;
        serialized.FindProperty("continueIndicator").objectReferenceValue = indicator.gameObject;
        serialized.FindProperty("combineSpeakerIntoText").boolValue = true;
        serialized.FindProperty("dynamicSize").boolValue = true;
        serialized.FindProperty("centerText").boolValue = true;
        serialized.FindProperty("smoothResize").boolValue = true;
        serialized.FindProperty("resizeSpeed").floatValue = 16f;
        serialized.FindProperty("textColor").colorValue = Color.white;
        serialized.FindProperty("backgroundColor").colorValue = new Color(0f, 0f, 0f, 0.78f);
        serialized.ApplyModifiedProperties();
    }

    private static DialogManager FindOrCreateManager(DialogView view)
    {
        DialogManager manager = Object.FindAnyObjectByType<DialogManager>();
        if (manager == null)
        {
            GameObject managerObject = new GameObject("DialogManager");
            Undo.RegisterCreatedObjectUndo(managerObject, "Create Dialog Manager");
            manager = Undo.AddComponent<DialogManager>(managerObject);
        }

        SerializedObject serialized = new SerializedObject(manager);
        serialized.FindProperty("view").objectReferenceValue = view;
        serialized.ApplyModifiedProperties();
        return manager;
    }

    private static RectTransform CreateUIObject(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(gameObject, "Create " + name);

        RectTransform rect = (RectTransform)gameObject.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    private static Text EnsureText(Transform parent, string name, Font font, int size, TextAnchor anchor, FontStyle style, Color color)
    {
        Transform existing = parent.Find(name);
        Text text;
        if (existing != null)
        {
            text = existing.GetComponent<Text>();
            if (text == null)
            {
                text = Undo.AddComponent<Text>(existing.gameObject);
            }
        }
        else
        {
            RectTransform rect = CreateUIObject(name, parent);
            text = Undo.AddComponent<Text>(rect.gameObject);
        }

        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = anchor;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static void AnchorToBottom(RectTransform rect, float width, float height, float offsetY)
    {
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, offsetY);
        rect.sizeDelta = new Vector2(width, height);
    }
}
}
