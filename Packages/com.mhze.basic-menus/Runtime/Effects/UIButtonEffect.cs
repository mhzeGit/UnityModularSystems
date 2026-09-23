using UnityEngine;

namespace MHZE.BasicMenus
{
    /// <summary>
    /// Base class for button visual effect modules. Subclass it and add the
    /// component to a GameObject with (or under) a <see cref="UIButtonEffects"/>
    /// component to react to button state changes.
    /// </summary>
    public abstract class UIButtonEffect : MonoBehaviour
    {
        /// <summary>The owning <see cref="UIButtonEffects"/> component.</summary>
        protected UIButtonEffects Owner { get; private set; }

        /// <summary>Called by <see cref="UIButtonEffects"/> on Awake.</summary>
        public void Initialize(UIButtonEffects owner)
        {
            Owner = owner;
        }

        /// <summary>Called whenever the button state changes.</summary>
        public abstract void OnStateChanged(UIButtonState state);

        /// <summary>Called every frame with unscaled delta time. Use for animations.</summary>
        public virtual void Tick(float deltaTime) { }
    }
}
