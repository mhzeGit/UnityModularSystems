using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MHZE.BasicMenus
{
    /// <summary>
    /// Detects whether the player is using the mouse (Pointer mode) or
    /// keyboard/gamepad (Navigation mode) and keeps EventSystem selection and the
    /// cursor in sync. Place it on an always-alive GameObject; it persists
    /// across scenes automatically.
    ///
    /// Cursor handling while a menu is open is routed through
    /// <see cref="UICursorStateRouter"/>, so other systems can override it.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class UIInputModeDetector : MonoBehaviour
    {
        [Header("Thresholds")]
        [Tooltip("Squared pixel distance the mouse must move to count as intentional pointer input.")]
        [SerializeField] private float mouseMoveSqrThreshold = 2f;

        [Tooltip("Squared magnitude a gamepad stick must reach to count as navigation input.")]
        [SerializeField] private float stickSqrThreshold = 0.25f;

        [Header("Cursor")]
        [Tooltip("Show the cursor in Pointer mode and hide it in Navigation mode while a menu is open.")]
        [SerializeField] private bool manageCursor = true;

        [Header("Input (optional)")]
        [Tooltip("UI/Navigate action. When empty the project-wide Navigate action is used, with raw device polling as a last resort.")]
        [SerializeField] private InputActionReference navigateAction;

        private static UIInputModeDetector _instance;

        private InputAction _resolvedNavigateAction;
        private Vector2 _lastMousePosition;
        private Selectable _lastNavigationTarget;

        /// <summary>The active detector, if one exists.</summary>
        public static UIInputModeDetector Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindAnyObjectByType<UIInputModeDetector>();
                return _instance;
            }
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
            _resolvedNavigateAction = ResolveNavigateAction();
            if (_resolvedNavigateAction != null)
            {
                _resolvedNavigateAction.performed += OnNavigatePerformed;
                _resolvedNavigateAction.Enable();
            }

            UIInputState.Changed += OnInputModeChanged;
            UIScreen.AnyOpenChanged += OnAnyScreenOpenChanged;

            if (Mouse.current != null)
                _lastMousePosition = Mouse.current.position.ReadValue();

            EvaluateCursorRequest();
        }

        private void OnDisable()
        {
            if (_resolvedNavigateAction != null)
                _resolvedNavigateAction.performed -= OnNavigatePerformed;
            _resolvedNavigateAction = null;

            UIInputState.Changed -= OnInputModeChanged;
            UIScreen.AnyOpenChanged -= OnAnyScreenOpenChanged;

            UICursorStateRouter.ClearRequest(this);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            DetectMouse();
            if (_resolvedNavigateAction == null) DetectNavigationFallback();
            MaintainSelection();
        }

        // ── Detection ────────────────────────────────────────────────────────

        private void DetectMouse()
        {
            if (Mouse.current == null) return;

            Vector2 position = Mouse.current.position.ReadValue();
            if ((position - _lastMousePosition).sqrMagnitude > mouseMoveSqrThreshold)
            {
                _lastMousePosition = position;
                UIInputState.Current = UIInputMode.Pointer;
            }
        }

        private void OnNavigatePerformed(InputAction.CallbackContext context)
        {
            if (context.ReadValue<Vector2>().sqrMagnitude > 0.01f)
                UIInputState.Current = UIInputMode.Navigation;
        }

        private void DetectNavigationFallback()
        {
            if (Gamepad.current != null)
            {
                Vector2 stick = Gamepad.current.leftStick.ReadValue();
                Vector2 dpad = Gamepad.current.dpad.ReadValue();
                if (stick.sqrMagnitude > stickSqrThreshold || dpad.sqrMagnitude > stickSqrThreshold)
                {
                    UIInputState.Current = UIInputMode.Navigation;
                    return;
                }
            }

            if (Keyboard.current != null)
            {
                if (Keyboard.current.upArrowKey.wasPressedThisFrame ||
                    Keyboard.current.downArrowKey.wasPressedThisFrame ||
                    Keyboard.current.leftArrowKey.wasPressedThisFrame ||
                    Keyboard.current.rightArrowKey.wasPressedThisFrame ||
                    Keyboard.current.tabKey.wasPressedThisFrame)
                {
                    UIInputState.Current = UIInputMode.Navigation;
                }
            }
        }

        private InputAction ResolveNavigateAction()
        {
            if (navigateAction != null && navigateAction.action != null)
                return navigateAction.action;

            var projectWideActions = UnityEngine.InputSystem.InputSystem.actions;
            return projectWideActions != null ? projectWideActions.FindAction("UI/Navigate") : null;
        }

        // ── Mode transitions ─────────────────────────────────────────────────

        private void OnInputModeChanged(UIInputMode mode)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                if (mode == UIInputMode.Pointer)
                {
                    // Clear the selection so only mouse hover highlights buttons.
                    eventSystem.SetSelectedGameObject(null);
                }
                else if (_lastNavigationTarget != null && _lastNavigationTarget.gameObject.activeInHierarchy)
                {
                    eventSystem.SetSelectedGameObject(_lastNavigationTarget.gameObject);
                }
            }

            EvaluateCursorRequest();
        }

        private void OnAnyScreenOpenChanged(bool anyOpen)
        {
            EvaluateCursorRequest();
        }

        private void EvaluateCursorRequest()
        {
            if (!manageCursor || !UIScreen.AnyOpen)
            {
                UICursorStateRouter.ClearRequest(this);
                return;
            }

            if (UIInputState.Current == UIInputMode.Pointer)
            {
                UICursorStateRouter.SetRequest(this, true, CursorLockMode.None, UICursorStateRouter.PriorityUIInputMode);
            }
            else
            {
                UICursorStateRouter.SetRequest(this, false, CursorLockMode.Locked, UICursorStateRouter.PriorityUIInputMode);
            }
        }

        // ── Focus helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Remember which selectable should be highlighted the next time the
        /// player uses keyboard/gamepad (called automatically by <see cref="UIScreen"/>).
        /// </summary>
        public void RequestFocus(Selectable target)
        {
            if (target == null) return;

            _lastNavigationTarget = target;

            if (UIInputState.Current == UIInputMode.Navigation && target.gameObject.activeInHierarchy)
            {
                var eventSystem = EventSystem.current;
                if (eventSystem != null)
                    eventSystem.SetSelectedGameObject(target.gameObject);
            }
        }

        /// <summary>
        /// Restore selection if it is lost (e.g. the selected button got disabled)
        /// and remember whatever the player navigates to.
        /// </summary>
        private void MaintainSelection()
        {
            if (UIInputState.Current != UIInputMode.Navigation) return;

            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;

            var selected = eventSystem.currentSelectedGameObject;

            if (selected == null || !selected.activeInHierarchy)
            {
                if (_lastNavigationTarget != null && _lastNavigationTarget.gameObject.activeInHierarchy)
                    eventSystem.SetSelectedGameObject(_lastNavigationTarget.gameObject);
            }
            else
            {
                var selectable = selected.GetComponent<Selectable>();
                if (selectable != null) _lastNavigationTarget = selectable;
            }
        }
    }
}
