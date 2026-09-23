using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MHZE.BasicMenus
{
    /// <summary>
    /// Listens for the Cancel input action (Escape / gamepad B) and routes it to
    /// the active screen with the highest <see cref="UIScreen.BackPriority"/> that
    /// consumes it. Screens register themselves automatically in
    /// OnEnable/OnDisable, so there is no manual wiring.
    ///
    /// Place it on an always-alive GameObject together with
    /// <see cref="UIInputModeDetector"/>. If no Cancel action is assigned, the
    /// project-wide UI/Cancel action is used when available.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class UIBackRouter : MonoBehaviour
    {
        [Header("Input (optional)")]
        [Tooltip("UI/Cancel action. When empty, the project-wide UI/Cancel action is used.")]
        [SerializeField] private InputActionReference cancelAction;

        private static readonly List<UIScreen> _screens = new List<UIScreen>();
        private static readonly List<Func<bool>> _modalBackHandlers = new List<Func<bool>>();
        private static bool _dirty;
        private static int _suppressedFrame = -1;
        private static int _handledFrame = -1;
        private static UIBackRouter _instance;

        private InputAction _resolvedCancelAction;

        /// <summary>
        /// True when a back input was consumed during the current frame. Screens
        /// that also listen for the same key (e.g. the pause toggle using
        /// UI/Cancel as a fallback) can use this to avoid acting twice on one press.
        /// </summary>
        public static bool BackHandledThisFrame => _handledFrame == Time.frameCount;

        /// <summary>
        /// Ignores back input for the rest of the current frame. Useful right
        /// after opening a screen from the same key that is also Cancel.
        /// </summary>
        public static void SuppressBackThisFrame()
        {
            _suppressedFrame = Time.frameCount;
        }

        /// <summary>Register a screen. Called automatically by <see cref="UIScreen"/>.</summary>
        public static void Register(UIScreen screen)
        {
            if (screen == null || _screens.Contains(screen)) return;
            _screens.Add(screen);
            _dirty = true;
        }

        /// <summary>Unregister a screen. Called automatically by <see cref="UIScreen"/>.</summary>
        public static void Unregister(UIScreen screen)
        {
            if (screen == null) return;
            _screens.Remove(screen);
        }

        /// <summary>
        /// Register a modal handler that gets the first chance to consume back
        /// input (e.g. an active rebind operation). Return true to consume.
        /// </summary>
        public static void RegisterModalBackHandler(Func<bool> handler)
        {
            if (handler == null || _modalBackHandlers.Contains(handler)) return;
            _modalBackHandlers.Add(handler);
        }

        /// <summary>Remove a previously registered modal handler.</summary>
        public static void UnregisterModalBackHandler(Func<bool> handler)
        {
            if (handler == null) return;
            _modalBackHandlers.Remove(handler);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            _resolvedCancelAction = ResolveCancelAction();
            if (_resolvedCancelAction != null)
            {
                _resolvedCancelAction.performed += OnCancelPerformed;
                _resolvedCancelAction.Enable();
            }
        }

        private void OnDisable()
        {
            if (_resolvedCancelAction != null)
                _resolvedCancelAction.performed -= OnCancelPerformed;

            _resolvedCancelAction = null;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private InputAction ResolveCancelAction()
        {
            if (cancelAction != null && cancelAction.action != null)
                return cancelAction.action;

            var projectWideActions = UnityEngine.InputSystem.InputSystem.actions;
            return projectWideActions != null ? projectWideActions.FindAction("UI/Cancel") : null;
        }

        private void OnCancelPerformed(InputAction.CallbackContext context)
        {
            HandleBack();
        }

        /// <summary>Route a back input. Also callable from a "Back" button's onClick.</summary>
        public void HandleBack()
        {
            if (_suppressedFrame == Time.frameCount) return;

            for (int i = _modalBackHandlers.Count - 1; i >= 0; i--)
            {
                var handler = _modalBackHandlers[i];
                if (handler != null && handler())
                {
                    _handledFrame = Time.frameCount;
                    return;
                }
            }

            if (_dirty)
            {
                _screens.Sort((a, b) => b.BackPriority.CompareTo(a.BackPriority));
                _dirty = false;
            }

            for (int i = 0; i < _screens.Count; i++)
            {
                var screen = _screens[i];
                if (screen != null && screen.isActiveAndEnabled && screen.OnBack())
                {
                    _handledFrame = Time.frameCount;
                    return;
                }
            }
        }
    }
}
