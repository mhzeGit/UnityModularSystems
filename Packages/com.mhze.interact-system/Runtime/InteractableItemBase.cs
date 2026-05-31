// Default interactable object behaviour. Stores whether it is interactable, whether it shows a prompt, the hold time, and optional prefix/suffix overrides for the input prompt. Fires C# events and a UnityEvent when interacted with.

using System;
using UnityEngine;
using ArgEvent;

namespace MHZE.InteractSystem
{
    public class InteractableItemBase : MonoBehaviour, IInteractable
    {
        [SerializeField] private bool isInteractable = true;
        [SerializeField] private bool allowPrompt = true;
        [SerializeField] private bool oneTimeInteract = false;
        [SerializeField] private float holdTime = 0f;
        [SerializeField] private string promptPrefix = string.Empty;
        [SerializeField] private string promptSuffix = string.Empty;

        private bool interactedOnce;

        public event Action OnInteractableUpdated;

        public event Action<IInteractor> Interacted;
        public event Action<IInteractor> InteractReleased;
        public ArgEventBinding<IInteractor> OnInteractedWithEvent = new ArgEventBinding<IInteractor>();

        public bool IsInteractable => isInteractable;
        public bool AllowPrompt => allowPrompt;
        public bool OneTimeInteract => oneTimeInteract;
        public bool InteractedOnce => interactedOnce;

        public void SetIsInteractable(bool value)
        {
            if (isInteractable == value) return;
            isInteractable = value;
            OnInteractableUpdated?.Invoke();
        }

        public void SetAllowPrompt(bool value)
        {
            if (allowPrompt == value) return;
            allowPrompt = value;
            OnInteractableUpdated?.Invoke();
        }

        public void SetInteractedOnce(bool value)
        {
            interactedOnce = value;
        }

        public float HoldTime => holdTime;

        public string PromptPrefix => promptPrefix;
        public string PromptSuffix => promptSuffix;

        public void SetPromptPrefix(string value)
        {
            if (promptPrefix == value) return;
            promptPrefix = value;
            OnInteractableUpdated?.Invoke();
        }

        public void SetPromptSuffix(string value)
        {
            if (promptSuffix == value) return;
            promptSuffix = value;
            OnInteractableUpdated?.Invoke();
        }

        public virtual void OnInteract(IInteractor interactor)
        {
            Interacted?.Invoke(interactor);
            OnInteractedWithEvent.Invoke(interactor);
        }

        public virtual void OnInteractReleased(IInteractor interactor)
        {
            InteractReleased?.Invoke(interactor);
        }

        public virtual void OnHoverEnter(IInteractor interactor) { }

        public virtual void OnHoverExit(IInteractor interactor) { }
    }
}
