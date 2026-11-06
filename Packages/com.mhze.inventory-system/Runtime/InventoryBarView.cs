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
    /// The inventory's bar: a row of separate dark stone slabs at the bottom of the screen, built in code on its own
    /// screen-space canvas. Each slab shows its number key in chalk, the item's icon (its name until it has one) and the
    /// stack size when it is more than one. The selected slab is lighter, has a thin ochre edge and chalk marks in
    /// three corners, and a short ochre line under it. The stone is drawn by the bar itself (a different grain for every
    /// slab); hand-made sprites can replace it.
    /// <para>
    /// The bar keeps out of the way: it slides off the edge of the screen when nothing has changed for a few seconds, and
    /// comes back for a pickup, a drop, a switch of slot or a refused pickup (<see cref="AutoHide"/>,
    /// <see cref="VisibleSeconds"/>). <see cref="InventoryBarAvoider"/> lets other UI use the space while it is away.
    /// It reads everything from <see cref="PlayerInventory"/> and changes nothing there.
    /// </para>
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
        [SerializeField, Min(24f)] float slotSize = 104f;
        [SerializeField, Min(0f)] float slotGap = 12f;
        [Tooltip("Free space between the slot's edge and the item's icon, as a fraction of the slot.")]
        [SerializeField, Range(0f, 0.3f)] float iconInset = 0.06f;
        [Tooltip("Higher draws on top. Keep it below pause menus and fades.")]
        [SerializeField] int sortingOrder = 30;

        [Header("Showing")]
        [Tooltip("The bar slides off the edge of the screen when nothing has changed for a while, and comes back when the player switches slot, picks something up or drops it. Off keeps it on screen.")]
        [SerializeField] bool autoHide = true;
        [Tooltip("Seconds the bar stays on screen after the last change.")]
        [SerializeField, Min(0.2f)] float visibleSeconds = 3.5f;
        [Tooltip("About how long the bar takes to slide in, in seconds.")]
        [SerializeField, Min(0.03f)] float slideInSeconds = 0.2f;
        [Tooltip("About how long the bar takes to slide away, in seconds.")]
        [SerializeField, Min(0.03f)] float slideOutSeconds = 0.4f;
        [Tooltip("Pressing the key of the slot that is already selected brings the bar in, so the player can look at it without changing anything.")]
        [SerializeField] bool showOnSameSlot = true;

        [Header("Look")]
        [Tooltip("Uses the TextMesh Pro default font when empty.")]
        [SerializeField] TMP_FontAsset font;
        [Tooltip("Hand-made art for a slot, stretched over the whole square. Empty: the bar draws a dark stone slab.")]
        [SerializeField] Sprite slotSprite;
        [Tooltip("The same for the selected slot. Empty: the slot sprite, or a lighter stone slab.")]
        [SerializeField] Sprite selectedSlotSprite;
        [Tooltip("The colour of the stone slab of a slot.")]
        [SerializeField] Color stoneColor = new Color(0.165f, 0.148f, 0.13f, 1f);
        [Tooltip("The colour of the selected slot's stone.")]
        [SerializeField] Color selectedStoneColor = new Color(0.29f, 0.255f, 0.19f, 1f);
        [Tooltip("The thin edge of the selected slot and the short line under it.")]
        [SerializeField] Color accentColor = new Color(0.91f, 0.72f, 0.3f, 1f);
        [Tooltip("The chalk of the numbers, the count and the corner marks of the selected slot.")]
        [SerializeField] Color textColor = new Color(0.97f, 0.95f, 0.88f, 1f);
        [Tooltip("1 keeps the selected slot the size of the others; more makes it bigger.")]
        [SerializeField, Min(1f)] float selectedSlotScale = 1f;

        [Header("Messages")]
        [SerializeField] string fullMessage = "Inventory full";

        const float MaxBarWidth = 1800f;
        const float NoticeSeconds = 1.6f;
        const float NoticeHeight = 40f;
        const float NoticeOffset = 18f;

        sealed class SlotView
        {
            public RectTransform Root;
            public Image Shadow;
            public Image Fill;
            public RectTransform Marks;
            public int Variant;
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
        Canvas canvas;
        RectTransform bar;
        TextMeshProUGUI notice;
        float noticeUntil;
        float slotPixels;
        PlayerInventory bound;

        // Sliding away: where the bar rests, which way it leaves and how far, and how far it has come back. The waits are
        // measured in real time (not the clock of the current frame), so a change raised between two frames, or during a
        // long one, gets its full wait.
        Vector2 restPosition;
        float slideDirection = -1f;
        float slideDistance;
        float barClearance;
        float noticeSpace;
        float reveal;
        float appliedReveal = -1f;
        float revealSpeed;
        float shownUntil;

        // The stone drawn by the bar: a few slabs with a different grain each, plain and selected, and a soft shadow.
        const int StoneVariants = 4;
        const int StoneSize = 192;
        const float StoneRadius = 0.05f;
        const float ShadowPad = 0.11f;
        const float ShadowDrop = 0.05f;
        readonly List<Object> art = new List<Object>();
        Sprite[] stone = new Sprite[0];
        Sprite[] stoneSelected = new Sprite[0];
        Sprite shadow;
        Sprite lineSprite;
        Color artStone;
        Color artSelectedStone;
        Color artAccent;

        // ───────────────────────────── showing and hiding ─────────────────────────────

        /// <summary>
        /// The bar slides off the edge of the screen when nothing changed for <see cref="VisibleSeconds"/>, and comes back
        /// for a pickup, a drop, a switch of slot or a refused pickup. Off keeps it on screen all the time.
        /// </summary>
        public bool AutoHide
        {
            get => autoHide;
            set => autoHide = value;
        }

        /// <summary>Seconds the bar stays on screen after the last change.</summary>
        public float VisibleSeconds
        {
            get => visibleSeconds;
            set => visibleSeconds = Mathf.Max(0.05f, value);
        }

        /// <summary>How far the bar is on screen: 0 is away, 1 is fully there. It follows an eased slide, not a jump.</summary>
        public float Reveal => reveal;

        /// <summary>Any part of the bar is on screen.</summary>
        public bool IsOnScreen => reveal > 0f;

        /// <summary>The bar sits at the bottom of the screen (rather than the top).</summary>
        public bool AtBottom => !IsTop(anchor);

        /// <summary>
        /// Screen pixels from the edge the bar sits at up to the top of the bar when it is fully shown, and up to the top of
        /// its "Inventory full" notice while that is on screen. What <see cref="InventoryBarAvoider"/> keeps other UI above.
        /// </summary>
        public float ClearanceHeight => (barClearance + noticeSpace * (NoticeOffset + NoticeHeight)) * (canvas != null ? Mathf.Max(0.01f, canvas.scaleFactor) : 1f);

        /// <summary>Brings the bar on screen and keeps it there for <see cref="VisibleSeconds"/> from now.</summary>
        public void Show()
        {
            Show(visibleSeconds);
        }

        /// <summary>Brings the bar on screen and keeps it there for at least this many seconds from now.</summary>
        public void Show(float seconds)
        {
            shownUntil = Mathf.Max(shownUntil, Time.realtimeSinceStartup + Mathf.Max(0f, seconds));
        }

        /// <summary>Slides the bar away now, unless <see cref="AutoHide"/> is off.</summary>
        public void Hide()
        {
            shownUntil = 0f;
        }

        // ───────────────────────────── lifecycle ─────────────────────────────

        void Awake()
        {
            if (inventory == null) inventory = GetComponentInParent<PlayerInventory>();

            // Fast Play keeps what is not serialized from the last run, and the clock starts over: start from nothing.
            reveal = autoHide ? 0f : 1f;
            revealSpeed = 0f;
            appliedReveal = -1f;
            shownUntil = 0f;
            noticeUntil = 0f;
            noticeSpace = 0f;
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
            DestroyArt();
        }

        void Bind(PlayerInventory source)
        {
            if (bound == source) return;

            Unbind();
            bound = source;
            bound.SlotChanged += HandleSlotChanged;
            bound.SlotCountChanged += HandleSlotCountChanged;
            bound.SelectionChanged += HandleSelectionChanged;
            bound.SelectionRepeated += HandleSelectionRepeated;
            bound.PickupRefused += HandleRefused;
            bound.SelectionBlocked += HandleBlocked;
        }

        void Unbind()
        {
            if (bound == null) return;

            bound.SlotChanged -= HandleSlotChanged;
            bound.SlotCountChanged -= HandleSlotCountChanged;
            bound.SelectionChanged -= HandleSelectionChanged;
            bound.SelectionRepeated -= HandleSelectionRepeated;
            bound.PickupRefused -= HandleRefused;
            bound.SelectionBlocked -= HandleBlocked;
            bound = null;
        }

        // The hand holds something that has to stay: the selected slot gives a small start instead of moving on.
        void HandleBlocked()
        {
            int index = bound.SelectedIndex;
            if (index >= 0 && index < views.Count) views[index].Punch = 1f;
            Show();
        }

        void HandleSlotChanged(int index)
        {
            if (index >= 0 && index < views.Count) Refresh(index);
            Show();
        }

        void HandleSlotCountChanged()
        {
            // The first build is not a change the player made; a different number of slots later is.
            bool changed = views.Count > 0 && bound != null && views.Count != bound.SlotCount;
            Rebuild();
            if (changed) Show();
        }

        void HandleSelectionChanged(int index)
        {
            ApplySelection();
            Show();
        }

        // Pressing the key of the slot that is already selected changes nothing, but it is a question about the bar.
        void HandleSelectionRepeated()
        {
            if (showOnSameSlot) Show();
        }

        void HandleRefused(GameObject item)
        {
            Show();
            if (notice == null) return;

            notice.text = fullMessage;
            noticeUntil = Time.realtimeSinceStartup + NoticeSeconds;
        }

        void Update()
        {
            if (bound == null) return;

            float dt = Time.unscaledDeltaTime;
            float follow = 1f - Mathf.Exp(-20f * dt);
            for (int i = 0; i < views.Count; i++)
            {
                SlotView view = views[i];
                float target = i == bound.SelectedIndex ? selectedSlotScale : 1f;
                view.Scale = Mathf.Lerp(view.Scale, target, follow);
                view.Punch = Mathf.MoveTowards(view.Punch, 0f, dt * 5f);
                float pop = view.Punch * view.Punch * 0.08f;
                view.Root.localScale = Vector3.one * (view.Scale + pop);
            }

            if (notice != null)
            {
                float left = noticeUntil - Time.realtimeSinceStartup;
                Color c = notice.color;
                c.a = Mathf.Clamp01(left / 0.4f);
                notice.color = c;

                // Room for the notice above the bar comes and goes smoothly, so whatever rides above the bar does too.
                noticeSpace = Mathf.MoveTowards(noticeSpace, left > 0f ? 1f : 0f, dt * 6f);
            }

            UpdateReveal(dt);
        }

        // Slides towards "there" while something changed lately (or auto hide is off) and towards "away" otherwise. The slide
        // is a critically damped spring, so a change that arrives while the bar is leaving turns it round without a jerk.
        void UpdateReveal(float dt)
        {
            if (bar == null) return;

            float goal = !autoHide || Time.realtimeSinceStartup < shownUntil ? 1f : 0f;
            if (!Mathf.Approximately(reveal, goal))
            {
                float seconds = goal > reveal ? slideInSeconds : slideOutSeconds;
                reveal = Mathf.SmoothDamp(reveal, goal, ref revealSpeed, seconds * 0.5f, float.PositiveInfinity, dt);
                if (Mathf.Abs(reveal - goal) < 0.002f)
                {
                    reveal = goal;
                    revealSpeed = 0f;
                }
            }

            ApplyReveal();
        }

        void ApplyReveal(bool force = false)
        {
            // Nothing is drawn, laid out or batched while the bar is away.
            bool onScreen = reveal > 0f;
            if (canvas != null && canvas.enabled != onScreen) canvas.enabled = onScreen;

            // Moving the bar dirties its canvas, so it is only touched while it slides.
            if (!force && Mathf.Approximately(appliedReveal, reveal)) return;
            appliedReveal = reveal;
            bar.anchoredPosition = restPosition + new Vector2(0f, slideDirection * (1f - reveal) * slideDistance);
        }

        // ───────────────────────────── building ─────────────────────────────

        void BuildCanvas()
        {
            if (canvasObject != null) return;

            canvasObject = new GameObject("Inventory Bar", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) canvasObject.layer = uiLayer;

            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            bar = NewRect("Bar", canvasObject.transform);
            BuildArt();

            notice = NewText("Notice", bar, 26f, FontStyles.Bold, TextAlignmentOptions.Bottom);
            notice.rectTransform.sizeDelta = new Vector2(600f, NoticeHeight);
            notice.color = new Color(textColor.r, textColor.g, textColor.b, 0f);

            // It starts away; the first change brings it in.
            reveal = autoHide ? 0f : 1f;
            revealSpeed = 0f;
        }

        static bool IsTop(BarAnchor value)
        {
            return value == BarAnchor.TopCenter || value == BarAnchor.TopLeft || value == BarAnchor.TopRight;
        }

        // The bar sits at its corner or edge of the screen; the notice is on the side of the bar that faces the screen.
        void PlaceBar(float width)
        {
            bool left = anchor == BarAnchor.BottomLeft || anchor == BarAnchor.TopLeft;
            bool right = anchor == BarAnchor.BottomRight || anchor == BarAnchor.TopRight;
            bool top = IsTop(anchor);

            var corner = new Vector2(left ? 0f : right ? 1f : 0.5f, top ? 1f : 0f);
            bar.anchorMin = bar.anchorMax = corner;
            bar.pivot = corner;
            restPosition = new Vector2(left ? margin.x : right ? -margin.x : 0f, top ? -margin.y : margin.y);
            bar.sizeDelta = new Vector2(width, slotPixels);

            // Away is past the edge of the screen: the margin, the slot (as big as it gets) and its shadow and line.
            float tall = slotPixels * Mathf.Max(1.1f, selectedSlotScale);
            slideDirection = top ? 1f : -1f;
            slideDistance = margin.y + tall + 16f;
            barClearance = margin.y + slotPixels * selectedSlotScale + 4f;

            RectTransform note = notice.rectTransform;
            var noteSide = new Vector2(corner.x, top ? 0f : 1f);
            note.anchorMin = note.anchorMax = noteSide;
            note.pivot = new Vector2(corner.x, top ? 1f : 0f);
            note.anchoredPosition = new Vector2(0f, top ? -NoticeOffset : NoticeOffset);
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
            if (artStone != stoneColor || artSelectedStone != selectedStoneColor || artAccent != accentColor) BuildArt();

            int count = bound.SlotCount;
            slotPixels = Mathf.Min(slotSize, (MaxBarWidth - (count - 1) * slotGap) / count);
            EnsureLine();
            float width = count * slotPixels + (count - 1) * slotGap;
            PlaceBar(width);
            ApplyReveal(true);

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

            view.Variant = index % Mathf.Max(1, stone.Length);

            // A soft shadow under the slab, so the slabs stand apart from the picture behind them.
            view.Shadow = NewImage("Shadow", view.Root, Color.white);
            float pad = Mathf.Round(slotPixels * ShadowPad);
            float drop = Mathf.Round(slotPixels * ShadowDrop);
            view.Shadow.rectTransform.offsetMin = new Vector2(-pad, -pad - drop);
            view.Shadow.rectTransform.offsetMax = new Vector2(pad, pad - drop);
            view.Shadow.sprite = shadow;
            view.Shadow.enabled = shadow != null;

            view.Fill = NewImage("Fill", view.Root, Color.white);
            Stretch(view.Fill.rectTransform, 0f);
            view.Fill.sprite = stone[view.Variant];

            view.Marks = CreateMarks(view.Root);

            view.Icon = NewImage("Icon", view.Root, Color.white);
            Stretch(view.Icon.rectTransform, slotPixels * iconInset);
            view.Icon.rectTransform.offsetMax = new Vector2(-slotPixels * iconInset, -slotPixels * (iconInset + 0.03f));
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

            // The number sits clear of the corner marks.
            view.Key = NewText("Key", view.Root, Mathf.Round(slotPixels * 0.19f), FontStyles.Bold, TextAlignmentOptions.TopLeft);
            view.Key.rectTransform.anchorMin = view.Key.rectTransform.anchorMax = new Vector2(0f, 1f);
            view.Key.rectTransform.pivot = new Vector2(0f, 1f);
            view.Key.rectTransform.sizeDelta = new Vector2(slotPixels * 0.4f, slotPixels * 0.3f);
            view.Key.rectTransform.anchoredPosition = new Vector2(slotPixels * 0.13f, -slotPixels * 0.07f);
            view.Key.color = new Color(textColor.r, textColor.g, textColor.b, 0.85f);
            view.Key.outlineWidth = 0.2f;
            view.Key.outlineColor = new Color32(0, 0, 0, 190);
            view.Key.text = KeyLabel(index);

            view.Count = NewText("Count", view.Root, Mathf.Round(slotPixels * 0.27f), FontStyles.Bold, TextAlignmentOptions.BottomRight);
            view.Count.rectTransform.anchorMin = view.Count.rectTransform.anchorMax = new Vector2(1f, 0f);
            view.Count.rectTransform.pivot = new Vector2(1f, 0f);
            view.Count.rectTransform.sizeDelta = new Vector2(slotPixels * 0.9f, slotPixels * 0.42f);
            view.Count.rectTransform.anchoredPosition = new Vector2(-slotPixels * 0.1f, slotPixels * 0.05f);
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
                view.Fill.sprite = selected ? stoneSelected[view.Variant] : stone[view.Variant];
                view.Marks.gameObject.SetActive(selected);
                Color key = textColor;
                key.a = selected ? 1f : 0.85f;
                view.Key.color = key;
                view.Name.color = selected ? textColor : new Color(textColor.r, textColor.g, textColor.b, 0.82f);
            }
        }

        static string KeyLabel(int index)
        {
            if (index < 9) return (index + 1).ToString();
            return index == 9 ? "0" : string.Empty;
        }

        // ───────────────────────────── the selected slot's marks ─────────────────────────────

        // Chalk marks in three corners of the slab (the fourth is where the number is) and a rounded line under it. They sit
        // between the slab and its icon, so the icon is drawn over them, and they are only switched on for the selected slot.
        RectTransform CreateMarks(RectTransform slot)
        {
            RectTransform marks = NewRect("Marks", slot);
            Stretch(marks, 0f);

            float inset = Mathf.Max(4f, Mathf.Round(slotPixels * 0.06f));
            float arm = Mathf.Round(slotPixels * 0.2f);
            float thick = Mathf.Max(2f, Mathf.Round(slotPixels * 0.022f));
            for (int corner = 0; corner < 4; corner++)
            {
                if (corner == 2) continue;   // top left: the number

                float sx = (corner & 1) == 0 ? 0f : 1f;
                float sy = (corner & 2) == 0 ? 0f : 1f;
                var anchor = new Vector2(sx, sy);
                var pivot = new Vector2(sx, sy);
                var offset = new Vector2(sx == 0f ? inset : -inset, sy == 0f ? inset : -inset);
                NewMark("Corner " + corner + " across", marks, anchor, pivot, new Vector2(arm, thick), offset, textColor);
                NewMark("Corner " + corner + " down", marks, anchor, pivot, new Vector2(thick, arm), offset, textColor);
            }

            RectTransform line = NewRect("Line", marks);
            line.anchorMin = line.anchorMax = new Vector2(0.5f, 0f);
            line.pivot = new Vector2(0.5f, 1f);
            line.sizeDelta = new Vector2(lineSprite.rect.width, lineSprite.rect.height);
            line.anchoredPosition = new Vector2(0f, -Mathf.Round(slotPixels * 0.07f));
            var lineImage = line.gameObject.AddComponent<Image>();
            lineImage.sprite = lineSprite;
            lineImage.color = accentColor;
            lineImage.raycastTarget = false;
            marks.gameObject.SetActive(false);
            return marks;
        }

        void NewMark(string objectName, RectTransform parent, Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 position, Color color)
        {
            RectTransform rect = NewRect(objectName, parent);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        // ───────────────────────────── the stone ─────────────────────────────

        void BuildArt()
        {
            DestroyArt();
            artStone = stoneColor;
            artSelectedStone = selectedStoneColor;
            artAccent = accentColor;

            shadow = MakeShadow();

            if (slotSprite != null)
            {
                stone = new[] { slotSprite };
                stoneSelected = new[] { selectedSlotSprite != null ? selectedSlotSprite : slotSprite };
                return;
            }

            stone = new Sprite[StoneVariants];
            stoneSelected = new Sprite[StoneVariants];
            for (int v = 0; v < StoneVariants; v++)
            {
                stone[v] = MakeStone(11 + v * 17, stoneColor, default);
                stoneSelected[v] = MakeStone(11 + v * 17, selectedStoneColor, accentColor);
            }
        }

        void DestroyArt()
        {
            foreach (Object made in art)
            {
                if (made != null) Destroy(made);
            }

            art.Clear();
            stone = new Sprite[0];
            stoneSelected = new Sprite[0];
            shadow = null;
            lineSprite = null;
        }

        // A square slab of dark stone with softly rounded corners: broad patches of lighter and darker stone, fine grain, a few
        // flecks and pits, a little lighter towards the top, light from the upper left (a lit top and left edge, a shaded
        // bottom and right edge) and a near-black outline. The selected slab also has a thin edge in the accent colour.
        // The same seed gives the same slab in the plain and the selected colour.
        Sprite MakeStone(int seed, Color baseColor, Color edgeColor)
        {
            const int n = StoneSize;
            float half = n * 0.5f;
            float radius = n * StoneRadius;
            var random = new System.Random(seed);
            var specks = new float[n * n];
            for (int i = 0; i < 30; i++) AddSpeck(specks, random, n, 0.035f);
            for (int i = 0; i < 22; i++) AddSpeck(specks, random, n, -0.03f);

            var light = new Vector2(-0.55f, 0.83f);   // falls from the upper left; y points up in a texture
            bool edged = edgeColor.a > 0f;
            var pixels = new Color32[n * n];
            float ox = seed * 3.1f, oy = seed * 1.7f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    int index = y * n + x;
                    float distance = BoxDistance(x + 0.5f, y + 0.5f, half, half, radius);
                    float coverage = Mathf.Clamp01(0.5f - distance);
                    if (coverage <= 0f) { pixels[index] = default; continue; }

                    float depth = -distance;
                    float u = x / (float)n, w = y / (float)n;
                    float value = (Mathf.PerlinNoise(u * 2.4f + ox, w * 2.4f + oy) - 0.5f) * 0.034f
                                  + (Mathf.PerlinNoise(u * 8f + oy, w * 8f + ox) - 0.5f) * 0.02f
                                  + ((float)random.NextDouble() - 0.5f) * 0.013f
                                  + specks[index]
                                  + (w - 0.5f) * 0.03f;

                    // Just inside the outline the edge catches the light on the upper left and falls into shade on the lower right.
                    if (depth > 2f && depth < 6f)
                    {
                        float dx = BoxDistance(x + 1.5f, y + 0.5f, half, half, radius) - BoxDistance(x - 0.5f, y + 0.5f, half, half, radius);
                        float dy = BoxDistance(x + 0.5f, y + 1.5f, half, half, radius) - BoxDistance(x + 0.5f, y - 0.5f, half, half, radius);
                        var normal = new Vector2(dx, dy);
                        if (normal.sqrMagnitude > 0.0001f) normal.Normalize();
                        float bevel = Mathf.Clamp01(1f - (depth - 2f) / 4f);
                        value += Vector2.Dot(normal, light) * 0.07f * bevel;
                    }

                    var c = new Color(baseColor.r + value, baseColor.g + value * 0.97f, baseColor.b + value * 0.92f, 1f);

                    if (edged)
                    {
                        // A thin edge of ochre, a little inside the outline.
                        float band = Mathf.Clamp01(Mathf.Min(depth - 2.6f, 6.4f - depth));
                        c = Color.Lerp(c, new Color(edgeColor.r, edgeColor.g, edgeColor.b, 1f), band * edgeColor.a);
                    }

                    float outline = Mathf.Clamp01(2.6f - depth);
                    c = Color.Lerp(c, new Color(0.03f, 0.028f, 0.026f, 1f), outline);
                    pixels[index] = new Color32(ToByte(c.r), ToByte(c.g), ToByte(c.b), ToByte(coverage));
                }
            }

            return MakeSprite(pixels, n, n, "Inventory Stone");
        }

        // The line under the selected slot: a pill drawn at the size it is shown, so its ends are round and not stretched.
        void EnsureLine()
        {
            int w = Mathf.Max(8, Mathf.RoundToInt(slotPixels * 0.9f));
            int h = Mathf.Max(3, Mathf.RoundToInt(slotPixels * 0.04f));
            if (lineSprite != null && Mathf.RoundToInt(lineSprite.rect.width) == w && Mathf.RoundToInt(lineSprite.rect.height) == h) return;

            if (lineSprite != null)
            {
                Texture2D old = lineSprite.texture;
                art.Remove(lineSprite);
                art.Remove(old);
                Destroy(lineSprite);
                Destroy(old);
            }

            var pixels = new Color32[w * h];
            float radius = h * 0.5f;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float centre = Mathf.Clamp(x + 0.5f, radius, w - radius);
                    float dx = x + 0.5f - centre, dy = y + 0.5f - radius;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                    pixels[y * w + x] = new Color32(255, 255, 255, ToByte(Mathf.Clamp01(0.5f - distance)));
                }
            }

            lineSprite = MakeSprite(pixels, w, h, "Inventory Line");
        }

        // A soft dark halo in the shape of a slab, a little bigger than the slab and drawn under it.
        Sprite MakeShadow()
        {
            const int n = 64;
            float half = n * 0.5f;
            float slabHalf = half / (1f + ShadowPad * 2f);
            float radius = slabHalf * StoneRadius * 2f;
            float blur = n * 0.07f;
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float distance = BoxDistance(x + 0.5f, y + 0.5f, half, slabHalf, radius);
                    float t = Mathf.Clamp01(0.5f - distance / (blur * 2f));
                    float alpha = t * t * (3f - 2f * t) * 0.5f;
                    pixels[y * n + x] = new Color32(0, 0, 0, ToByte(alpha));
                }
            }

            return MakeSprite(pixels, n, n, "Inventory Shadow");
        }

        Sprite MakeSprite(Color32[] pixels, int width, int height, string spriteName)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
            {
                name = spriteName,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            art.Add(texture);

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = spriteName;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            art.Add(sprite);
            return sprite;
        }

        // How far a point is from the edge of a rounded square centred in a texture of side 2 * centre: negative inside,
        // positive outside. half is the square's half side and radius the roundness of its corners.
        static float BoxDistance(float x, float y, float centre, float half, float radius)
        {
            float qx = Mathf.Abs(x - centre) - (half - radius);
            float qy = Mathf.Abs(y - centre) - (half - radius);
            float outsideX = Mathf.Max(qx, 0f), outsideY = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        static void AddSpeck(float[] specks, System.Random random, int n, float amount)
        {
            int x = random.Next(10, n - 11), y = random.Next(10, n - 11);
            int size = random.Next(1, 4);
            for (int dy = 0; dy < size; dy++)
                for (int dx = 0; dx < size; dx++)
                    specks[(y + dy) * n + x + dx] += amount;
        }

        static byte ToByte(float value)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);
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
