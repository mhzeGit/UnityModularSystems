using UnityEngine;
using UnityEngine.UI;

namespace MHZE.BasicMenus
{
    /// <summary>
    /// Simple closeable info panel (credits, version, controls overview...).
    /// Pressing back (Escape / gamepad B) or clicking the assigned back button
    /// closes it. Author the panel content freely; the component only handles
    /// open/close and back routing.
    /// </summary>
    public class InfoScreen : UIScreen
    {
        [Header("Back Button")]
        [Tooltip("Optional button that closes this screen.")]
        [SerializeField] private Button backButton;

        private void Start()
        {
            if (backButton != null)
                backButton.onClick.AddListener(Close);
        }

        public override int BackPriority => 60;

        public override bool OnBack()
        {
            if (!IsOpen) return false;
            Close();
            return true;
        }
    }
}
