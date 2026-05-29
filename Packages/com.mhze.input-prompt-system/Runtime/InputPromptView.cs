// A single on-screen prompt row that shows an icon, prefix text, and suffix text. The manager calls SetContent to update what is displayed and SetVisible to show or hide it. Handles null or empty values by disabling the relevant child objects so unused slots do not take up space.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MHZE.InputPromptSystem
{
public class InputPromptView : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text prefixText;
    [SerializeField] private TMP_Text suffixText;

    public void SetContent(Sprite icon, string prefix, string suffix)
    {
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }

        if (prefixText != null)
        {
            prefixText.text = prefix ?? string.Empty;
            prefixText.gameObject.SetActive(!string.IsNullOrWhiteSpace(prefix));
        }

        if (suffixText != null)
        {
            suffixText.text = suffix ?? string.Empty;
            suffixText.gameObject.SetActive(!string.IsNullOrWhiteSpace(suffix));
        }
    }

    public void SetVisible(bool isVisible)
    {
        gameObject.SetActive(isVisible);
    }
}
}