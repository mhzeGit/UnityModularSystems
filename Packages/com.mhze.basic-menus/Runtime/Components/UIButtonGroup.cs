using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MHZE.BasicMenus
{
    /// <summary>
    /// Manages a list of buttons as a single selectable group (tab bars, custom
    /// button lists...). The selected entry gets its own color, scale and
    /// Z-offset (rendered on top) while hovered entries get a lighter variant.
    /// The <c>onSelected</c> UnityEvent of each entry fires when it becomes the
    /// active entry, so you can drive a tab system from the inspector.
    /// </summary>
    public class UIButtonGroup : MonoBehaviour
    {
        [System.Serializable]
        public class ManagedButton
        {
            [Tooltip("The image of the button managed by this entry.")]
            public Image targetImage;

            [Tooltip("Invoked when this entry becomes the selected one.")]
            public UnityEvent onSelected;

            [Header("Explicit Navigation (optional)")]
            public Selectable onUp;
            public Selectable onDown;
            public Selectable onLeft;
            public Selectable onRight;

            [HideInInspector] public Vector3 initialScale;
            [HideInInspector] public Color initialColor;
            [HideInInspector] public Vector3 initialPosition;
            [HideInInspector] public Color targetColor;
            [HideInInspector] public Vector3 targetScale;
            [HideInInspector] public float targetZ;

            [HideInInspector] public Color startColor;
            [HideInInspector] public Vector3 startScale;
            [HideInInspector] public float startZ;
            [HideInInspector] public float animTime;
            [HideInInspector] public bool isAnimating;
        }

        [Header("Settings")]
        [Tooltip("Transition duration in seconds.")]
        [SerializeField] private float transitionDuration = 0.2f;

        [Header("Button Colors")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color hoverColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        [SerializeField] private Color selectedColor = new Color(0.8f, 0.8f, 0.8f, 1f);

        [Header("Button Scales")]
        [SerializeField] private Vector3 normalScale = Vector3.one;
        [SerializeField] private Vector3 hoverScale = new Vector3(1.05f, 1.05f, 1.05f);
        [SerializeField] private Vector3 selectedScale = new Vector3(1.1f, 1.1f, 1.1f);

        [Header("Z-Depth")]
        [Tooltip("Negative Z brings the selected UI element closer to the camera/screen.")]
        [SerializeField] private float selectedZOffset = 10f;

        [Header("Entries")]
        [SerializeField] private List<ManagedButton> buttons = new List<ManagedButton>();
        [SerializeField] private int defaultSelectedIndex = 0;

        private int selectedIndex = 0;
        private int hoverIndex = -1;

        private void Start()
        {
            selectedIndex = GetValidIndex(defaultSelectedIndex);

            for (int i = 0; i < buttons.Count; i++)
            {
                var button = buttons[i];
                if (button.targetImage == null) continue;

                button.initialScale = button.targetImage.rectTransform.localScale;
                button.initialColor = button.targetImage.color;
                button.initialPosition = button.targetImage.rectTransform.localPosition;

                AddEventTrigger(button.targetImage.gameObject, i);
                UpdateTargetState(i, true);
            }
        }

        /// <summary>Select the entry marked as default.</summary>
        public void ResetToDefault(bool invokeEvent = true)
        {
            ForceSelectedButtonIndex(defaultSelectedIndex, invokeEvent);
        }

        /// <summary>Select a specific entry, ignoring navigation.</summary>
        public void ForceSelectedButtonIndex(int index, bool invokeEvent = true)
        {
            if (buttons.Count == 0) return;

            selectedIndex = GetValidIndex(index);
            hoverIndex = -1;

            if (invokeEvent)
                buttons[selectedIndex].onSelected?.Invoke();

            RefreshAllButtons();
        }

        /// <summary>Follow an explicit navigation link from the selected entry.</summary>
        public void CustomNavigate(string direction)
        {
            if (selectedIndex < 0 || selectedIndex >= buttons.Count) return;

            var button = buttons[selectedIndex];
            Selectable target = direction.ToLower() switch
            {
                "up" => button.onUp,
                "down" => button.onDown,
                "left" => button.onLeft,
                "right" => button.onRight,
                _ => null
            };

            if (target != null)
                target.Select();
        }

        /// <summary>Move the selection by <paramref name="amount"/> entries, wrapping around.</summary>
        public void AddSelectedButtonIndex(int amount)
        {
            if (buttons.Count == 0) return;

            int newIndex = (selectedIndex + amount) % buttons.Count;
            if (newIndex < 0) newIndex += buttons.Count;

            if (newIndex != selectedIndex)
                SetSelectedButtonIndex(newIndex);
        }

        /// <summary>Select the entry at <paramref name="index"/>.</summary>
        public void SetSelectedButtonIndex(int index)
        {
            if (index >= 0 && index < buttons.Count && selectedIndex != index)
                ForceSelectedButtonIndex(index, true);
        }

        private void AddEventTrigger(GameObject gameObject, int index)
        {
            var trigger = gameObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = gameObject.AddComponent<EventTrigger>();

            AddTrigger(trigger, EventTriggerType.PointerEnter, _ => OnHover(index, true));
            AddTrigger(trigger, EventTriggerType.PointerExit, _ => OnHover(index, false));
            AddTrigger(trigger, EventTriggerType.PointerClick, _ => OnSelect(index));
            AddTrigger(trigger, EventTriggerType.Submit, _ => OnSelect(index));
            AddTrigger(trigger, EventTriggerType.Select, _ => OnSelect(index));
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType type, UnityAction<BaseEventData> callback)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(callback);
            trigger.triggers.Add(entry);
        }

        private void OnHover(int index, bool state)
        {
            if (state) hoverIndex = index;
            else if (hoverIndex == index) hoverIndex = -1;

            RefreshAllButtons();
        }

        private void OnSelect(int index)
        {
            if (selectedIndex == index) return;

            selectedIndex = index;
            if (selectedIndex >= 0 && selectedIndex < buttons.Count)
                buttons[selectedIndex].onSelected?.Invoke();

            RefreshAllButtons();
        }

        private int GetValidIndex(int index)
        {
            if (buttons.Count == 0) return -1;
            if (index < 0) return 0;
            if (index >= buttons.Count) return buttons.Count - 1;
            return index;
        }

        private void RefreshAllButtons()
        {
            for (int i = 0; i < buttons.Count; i++)
                UpdateTargetState(i, false);
        }

        private void UpdateTargetState(int index, bool forceImmediate)
        {
            var button = buttons[index];
            if (button.targetImage == null) return;

            bool isSelected = index == selectedIndex;
            bool isHovered = index == hoverIndex;

            Color targetColor;
            Vector3 targetScale;
            float targetZ;

            if (isSelected)
            {
                targetColor = selectedColor;
                targetScale = selectedScale;
                targetZ = selectedZOffset;
            }
            else if (isHovered)
            {
                targetColor = hoverColor;
                targetScale = hoverScale;
                targetZ = 0f;
            }
            else
            {
                targetColor = normalColor;
                targetScale = normalScale;
                targetZ = 0f;
            }

            // Render the selected element on top without reordering the hierarchy.
            var canvas = button.targetImage.GetComponent<Canvas>();
            if (isSelected)
            {
                if (canvas == null) canvas = button.targetImage.gameObject.AddComponent<Canvas>();
                canvas.overrideSorting = true;
                canvas.sortingOrder = 1;

                if (button.targetImage.GetComponent<GraphicRaycaster>() == null)
                    button.targetImage.gameObject.AddComponent<GraphicRaycaster>();
            }
            else if (canvas != null)
            {
                Destroy(button.targetImage.GetComponent<GraphicRaycaster>());
                Destroy(canvas);
            }

            bool colorChanged = targetColor != button.targetColor;
            bool scaleChanged = targetScale != button.targetScale;
            bool zChanged = !Mathf.Approximately(targetZ, button.targetZ);

            if (forceImmediate)
            {
                button.targetColor = targetColor;
                button.targetScale = targetScale;
                button.targetZ = targetZ;

                button.targetImage.color = targetColor;
                button.targetImage.rectTransform.localScale = targetScale;

                Vector3 position = button.initialPosition;
                position.z += targetZ;
                button.targetImage.rectTransform.localPosition = position;

                button.isAnimating = false;
            }
            else if (colorChanged || scaleChanged || zChanged)
            {
                button.startColor = button.targetImage.color;
                button.startScale = button.targetImage.rectTransform.localScale;
                button.startZ = button.targetImage.rectTransform.localPosition.z - button.initialPosition.z;

                button.targetColor = targetColor;
                button.targetScale = targetScale;
                button.targetZ = targetZ;

                button.animTime = 0f;
                button.isAnimating = true;
            }
        }

        private void Update()
        {
            float delta = Time.unscaledDeltaTime;
            float safeDuration = Mathf.Max(0.0001f, transitionDuration);

            for (int i = 0; i < buttons.Count; i++)
            {
                var button = buttons[i];
                if (!button.isAnimating || button.targetImage == null) continue;

                button.animTime += delta;
                float t = Mathf.Clamp01(button.animTime / safeDuration);
                float easedT = Mathf.SmoothStep(0f, 1f, t);

                button.targetImage.color = Color.Lerp(button.startColor, button.targetColor, easedT);
                button.targetImage.rectTransform.localScale = Vector3.Lerp(button.startScale, button.targetScale, easedT);

                Vector3 position = button.initialPosition;
                position.z += Mathf.Lerp(button.startZ, button.targetZ, easedT);
                button.targetImage.rectTransform.localPosition = position;

                if (t >= 1f)
                    button.isAnimating = false;
            }
        }
    }
}
