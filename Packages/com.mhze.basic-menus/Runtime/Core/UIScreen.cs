using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MHZE.BasicMenus
{
    /// <summary>
    /// Base class for every menu screen (main menu, pause menu, options, info, popups...).
    ///
    /// A screen either toggles its own GameObject or an assigned content panel.
    /// The content-panel mode keeps the GameObject active so the screen keeps
    /// receiving input and stays registered with <see cref="UIBackRouter"/>.
    ///
    /// Screens register themselves with the router automatically (no manual
    /// wiring) and expose <see cref="Opened"/> / <see cref="Closed"/> events plus
    /// matching UnityEvents for inspector listeners.
    /// </summary>
    public abstract class UIScreen : MonoBehaviour
    {
        [Header("Screen")]
        [Tooltip("Selectable highlighted when the player switches to keyboard/gamepad while this screen is visible.")]
        [SerializeField] protected Selectable defaultSelectable;

        [Tooltip("Hide this screen on Awake. Leave enabled for every screen except the main menu root.")]
        [SerializeField] protected bool startHidden = true;

        [Tooltip("Optional child panel toggled instead of this whole GameObject. Assign it when the screen must stay active (e.g. pause menu).")]
        [SerializeField] protected GameObject contentPanel;

        [Header("Events")]
        [Tooltip("Invoked after this screen opens.")]
        [SerializeField] private UnityEvent onOpened = new UnityEvent();

        [Tooltip("Invoked after this screen closes.")]
        [SerializeField] private UnityEvent onClosed = new UnityEvent();

        private static int _openCount;

        /// <summary>Invoked whenever <see cref="AnyOpen"/> changes.</summary>
        public static event Action<bool> AnyOpenChanged;

        /// <summary>True while at least one screen is visible.</summary>
        public static bool AnyOpen => _openCount > 0;

        private bool _isOpen;

        /// <summary>Is this screen currently visible?</summary>
        public bool IsOpen
        {
            get => _isOpen;
            protected set
            {
                if (_isOpen == value) return;

                bool hadAnyOpen = AnyOpen;
                _isOpen = value;
                if (value) _openCount++;
                else _openCount = Mathf.Max(0, _openCount - 1);

                if (hadAnyOpen != AnyOpen)
                    AnyOpenChanged?.Invoke(AnyOpen);
            }
        }

        /// <summary>Invoked after this screen opens.</summary>
        public event Action Opened;

        /// <summary>Invoked after this screen closes.</summary>
        public event Action Closed;

        /// <summary>
        /// Back-button priority; higher values are handled first.
        /// Suggested values: 0-40 pause toggles / root menus, 50-90 normal screens
        /// and sub-menus, 100+ popups that must block everything else.
        /// </summary>
        public virtual int BackPriority => 50;

        protected virtual void Awake()
        {
            // Open() sets _isOpen before activating the GameObject, so a screen
            // opened by another screen never hides itself here.
            if (startHidden && !_isOpen)
            {
                if (contentPanel != null) contentPanel.SetActive(false);
                else gameObject.SetActive(false);
            }
        }

        protected virtual void OnEnable()
        {
            UIBackRouter.Register(this);
        }

        protected virtual void OnDisable()
        {
            UIBackRouter.Unregister(this);
        }

        protected virtual void OnDestroy()
        {
            // Safety: keep the shared counter accurate if a screen is destroyed while open.
            if (!_isOpen) return;

            bool hadAnyOpen = AnyOpen;
            _openCount = Mathf.Max(0, _openCount - 1);
            _isOpen = false;

            if (hadAnyOpen != AnyOpen)
                AnyOpenChanged?.Invoke(AnyOpen);
        }

        /// <summary>Show this screen.</summary>
        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;

            if (contentPanel != null) contentPanel.SetActive(true);
            else gameObject.SetActive(true);

            OnOpened();
            onOpened.Invoke();
            Opened?.Invoke();
            PushFocus();
        }

        /// <summary>Hide this screen.</summary>
        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            OnClosed();

            if (contentPanel != null) contentPanel.SetActive(false);
            else gameObject.SetActive(false);

            onClosed.Invoke();
            Closed?.Invoke();
        }

        /// <summary>Open when closed, close when open.</summary>
        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        /// <summary>
        /// Called by <see cref="UIBackRouter"/> when the player presses Cancel
        /// (Escape / gamepad B). Return true when this screen consumed the input.
        /// </summary>
        public abstract bool OnBack();

        /// <summary>Called after this screen becomes visible.</summary>
        protected virtual void OnOpened() { }

        /// <summary>Called before this screen is hidden.</summary>
        protected virtual void OnClosed() { }

        /// <summary>Send <see cref="defaultSelectable"/> to the input-mode detector.</summary>
        protected void PushFocus()
        {
            if (defaultSelectable == null) return;
            UIInputModeDetector.Instance?.RequestFocus(defaultSelectable);
        }

        /// <summary>Change the default selectable and push it immediately.</summary>
        protected void SetDefaultSelectable(Selectable selectable)
        {
            defaultSelectable = selectable;
            PushFocus();
        }

        /// <summary>Select a specific selectable immediately (used for Navigation mode).</summary>
        protected void SelectNow(Selectable selectable)
        {
            if (selectable == null) return;

            var eventSystem = EventSystem.current;
            if (eventSystem != null)
                eventSystem.SetSelectedGameObject(selectable.gameObject);

            UIInputModeDetector.Instance?.RequestFocus(selectable);
        }
    }
}
