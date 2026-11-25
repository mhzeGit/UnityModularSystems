// The answer buttons of a question asked with DialogManager.Ask: a stack of rounded buttons above the speech bubble, numbered 1, 2, 3... Picked with the mouse, the number keys, Up/Down (or W/S, a gamepad's d-pad or stick) and Enter/Space (or the gamepad's south button). A disabled answer is greyed out and struck through; trying to pick it shakes it. Built by the DialogManager at runtime when none is assigned.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MHZE.DialogSystem
{
[DisallowMultipleComponent]
public class DialogChoiceView : MonoBehaviour
{
    [Header("Look")]
    [Tooltip("Font of the answers. Empty = the dialog line's font (or Unity's built-in font).")]
    [SerializeField] private Font font;
    [Min(8)] [SerializeField] private int fontSize = 26;
    [Tooltip("Height of one answer button, in reference pixels.")]
    [Min(16f)] [SerializeField] private float rowHeight = 54f;
    [Min(0f)] [SerializeField] private float rowSpacing = 10f;
    [Tooltip("Smallest button width; every button is as wide as the longest answer.")]
    [Min(40f)] [SerializeField] private float minRowWidth = 260f;
    [Tooltip("Space left and right of the longest answer.")]
    [Min(0f)] [SerializeField] private float rowPadding = 34f;
    [Tooltip("Gap between the speech bubble and the lowest button.")]
    [SerializeField] private float gapAboveBubble = 16f;
    [Min(1)] [SerializeField] private int cornerRadius = 18;
    [SerializeField] private Color rowColor = new Color(0f, 0f, 0f, 0.78f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color numberColor = new Color(1f, 0.92f, 0.6f, 1f);
    [Tooltip("The answer that Enter would pick (also the one under the mouse).")]
    [SerializeField] private Color highlightColor = new Color(1f, 0.86f, 0.45f, 0.96f);
    [SerializeField] private Color highlightTextColor = new Color(0.13f, 0.08f, 0.04f, 1f);
    [SerializeField] private Color disabledColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);
    [SerializeField] private Color disabledTextColor = new Color(0.62f, 0.62f, 0.62f, 0.7f);

    [Header("Input")]
    [Tooltip("Seconds after the answers appear before input counts, so a key held to skip the question cannot pick an answer.")]
    [Min(0f)] [SerializeField] private float inputDelay = 0.3f;
    [Tooltip("Free and show the mouse cursor while the answers are up (and put it back after).")]
    [SerializeField] private bool unlockCursor = true;

    private sealed class Row
    {
        public RectTransform rect;
        public Image background;
        public Text label;
        public RectTransform strike;
        public Image strikeImage;
        public string text;
        public bool enabled;
        public float scale = 1f;
        public float shakeStart = -10f;
        public Vector2 home;
    }

    private readonly List<Row> rows = new List<Row>();
    private RectTransform container;
    private Action<int> onPick;
    private Action<int> onMove;
    private Action<int> onDenied;
    private int highlight = -1;
    private float acceptAt;
    private bool shown;
    private bool cursorSaved;
    private CursorLockMode savedLock;
    private bool savedVisible;
    private Vector2 lastMouse = new Vector2(float.NaN, float.NaN);
    private float stickRepeatAt;
    private const float ShakeSeconds = 0.35f;

    public bool IsShown => shown;

    /// <summary>The answer Enter would pick now (-1 when none can be picked).</summary>
    public int Highlighted => highlight;

    /// <summary>Uses the dialog line's font and size for the answers (called by the DialogManager when it builds the view).</summary>
    public void MatchText(Text dialogText)
    {
        if (dialogText == null)
        {
            return;
        }

        if (font == null)
        {
            font = dialogText.font;
        }

        fontSize = dialogText.fontSize;
    }

    /// <summary>
    /// Shows the answers. <paramref name="pick"/> gets the index of an enabled answer the player picked; <paramref name="moved"/>
    /// the newly highlighted answer; <paramref name="denied"/> a disabled answer the player tried to pick.
    /// </summary>
    public void Show(IList<DialogChoice> choices, Action<int> pick, Action<int> moved = null, Action<int> denied = null)
    {
        EnsureContainer();
        onPick = pick;
        onMove = moved;
        onDenied = denied;
        int count = choices != null ? choices.Count : 0;
        while (rows.Count < count)
        {
            rows.Add(BuildRow(rows.Count));
        }

        float width = minRowWidth;
        for (int i = 0; i < rows.Count; i++)
        {
            Row row = rows[i];
            bool used = i < count;
            row.rect.gameObject.SetActive(used);
            if (!used)
            {
                continue;
            }

            row.text = choices[i].Label ?? string.Empty;
            row.enabled = choices[i].Enabled;
            row.scale = 1f;
            row.shakeStart = -10f;
            row.label.font = font != null ? font : row.label.font;
            row.label.fontSize = fontSize;
            width = Mathf.Max(width, MeasureWidth(row.label, (i + 1) + "    " + row.text) + rowPadding * 2f);
        }

        for (int i = 0; i < count; i++)
        {
            Row row = rows[i];
            row.home = new Vector2(0f, (count - 1 - i) * (rowHeight + rowSpacing));
            row.rect.sizeDelta = new Vector2(width, rowHeight);
            row.rect.anchoredPosition = row.home;
            row.rect.localScale = Vector3.one;
            float textWidth = MeasureWidth(row.label, (i + 1) + "    " + row.text);
            row.strike.sizeDelta = new Vector2(textWidth + 14f, Mathf.Max(2f, fontSize * 0.1f));
            row.strike.gameObject.SetActive(!row.enabled);
        }

        container.sizeDelta = new Vector2(width, count * rowHeight + Mathf.Max(0, count - 1) * rowSpacing);
        container.gameObject.SetActive(count > 0);
        highlight = FirstEnabled(count);
        acceptAt = Time.unscaledTime + inputDelay;
        lastMouse = new Vector2(float.NaN, float.NaN);
        shown = count > 0;
        if (shown && unlockCursor)
        {
            if (!cursorSaved)
            {
                savedLock = Cursor.lockState;
                savedVisible = Cursor.visible;
                cursorSaved = true;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        Refresh(true);
    }

    public void Hide()
    {
        shown = false;
        onPick = null;
        onMove = null;
        onDenied = null;
        highlight = -1;
        if (container != null)
        {
            container.gameObject.SetActive(false);
        }

        RestoreCursor();
    }

    private void OnDisable()
    {
        if (shown)
        {
            Hide();
        }
    }

    /// <summary>Picks an answer as if clicked: false (and a shake) when it is disabled.</summary>
    public bool TryPick(int index)
    {
        if (!shown || index < 0 || index >= rows.Count || !rows[index].rect.gameObject.activeSelf)
        {
            return false;
        }

        if (!rows[index].enabled)
        {
            rows[index].shakeStart = Time.unscaledTime;
            onDenied?.Invoke(index);
            return false;
        }

        Action<int> callback = onPick;
        callback?.Invoke(index);
        return true;
    }

    private void Update()
    {
        if (!shown)
        {
            return;
        }

        if (unlockCursor && (Cursor.lockState != CursorLockMode.None || !Cursor.visible))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        Refresh(false);
        if (Time.unscaledTime < acceptAt)
        {
            return;
        }

        ReadInput();
    }

    private void ReadInput()
    {
        int count = ActiveCount();
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            for (int i = 0; i < count && i < 9; i++)
            {
                if (keyboard[Key.Digit1 + i].wasPressedThisFrame || keyboard[Key.Numpad1 + i].wasPressedThisFrame)
                {
                    Highlight(i);
                    TryPick(i);
                    return;
                }
            }

            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
            {
                Step(-1);
            }

            if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
            {
                Step(1);
            }

            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
            {
                TryPick(highlight);
                return;
            }
        }

        Gamepad pad = Gamepad.current;
        if (pad != null)
        {
            if (pad.dpad.up.wasPressedThisFrame)
            {
                Step(-1);
            }

            if (pad.dpad.down.wasPressedThisFrame)
            {
                Step(1);
            }

            float y = pad.leftStick.ReadValue().y;
            if (Mathf.Abs(y) > 0.6f)
            {
                if (Time.unscaledTime >= stickRepeatAt)
                {
                    Step(y > 0f ? -1 : 1);
                    stickRepeatAt = Time.unscaledTime + 0.3f;
                }
            }
            else
            {
                stickRepeatAt = 0f;
            }

            if (pad.buttonSouth.wasPressedThisFrame)
            {
                TryPick(highlight);
                return;
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 position = mouse.position.ReadValue();
            int under = RowUnder(position);
            bool moved = float.IsNaN(lastMouse.x) || (position - lastMouse).sqrMagnitude > 1f;
            lastMouse = position;
            if (under >= 0 && moved && rows[under].enabled)
            {
                Highlight(under);
            }

            if (under >= 0 && mouse.leftButton.wasPressedThisFrame)
            {
                TryPick(under);
            }
        }
    }

    private void Step(int direction)
    {
        int count = ActiveCount();
        if (count == 0)
        {
            return;
        }

        int start = highlight < 0 ? (direction > 0 ? -1 : count) : highlight;
        for (int k = 1; k <= count; k++)
        {
            int i = start + direction * k;
            if (i < 0 || i >= count)
            {
                break;
            }

            if (rows[i].enabled)
            {
                Highlight(i);
                return;
            }
        }
    }

    private void Highlight(int index)
    {
        if (index == highlight || index < 0 || index >= rows.Count || !rows[index].enabled)
        {
            return;
        }

        highlight = index;
        onMove?.Invoke(index);
    }

    private int RowUnder(Vector2 screenPoint)
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        int count = ActiveCount();
        for (int i = 0; i < count; i++)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(rows[i].rect, screenPoint, eventCamera))
            {
                return i;
            }
        }

        return -1;
    }

    private int ActiveCount()
    {
        int n = 0;
        while (n < rows.Count && rows[n].rect.gameObject.activeSelf)
        {
            n++;
        }

        return n;
    }

    private int FirstEnabled(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (rows[i].enabled)
            {
                return i;
            }
        }

        return -1;
    }

    private void Refresh(bool snap)
    {
        float blend = snap ? 1f : 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);
        int count = ActiveCount();
        for (int i = 0; i < count; i++)
        {
            Row row = rows[i];
            bool lit = i == highlight && row.enabled;
            Color background = !row.enabled ? disabledColor : lit ? highlightColor : rowColor;
            Color text = !row.enabled ? disabledTextColor : lit ? highlightTextColor : textColor;
            row.background.color = snap ? background : Color.Lerp(row.background.color, background, blend);
            row.label.color = text;
            row.strikeImage.color = disabledTextColor;
            string number = ColorUtility.ToHtmlStringRGBA(!row.enabled ? disabledTextColor : lit ? highlightTextColor : numberColor);
            row.label.text = "<b><color=#" + number + ">" + (i + 1) + "</color></b>    " + row.text;

            float targetScale = lit ? 1.045f : 1f;
            row.scale = snap ? targetScale : Mathf.Lerp(row.scale, targetScale, blend);
            row.rect.localScale = new Vector3(row.scale, row.scale, 1f);

            float shake = Time.unscaledTime - row.shakeStart;
            float offset = shake < ShakeSeconds ? Mathf.Sin(shake * 62f) * 9f * (1f - shake / ShakeSeconds) : 0f;
            row.rect.anchoredPosition = row.home + new Vector2(offset, 0f);
        }
    }

    private void EnsureContainer()
    {
        if (container != null)
        {
            return;
        }

        container = transform as RectTransform;
        if (container == null)
        {
            container = gameObject.AddComponent<RectTransform>();
        }

        container.anchorMin = new Vector2(0.5f, 1f);
        container.anchorMax = new Vector2(0.5f, 1f);
        container.pivot = new Vector2(0.5f, 0f);
        container.anchoredPosition = new Vector2(0f, gapAboveBubble);
    }

    private Row BuildRow(int index)
    {
        var row = new Row();
        var go = new GameObject("Answer" + (index + 1), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        row.rect = (RectTransform)go.transform;
        row.rect.SetParent(container, false);
        row.rect.anchorMin = new Vector2(0.5f, 0f);
        row.rect.anchorMax = new Vector2(0.5f, 0f);
        row.rect.pivot = new Vector2(0.5f, 0f);
        row.background = go.GetComponent<Image>();
        row.background.sprite = ProceduralRoundedSprite.Get(cornerRadius);
        row.background.type = Image.Type.Sliced;
        row.background.raycastTarget = false;

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        var labelRect = (RectTransform)labelGo.transform;
        labelRect.SetParent(row.rect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12f, 0f);
        labelRect.offsetMax = new Vector2(-12f, 0f);
        row.label = labelGo.GetComponent<Text>();
        row.label.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        row.label.fontSize = fontSize;
        row.label.alignment = TextAnchor.MiddleCenter;
        row.label.horizontalOverflow = HorizontalWrapMode.Overflow;
        row.label.verticalOverflow = VerticalWrapMode.Overflow;
        row.label.supportRichText = true;
        row.label.raycastTarget = false;

        var strikeGo = new GameObject("Strike", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        row.strike = (RectTransform)strikeGo.transform;
        row.strike.SetParent(row.rect, false);
        row.strike.anchorMin = row.strike.anchorMax = new Vector2(0.5f, 0.5f);
        row.strike.pivot = new Vector2(0.5f, 0.5f);
        row.strike.anchoredPosition = Vector2.zero;
        row.strikeImage = strikeGo.GetComponent<Image>();
        row.strikeImage.raycastTarget = false;
        strikeGo.SetActive(false);
        return row;
    }

    private void RestoreCursor()
    {
        if (!cursorSaved)
        {
            return;
        }

        cursorSaved = false;
        Cursor.lockState = savedLock;
        Cursor.visible = savedVisible;
    }

    private static float MeasureWidth(Text text, string content)
    {
        TextGenerationSettings settings = text.GetGenerationSettings(Vector2.zero);
        settings.horizontalOverflow = HorizontalWrapMode.Overflow;
        settings.verticalOverflow = VerticalWrapMode.Overflow;
        settings.richText = true;
        float pixelsPerUnit = text.pixelsPerUnit == 0f ? 1f : text.pixelsPerUnit;
        return text.cachedTextGeneratorForLayout.GetPreferredWidth(content, settings) / pixelsPerUnit;
    }
}
}
