using System;
using UnityEngine;

namespace MHZE.BasicMenus
{
    /// <summary>
    /// Animates the scale of a target transform based on the button state.
    /// Leave the target empty to scale the button itself.
    /// </summary>
    public class UIScaleEffect : UIButtonEffect
    {
        [Serializable]
        public struct ScaleConfig
        {
            public Vector3 scale;
            public float speed;
        }

        [Header("Target (optional, defaults to this transform)")]
        [SerializeField] private Transform target;

        [Header("Per-state scale")]
        [SerializeField] private ScaleConfig normal   = new ScaleConfig { scale = Vector3.one,           speed = 10f };
        [SerializeField] private ScaleConfig hovered  = new ScaleConfig { scale = Vector3.one * 1.05f,   speed = 12f };
        [SerializeField] private ScaleConfig selected = new ScaleConfig { scale = Vector3.one * 1.08f,   speed = 12f };
        [SerializeField] private ScaleConfig pressed  = new ScaleConfig { scale = Vector3.one * 0.95f,   speed = 20f };

        private Vector3 _targetScale;

        private void Awake()
        {
            if (target == null) target = transform;
            _targetScale = normal.scale;
        }

        public override void OnStateChanged(UIButtonState state)
        {
            _targetScale = GetConfig(state).scale;
        }

        public override void Tick(float deltaTime)
        {
            if (target == null) return;
            target.localScale = Vector3.Lerp(target.localScale, _targetScale, deltaTime * GetConfig(Owner != null ? Owner.CurrentState : UIButtonState.Normal).speed);
        }

        private ScaleConfig GetConfig(UIButtonState state)
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
