using UnityEngine;
using UnityEngine.EventSystems;

namespace MHZE.BasicMenus
{
    /// <summary>Visual states a managed button can be in.</summary>
    public enum UIButtonState { Normal, Hovered, Selected, Pressed, Disabled }

    /// <summary>
    /// Tracks pointer/keyboard/gamepad state for a Selectable and forwards it to
    /// any <see cref="UIButtonEffect"/> modules on the same GameObject or its
    /// children (scale, color or custom effects).
    ///
    /// Pointer events only apply in <see cref="UIInputMode.Pointer"/> mode and
    /// selection only in <see cref="UIInputMode.Navigation"/> mode, so buttons
    /// never show two highlights at once. Add this component next to a Button
    /// (or Toggle, Slider...) and add one or more effect modules.
    /// </summary>
    [RequireComponent(typeof(UnityEngine.UI.Selectable))]
    public class UIButtonEffects : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler,
        ISubmitHandler
    {
        private UIButtonEffect[] _effects;
        private UnityEngine.UI.Selectable _selectable;
        private UIButtonState _state = UIButtonState.Normal;
        private bool _pointerInside;

        /// <summary>The current visual state of the button.</summary>
        public UIButtonState CurrentState => _state;

        private void Awake()
        {
            _selectable = GetComponent<UnityEngine.UI.Selectable>();
            _effects = GetComponentsInChildren<UIButtonEffect>(true);
            foreach (var effect in _effects)
                effect.Initialize(this);
        }

        private void OnEnable()
        {
            UIInputState.Changed += OnInputModeChanged;
            SetState(UIButtonState.Normal, true);
        }

        private void OnDisable()
        {
            UIInputState.Changed -= OnInputModeChanged;
        }

        private void Update()
        {
            if (!_selectable.interactable && _state != UIButtonState.Disabled)
                SetState(UIButtonState.Disabled);

            float deltaTime = Time.unscaledDeltaTime;
            foreach (var effect in _effects)
                effect.Tick(deltaTime);
        }

        // ── Pointer events ───────────────────────────────────────────────────

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (UIInputState.Current != UIInputMode.Pointer) return;
            if (!_selectable.interactable) return;

            _pointerInside = true;
            SetState(UIButtonState.Hovered);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _pointerInside = false;
            if (_state == UIButtonState.Hovered)
                SetState(UIButtonState.Normal);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_selectable.interactable) return;
            SetState(UIButtonState.Pressed);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_selectable.interactable) return;
            SetState(_pointerInside ? UIButtonState.Hovered : UIButtonState.Normal);
        }

        // ── Selection events ─────────────────────────────────────────────────

        public void OnSelect(BaseEventData eventData)
        {
            if (!_selectable.interactable) return;
            SetState(UIButtonState.Selected);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            CancelInvoke(nameof(ReturnToSelected));
            if (_state != UIButtonState.Disabled)
                SetState(UIButtonState.Normal);
        }

        // ── Submit ───────────────────────────────────────────────────────────

        public void OnSubmit(BaseEventData eventData)
        {
            if (!_selectable.interactable) return;

            SetState(UIButtonState.Pressed);
            Invoke(nameof(ReturnToSelected), 0.1f);
        }

        private void ReturnToSelected()
        {
            if (_state != UIButtonState.Pressed) return;

            var eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.currentSelectedGameObject == gameObject)
                SetState(UIButtonState.Selected);
            else
                SetState(UIButtonState.Normal);
        }

        // ── Mode switching ───────────────────────────────────────────────────

        private void OnInputModeChanged(UIInputMode mode)
        {
            _pointerInside = false;
            SetState(UIButtonState.Normal, true);
        }

        private void SetState(UIButtonState newState, bool force = false)
        {
            if (!force && _state == newState) return;

            _state = newState;
            foreach (var effect in _effects)
                effect.OnStateChanged(newState);
        }
    }
}
