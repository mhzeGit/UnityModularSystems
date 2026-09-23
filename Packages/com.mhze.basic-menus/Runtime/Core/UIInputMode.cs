using System;

namespace MHZE.BasicMenus
{
    /// <summary>Which input device category the player is currently using.</summary>
    public enum UIInputMode
    {
        /// <summary>Mouse or touch. Buttons highlight on hover; EventSystem selection is cleared.</summary>
        Pointer,

        /// <summary>Keyboard or gamepad. Buttons highlight through EventSystem selection; the cursor is hidden.</summary>
        Navigation
    }

    /// <summary>
    /// Static, globally readable input-mode state. Written by <see cref="UIInputModeDetector"/>,
    /// read by any UI or gameplay script without needing a reference.
    /// </summary>
    public static class UIInputState
    {
        /// <summary>Invoked whenever the active input mode changes.</summary>
        public static event Action<UIInputMode> Changed;

        private static UIInputMode _current = UIInputMode.Pointer;

        /// <summary>The current input mode. Assigning the same value does not fire <see cref="Changed"/>.</summary>
        public static UIInputMode Current
        {
            get => _current;
            set
            {
                if (_current == value) return;
                _current = value;
                Changed?.Invoke(_current);
            }
        }
    }
}
