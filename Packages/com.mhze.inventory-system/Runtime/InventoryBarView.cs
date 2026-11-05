using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MHZE.InventorySystem
{
    /// <summary>Which corner or edge of the screen the bar sits at.</summary>
    public enum BarAnchor
    {
        BottomCenter,
        BottomLeft,
        BottomRight,
        TopCenter,
        TopLeft,
        TopRight
    }

    /// <summary>
    /// The inventory's bar: a row of slots at the bottom of the screen, built in code on its own screen-space
    /// canvas. Each slot shows its number key, the item's icon (its name until it has one), the stack size when
    /// it is more than one, and the selected slot stands out. It reads everything from
    /// <see cref="PlayerInventory"/> and changes nothing.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("MHZE/Inventory/Inventory Bar View")]
    public sealed class InventoryBarView : MonoBehaviour
    {
        [Tooltip("Found on this object or above it when empty.")]
        [SerializeField] PlayerInventory inventory;

        [Header("Layout (pixels at a 1920 x 1080 canvas)")]
        [SerializeField] BarAnchor anchor = BarAnchor.BottomCenter;
        [Tooltip("Distance from the screen edges: x from the left or right edge, y from the top or bottom edge.")]
        [SerializeField] Vector2 margin = new Vector2(28f, 28f);
        [SerializeField, Min(24f)] float slotSize = 76f;
        [SerializeField, Min(0f)] float slotGap = 10f;
        [SerializeField, Min(1f)] float selectedScale = 1.12f;
        [Tooltip("Free space between the slot's edge and the item's icon, as a fraction of the slot.")]
        [SerializeField, Range(0f, 0.3f)] float iconInset = 0.08f;
        [Tooltip("Higher draws on top. Keep it below pause menus and fades.")]
        [SerializeField] int sortingOrder = 30;

        [Header("Look")]
        [Tooltip("Uses the TextMesh Pro default font when empty.")]
        [SerializeField] TMP_FontAsset font;
        [SerializeField] Color slotColor = new Color(0.06f, 0.055f, 0.05f, 0.58f);
        [SerializeField] Color selectedSlotColor = new Color(0.11f, 0.095f, 0.08f, 0.88f);
        [SerializeField] Color borderColor = new Color(1f, 1f, 1f, 0.2f);
        [SerializeField] Color selectedBorderColor = new Color(1f, 0.9f, 0.62f, 1f);
        [SerializeField] Color textColor = new Color(0.97f, 0.94f, 0.86f, 1f);

        [Header("Messages")]
        [SerializeField] string fullMessage = "Inventory full";

        const float MaxBarWidth = 1800f;
        const float NoticeSeconds = 1.6f;

        sealed class SlotView
        {
            public RectTransform Root;
            public Image Border;
            public Image Fill;
            public Image Icon;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Key;
            public TextMeshProUGUI Count;
            public float Scale = 1f;
            public float Punch;
            public int ShownCount = -1;
        }

        readonly List<SlotView> views = new List<SlotView>();
        GameObject canvasObject;
        RectTransform bar;
        TextMeshProUGUI notice;
        float noticeUntil;
        float slotPixels;
        PlayerInventory bound;

        void Awake()
        {
            if (inventory == null) inventory = GetComponentInParent<PlayerInventory>();
        }

        void OnValidate()
        {
            // Changing the layout in the Inspector while playing shows at once.
            if (Application.isPlaying && bound != null && bar != null) Rebuild();
        }

        void OnEnable()
        {
            if (inventory == null) inventory = GetComponentInParent<PlayerInventory>();
            if (inventory == null) inventory = PlayerInventory.Instance;
            if (inventory == null) return;

            Bind(inventory);
            BuildCanvas();
            canvasObject.SetActive(true);
            Rebuild();
        }

        void OnDisable()
        {
            Unbind();
            if (canvasObject != null) canvasObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (canvasObject != null) Destroy(canvasObject);
        }

        void Bind(PlayerInventory source)
        {
            if (bound == source) return;

            Unbind();
            bound = source;
            bound.SlotChanged += HandleSlotChanged;
            bound.SlotCountChanged += Rebuild;
            bound.SelectionChanged += HandleSelectionChanged;
            bound.PickupRefused += HandleRefused;
            bound.SelectionBlocked += HandleBlocked;
        }

        void Unbind()
        {
            if (bound == null) return;

            bound.SlotChanged -= HandleSlotChanged;
            bound.SlotCountChanged -= Rebuild;
            bound.SelectionChanged -= HandleSelectionChanged;
            bound.PickupRefused -= HandleRefused;
            bound.SelectionBlocked -= HandleBlocked;
            bound = null;
        }

        // The hand holds something that has to stay: the selected slot gives a small start instead of moving on.
        void HandleBlocked()
        {
            int index = bound.SelectedIndex;
            if (index >= 0 && index < views.Count) views[index].Punch = 1f;
        }

        void HandleSlotChanged(int index)
        {
            if (index >= 0 && index < views.Count) Refresh(index);
        }

        void HandleSelectionChanged(int index)
        {
            ApplySelection();
        }

        void HandleRefused(GameObject item)
        {
            if (notice == null) return;

            notice.text = fullMessage;
            noticeUntil = Time.unscaledTime + NoticeSeconds;
        }

        void Update()
        {
            if (bound == null) return;

            float dt = Time.unscaledDeltaTime;
            float follow = 1f - Mathf.Exp(-20f * dt);
            for (int i = 0; i < views.Count; i++)
            {
                SlotView view = views[i];
                float target = i == bound.SelectedIndex ? selectedScale : 1f;
                view.Scale = Mathf.Lerp(view.Scale, target, follow);
                view.Punch = Mathf.MoveTowards(view.Punch, 0f, dt * 5f);
                float pop = view.Punch * view.Punch * 0.08f;
                view.Root.localScale = Vector3.one * (view.Scale + pop);
            }

            if (notice != null)
            {
                float left = noticeUntil - Time.unscaledTime;
                Color c = notice.color;
                c.a = Mathf.Clamp01(left / 0.4f);
                notice.color = c;
            }
        }

        // ───────────────────────────── building ─────────────────────────────

        void BuildCanvas()
        {
            if (canvasObject != null) return;

            canvasObject = new GameObject("Inventory Bar", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) canvasObject.layer = uiLayer;

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            bar = NewRect("Bar", canvasObject.transform);

            notice = NewText("Notice", bar, 26f, FontStyles.Bold, TextAlignmentOptions.Bottom);
            notice.rectTransform.sizeDelta = new Vector2(600f, 40f);
            notice.color = new Color(textColor.r, textColor.g, textColor.b, 0f);
        }

        // The bar sits at its corner or edge of the screen; the notice is on the side of the bar that faces the screen.
        void PlaceBar(float width)
        {
            bool left = anchor == BarAnchor.BottomLeft || anchor == BarAnchor.TopLeft;
            bool right = anchor == BarAnchor.BottomRight || anchor == BarAnchor.TopRight;
            bool top = anchor == BarAnchor.TopCenter || anchor == BarAnchor.TopLeft || anchor == BarAnchor.TopRight;

            var corner = new Vector2(left ? 0f : right ? 1f : 0.5f, top ? 1f : 0f);
            bar.anchorMin = bar.anchorMax = corner;
            bar.pivot = corner;
            bar.anchoredPosition = new Vector2(left ? margin.x : right ? -margin.x : 0f, top ? -margin.y : margin.y);
            bar.sizeDelta = new Vector2(width, slotPixels);

            RectTransform note = notice.rectTransform;
            var noteSide = new Vector2(corner.x, top ? 0f : 1f);
            note.anchorMin = note.anchorMax = noteSide;
            note.pivot = new Vector2(corner.x, top ? 1f : 0f);
            note.anchoredPosition = new Vector2(0f, top ? -18f : 18f);
            notice.alignment = left
                ? (top ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.BottomLeft)
                : right
                    ? (top ? TextAlignmentOptions.TopRight : TextAlignmentOptions.BottomRight)
                    : (top ? TextAlignmentOptions.Top : TextAlignmentOptions.Bottom);
        }

        void Rebuild()
        {
            if (bound == null || canvasObject == null) return;

            foreach (SlotView old in views)
            {
                if (old.Root != null) Destroy(old.Root.gameObject);
            }

            views.Clear();

            int count = bound.SlotCount;
            slotPixels = Mathf.Min(slotSize, (MaxBarWidth - (count - 1) * slotGap) / count);
            float width = count * slotPixels + (count - 1) * slotGap;
            PlaceBar(width);

            for (int i = 0; i < count; i++)
            {
                float x = -width * 0.5f + slotPixels * 0.5f + i * (slotPixels + slotGap);
                views.Add(CreateSlot(i, x));
            }

            for (int i = 0; i < views.Count; i++) Refresh(i);
            ApplySelection();
        }

        SlotView CreateSlot(int index, float x)
        {
            var view = new SlotView();
            view.Root = NewRect("Slot " + (index + 1), bar);
            view.Root.anchorMin = view.Root.anchorMax = new Vector2(0.5f, 0.5f);
            view.Root.pivot = new Vector2(0.5f, 0.5f);
            view.Root.sizeDelta = new Vector2(slotPixels, slotPixels);
            view.Root.anchoredPosition = new Vector2(x, 0f);

            view.Border = NewImage("Border", view.Root, borderColor);
            view.Fill = NewImage("Fill", view.Root, slotColor);
            Stretch(view.Fill.rectTransform, 0f);

            view.Icon = NewImage("Icon", view.Root, Color.white);
            Stretch(view.Icon.rectTransform, slotPixels * iconInset);
            view.Icon.preserveAspect = true;
            view.Icon.enabled = false;

            view.Name = NewText("Name", view.Root, Mathf.Round(slotPixels * 0.2f), FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(view.Name.rectTransform, slotPixels * 0.07f);
            view.Name.enableAutoSizing = true;
            view.Name.fontSizeMin = Mathf.Round(slotPixels * 0.12f);
            view.Name.fontSizeMax = Mathf.Round(slotPixels * 0.22f);
            view.Name.textWrappingMode = TextWrappingModes.Normal;
            // Not Ellipsis: the default TextMesh Pro font has no ellipsis glyph and would log a warning for every label.
            view.Name.overflowMode = TextOverflowModes.Truncate;
            view.Name.gameObject.SetActive(false);

            view.Key = NewText("Key", view.Root, Mathf.Round(slotPixels * 0.19f), FontStyles.Bold, TextAlignmentOptions.TopLeft);
            view.Key.rectTransform.anchorMin = view.Key.rectTransform.anchorMax = new Vector2(0f, 1f);
            view.Key.rectTransform.pivot = new Vector2(0f, 1f);
            view.Key.rectTransform.sizeDelta = new Vector2(slotPixels * 0.5f, slotPixels * 0.3f);
            view.Key.rectTransform.anchoredPosition = new Vector2(slotPixels * 0.07f, -slotPixels * 0.04f);
            view.Key.color = new Color(textColor.r, textColor.g, textColor.b, 0.55f);
            view.Key.text = KeyLabel(index);

            view.Count = NewText("Count", view.Root, Mathf.Round(slotPixels * 0.3f), FontStyles.Bold, TextAlignmentOptions.BottomRight);
            view.Count.rectTransform.anchorMin = view.Count.rectTransform.anchorMax = new Vector2(1f, 0f);
            view.Count.rectTransform.pivot = new Vector2(1f, 0f);
            view.Count.rectTransform.sizeDelta = new Vector2(slotPixels * 0.9f, slotPixels * 0.45f);
            view.Count.rectTransform.anchoredPosition = new Vector2(-slotPixels * 0.07f, slotPixels * 0.03f);
            view.Count.outlineWidth = 0.28f;
            view.Count.outlineColor = new Color32(0, 0, 0, 255);
            view.Count.gameObject.SetActive(false);

            return view;
        }

        // ───────────────────────────── showing ─────────────────────────────

        void Refresh(int index)
        {
            InventorySlot slot = bound.Inventory[index];
            SlotView view = views[index];

            bool hasItem = !slot.IsEmpty;
            bool hasIcon = hasItem && slot.Info.Icon != null;

            view.Icon.sprite = hasIcon ? slot.Info.Icon : null;
            view.Icon.enabled = hasIcon;

            view.Name.gameObject.SetActive(hasItem && !hasIcon);
            if (hasItem && !hasIcon) view.Name.text = slot.Info.Name;

            bool stacked = slot.Count > 1;
            view.Count.gameObject.SetActive(stacked);
            if (stacked) view.Count.text = slot.Count.ToString();

            // A small pop when a slot gains an item.
            if (slot.Count != view.ShownCount)
            {
                if (view.ShownCount >= 0 && slot.Count > view.ShownCount) view.Punch = 1f;
                view.ShownCount = slot.Count;
            }
        }

        void ApplySelection()
        {
            if (bound == null) return;

            for (int i = 0; i < views.Count; i++)
            {
                SlotView view = views[i];
                bool selected = i == bound.SelectedIndex;
                view.Fill.color = selected ? selectedSlotColor : slotColor;
                view.Border.color = selected ? selectedBorderColor : borderColor;
                Stretch(view.Border.rectTransform, -(selected ? 3.5f : 1.5f));
                Color key = textColor;
                key.a = selected ? 0.95f : 0.55f;
                view.Key.color = key;
                view.Name.color = selected ? textColor : new Color(textColor.r, textColor.g, textColor.b, 0.82f);
            }
        }

        static string KeyLabel(int index)
        {
            if (index < 9) return (index + 1).ToString();
            return index == 9 ? "0" : string.Empty;
        }

        // ───────────────────────────── ui helpers ─────────────────────────────

        RectTransform NewRect(string objectName, Transform parent)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        Image NewImage(string objectName, RectTransform parent, Color color)
        {
            RectTransform rect = NewRect(objectName, parent);
            Stretch(rect, 0f);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        TextMeshProUGUI NewText(string objectName, RectTransform parent, float size, FontStyles style, TextAlignmentOptions alignment)
        {
            RectTransform rect = NewRect(objectName, parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = textColor;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
