using UnityEngine;

namespace MHZE.InventorySystem
{
    /// <summary>
    /// Keeps a piece of the screen's UI (the subtitles, an interaction prompt) above the inventory bar while the bar is on
    /// screen, and lets it settle back into the freed space when the bar slides away. Put it on the UI element, a
    /// <see cref="RectTransform"/> on a canvas, that sits at the bottom of the screen where the bar comes up.
    /// <para>
    /// The element is only ever moved up from where it was authored, by as much as it takes to clear the bar and its
    /// "Inventory full" notice (and <see cref="gap"/>), and by the same slide as the bar (<see cref="InventoryBarView.Reveal"/>),
    /// so the two rise and fall together. An element that already sits above the bar is not moved. It is put back where it
    /// was when this component is switched off. It does nothing for a bar at the top of the screen.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("MHZE/Inventory/Inventory Bar Avoider")]
    public sealed class InventoryBarAvoider : MonoBehaviour
    {
        [Tooltip("Found in the scene when empty.")]
        [SerializeField] InventoryBarView bar;
        [Tooltip("Free space kept between the bar's notice and this element, in canvas pixels.")]
        [SerializeField, Min(0f)] float gap = 10f;

        static readonly Vector3[] corners = new Vector3[4];

        RectTransform rect;
        Canvas canvas;
        float lifted;
        float nextLook;

        /// <summary>How far the element is raised now, in the units of its own position (canvas pixels).</summary>
        public float Lift => lifted;

        void OnEnable()
        {
            rect = (RectTransform)transform;
            Canvas parent = GetComponentInParent<Canvas>(true);
            canvas = parent != null ? parent.rootCanvas : null;

            // Fast Play keeps what is not serialized from the last run, and the clock starts over.
            lifted = 0f;
            nextLook = 0f;
        }

        void OnDisable()
        {
            // Put the element back where it was authored.
            if (rect != null && lifted != 0f)
            {
                Vector2 position = rect.anchoredPosition;
                position.y -= lifted;
                rect.anchoredPosition = position;
            }

            lifted = 0f;
        }

        void LateUpdate()
        {
            if (!HasBar()) return;

            float pixel = PixelsPerUnit();
            float wanted = 0f;
            if (bar.AtBottom && bar.Reveal > 0f)
            {
                // Where the lower edge would be without us, against the top of the bar's notice.
                float restBottom = ScreenBottom() - lifted * pixel;
                float needed = Mathf.Max(0f, bar.ClearanceHeight + gap * pixel - restBottom);
                wanted = needed * bar.Reveal / pixel;
            }

            if (Mathf.Approximately(wanted, lifted)) return;

            Vector2 position = rect.anchoredPosition;
            position.y += wanted - lifted;
            rect.anchoredPosition = position;
            lifted = wanted;
        }

        bool HasBar()
        {
            if (bar != null) return true;
            if (Time.realtimeSinceStartup < nextLook) return false;

            // The bar may appear after this element does; look again now and then, not every frame.
            nextLook = Time.realtimeSinceStartup + 0.5f;
            bar = FindAnyObjectByType<InventoryBarView>();
            return bar != null;
        }

        Camera EventCamera()
        {
            return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        }

        // The lower edge of the element on the screen, in pixels from the bottom of the screen.
        float ScreenBottom()
        {
            rect.GetWorldCorners(corners);
            Camera camera = EventCamera();
            float bottom = float.MaxValue;
            for (int i = 0; i < corners.Length; i++)
            {
                bottom = Mathf.Min(bottom, RectTransformUtility.WorldToScreenPoint(camera, corners[i]).y);
            }

            return bottom;
        }

        // Screen pixels in one unit of the element's anchored position.
        float PixelsPerUnit()
        {
            Transform parent = rect.parent;
            if (parent == null) return 1f;

            Camera camera = EventCamera();
            float from = RectTransformUtility.WorldToScreenPoint(camera, parent.TransformPoint(Vector3.zero)).y;
            float to = RectTransformUtility.WorldToScreenPoint(camera, parent.TransformPoint(Vector3.up)).y;
            return Mathf.Max(0.0001f, Mathf.Abs(to - from));
        }
    }
}
