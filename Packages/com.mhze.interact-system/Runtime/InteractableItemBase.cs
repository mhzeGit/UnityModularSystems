using System;
using UnityEngine;
using UnityEngine.Events;

namespace MHZE.InteractSystem
{
    public class InteractableItemBase : MonoBehaviour, IInteractable
    {
        [SerializeField] private bool isInteractable = true;
        [SerializeField] private bool allowPrompt = true;
        [SerializeField] private float holdTime = 0f;
        [SerializeField] private string promptFormat = "Press {KEY} To Interact";

        private IInteractor currentInteractor;

        public event Action OnInteractableUpdated;

        public event Action<IInteractor> Interacted;
        public event Action<IInteractor> InteractReleased;
        public UnityEvent OnInteractedWithEvent;

        public bool IsInteractable
        {
            get => isInteractable;
            set
            {
                if (isInteractable == value) return;
                isInteractable = value;
                OnInteractableUpdated?.Invoke();
            }
        }

        public bool AllowPrompt
        {
            get => allowPrompt;
            set
            {
                if (allowPrompt == value) return;
                allowPrompt = value;
                OnInteractableUpdated?.Invoke();
            }
        }

        public float HoldTime => holdTime;

        public string PromptText
        {
            get
            {
                if (!allowPrompt) return string.Empty;
                string binding = currentInteractor?.InteractionBindingDisplayString ?? "KEY";
                return promptFormat.Replace("{KEY}", binding);
            }
            set
            {
                if (promptFormat == value) return;
                promptFormat = value;
                OnInteractableUpdated?.Invoke();
            }
        }

        public virtual void OnInteract(IInteractor interactor)
        {
            Interacted?.Invoke(interactor);
            OnInteractedWithEvent?.Invoke();
        }

        public virtual void OnInteractReleased(IInteractor interactor)
        {
            InteractReleased?.Invoke(interactor);
        }

        public virtual void OnHoverEnter(IInteractor interactor)
        {
            currentInteractor = interactor;
        }

        public virtual void OnHoverExit(IInteractor interactor)
        {
            if (currentInteractor == interactor)
                currentInteractor = null;
        }
    }
}
