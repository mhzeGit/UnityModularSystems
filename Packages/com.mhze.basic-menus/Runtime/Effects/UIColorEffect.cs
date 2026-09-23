using System;
using UnityEngine;
using UnityEngine.UI;

namespace MHZE.BasicMenus
{
    /// <summary>
    /// Animates the color of a Graphic based on the button state.
    /// Leave the target empty to use the Graphic on the same GameObject.
    /// </summary>
    public class UIColorEffect : UIButtonEffect
    {
        [Serializable]
        public struct ColorConfig
        {
            public Color color;
            public float speed;
        }

        [Header("Target (optional)")]
        [SerializeField] private Graphic target;

        [Header("Per-state color")]
        [SerializeField] private ColorConfig normal   = new ColorConfig { color = Color.white,                  speed = 8f };
        [SerializeField] private ColorConfig hovered  = new ColorConfig { color = new Color(0.9f, 0.9f, 1f),    speed = 10f };
        [SerializeField] private ColorConfig selected = new ColorConfig { color = new Color(0.8f, 0.8f, 1f),    speed = 10f };
        [SerializeField] private ColorConfig pressed  = new ColorConfig { color = new Color(0.7f, 0.7f, 0.9f),  speed = 15f };

        private Color _targetColor;
        private Color _currentColor;

        private void Awake()
        {
            if (target == null) target = GetComponent<Graphic>();
            _targetColor = normal.color;
            _currentColor = _targetColor;
        }

        public override void OnStateChanged(UIButtonState state)
        {
            _targetColor = GetConfig(state).color;
        }

        public override void Tick(float deltaTime)
        {
            if (target == null) return;
            float speed = GetConfig(Owner != null ? Owner.CurrentState : UIButtonState.Normal).speed;
            _currentColor = Color.Lerp(_currentColor, _targetColor, deltaTime * speed);
            target.color = _currentColor;
        }

        private ColorConfig GetConfig(UIButtonState state)
        {
            switch (state)
            {
                case UIButtonState.Hovered: return hovered;
                case UIButtonState.Selected: return selected;
                case UIButtonState.Pressed: return pressed;
                default: return normal;
            }
        }
    }
}
