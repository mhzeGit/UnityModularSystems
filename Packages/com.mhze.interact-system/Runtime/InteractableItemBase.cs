// Default interactable object behaviour. Stores whether it is interactable, whether it shows a prompt, the hold time, and optional prefix/suffix overrides for the input prompt. Fires C# events and a UnityEvent when interacted with.

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
        [SerializeField] private string promptPrefix = string.Empty;
        [SerializeField] private string promptSuffix = string.Empty;

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

        public string PromptPrefix => promptPrefix;
        public string PromptSuffix => promptSuffix;

        public virtual void OnInteract(IInteractor interactor)
        {
            Interacted?.Invoke(interactor);
            OnInteractedWithEvent?.Invoke();
        }

        public virtual void OnInteractReleased(IInteractor interactor)
        {
            InteractReleased?.Invoke(interactor);
        }

        public virtual void OnHoverEnter(IInteractor interactor) { }

        public virtual void OnHoverExit(IInteractor interactor) { }
    }
}
