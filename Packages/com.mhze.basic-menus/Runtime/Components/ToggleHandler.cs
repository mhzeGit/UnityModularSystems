using UnityEngine;
using UnityEngine.UI;

namespace MHZE.BasicMenus
{
    /// <summary>
    /// Small helper for wiring buttons to a toggle through inspector onClick
    /// events. Add it next to the button and assign the target toggle.
    /// </summary>
    public class ToggleHandler : MonoBehaviour
    {
        [SerializeField] private Toggle targetToggle;

        /// <summary>Invert the toggle value.</summary>
        public void ToggleValue()
        {
            if (targetToggle == null) return;
            targetToggle.isOn = !targetToggle.isOn;
        }

        /// <summary>Set the toggle value.</summary>
        public void SetToggle(bool value)
        {
            if (targetToggle == null) return;
            targetToggle.isOn = value;
        }
    }
}
