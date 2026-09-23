using UnityEngine;
using UnityEngine.UI;

namespace MHZE.BasicMenus
{
    /// <summary>
    /// Small helper for wiring increase/decrease buttons to a slider through
    /// inspector onClick events. Add it next to the button and assign the
    /// target slider.
    /// </summary>
    public class SliderHandler : MonoBehaviour
    {
        [SerializeField] private Slider targetSlider;

        /// <summary>Add <paramref name="delta"/> to the slider value, clamped to its range.</summary>
        public void AddValue(float delta)
        {
            if (targetSlider == null) return;
            targetSlider.value = Mathf.Clamp(targetSlider.value + delta, targetSlider.minValue, targetSlider.maxValue);
        }

        /// <summary>Set the slider value, clamped to its range.</summary>
        public void SetValue(float value)
        {
            if (targetSlider == null) return;
            targetSlider.value = Mathf.Clamp(value, targetSlider.minValue, targetSlider.maxValue);
        }
    }
}
