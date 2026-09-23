using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MHZE.BasicMenus
{
    /// <summary>
    /// Exposes navigation and submit/cancel events of a Selectable through the
    /// inspector or C# events. Useful when a button needs to react to arrow-key
    /// navigation (e.g. changing a value with left/right) instead of moving
    /// the selection.
    /// </summary>
    [RequireComponent(typeof(Selectable))]
    public class SelectableCustomNavEvents : MonoBehaviour, IMoveHandler, ISubmitHandler, ICancelHandler
    {
        [Header("Unity Events")]
        public UnityEvent onUp;
        public UnityEvent onDown;
        public UnityEvent onLeft;
        public UnityEvent onRight;
        public UnityEvent onSubmit;
        public UnityEvent onCancel;

        public event Action Up;
        public event Action Down;
        public event Action Left;
        public event Action Right;
        public event Action Submit;
        public event Action Cancel;

        private Selectable _selectable;

        private void Awake()
        {
            _selectable = GetComponent<Selectable>();
        }

        public void OnMove(AxisEventData eventData)
        {
            if (_selectable != null && !_selectable.IsInteractable()) return;

            switch (eventData.moveDir)
            {
                case MoveDirection.Up:
                    onUp?.Invoke();
                    Up?.Invoke();
                    break;
                case MoveDirection.Down:
                    onDown?.Invoke();
                    Down?.Invoke();
                    break;
                case MoveDirection.Left:
                    onLeft?.Invoke();
                    Left?.Invoke();
                    break;
                case MoveDirection.Right:
                    onRight?.Invoke();
                    Right?.Invoke();
                    break;
            }
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (_selectable != null && !_selectable.IsInteractable()) return;

            onSubmit?.Invoke();
            Submit?.Invoke();
        }

        public void OnCancel(BaseEventData eventData)
        {
            if (_selectable != null && !_selectable.IsInteractable()) return;

            onCancel?.Invoke();
            Cancel?.Invoke();
        }
    }
}
