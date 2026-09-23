using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MHZE.BasicMenus
{
    /// <summary>
    /// Blocking confirmation popup ("Quit to desktop?", "Delete save?"...).
    /// Open it from code and listen to <see cref="onConfirmed"/> /
    /// <see cref="onCancelled"/>. With the default settings it closes itself
    /// after either answer. Its very high <see cref="UIScreen.BackPriority"/>
    /// makes it consume back input before any other screen.
    /// </summary>
    public class ConfirmationScreen : UIScreen
    {
        [Header("Labels")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text messageText;

        [Header("Buttons")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        [Header("Behavior")]
        [SerializeField] private bool closeOnConfirm = true;
        [SerializeField] private bool closeOnCancel = true;

        [Header("Events")]
        [SerializeField] private UnityEvent onConfirmed = new UnityEvent();
        [SerializeField] private UnityEvent onCancelled = new UnityEvent();

        /// <summary>Invoked when the player answers yes.</summary>
        public UnityEvent OnConfirmed => onConfirmed;

        /// <summary>Invoked when the player answers no (or presses back).</summary>
        public UnityEvent OnCancelled => onCancelled;

        private void Start()
        {
            if (confirmButton != null)
                confirmButton.onClick.AddListener(Confirm);

            if (cancelButton != null)
                cancelButton.onClick.AddListener(Cancel);
        }

        /// <summary>Set the texts and show the popup.</summary>
        public void Open(string title, string message)
        {
            SetContent(title, message);
            Open();
        }

        /// <summary>Update the title/message texts without opening the popup.</summary>
        public void SetContent(string title, string message)
        {
            if (titleText != null) titleText.text = title;
            if (messageText != null) messageText.text = message;
        }

        /// <summary>Answer yes.</summary>
        public void Confirm()
        {
            onConfirmed?.Invoke();
            if (closeOnConfirm) Close();
        }

        /// <summary>Answer no.</summary>
        public void Cancel()
        {
            onCancelled?.Invoke();
            if (closeOnCancel) Close();
        }

        public override int BackPriority => 200;

        public override bool OnBack()
        {
            if (!IsOpen) return false;
            Cancel();
            return true;
        }
    }
}
