using System;
using UnityEngine;

namespace MHZE.InteractSystem
{
    public abstract class InteractSystem : MonoBehaviour, IInteractor
    {
        public IInteractable CurrentInteractable { get; private set; }
        public GameObject CurrentInteractableObject { get; private set; }
        public abstract Camera PlayerCamera { get; }
        public abstract Transform InteractorTransform { get; }
        public abstract string InteractionBindingDisplayString { get; }

        public event Action<IInteractable, IInteractor> OnInteractableFound;
        public event Action<GameObject, IInteractor> OnPerformedInteraction;
        public event Action<IInteractable, IInteractor> OnInteractableLost;
        public event Action<float> OnHoldAttemptStarted;
        public event Action OnHoldAttemptEnded;
        public event Action OnCurrentInteractableUpdated;

        public bool TryInteract(IInteractable interactable)
        {
            if (interactable == null || !interactable.IsInteractable)
                return false;
            if (interactable.OneTimeInteract && interactable.InteractedOnce)
                return false;

            interactable.OnInteract(this);
            return true;
        }

        public void ReleaseInteract(IInteractable interactable)
        {
            if (interactable != null)
                interactable.OnInteractReleased(this);
        }

        protected void SetCurrentInteractable(IInteractable interactable, GameObject obj)
        {
            if (interactable == CurrentInteractable && obj == CurrentInteractableObject)
                return;

            UnsubscribeCurrent();
            if (CurrentInteractable != null)
                CurrentInteractable.OnHoverExit(this);

            CurrentInteractable = interactable;
            CurrentInteractableObject = obj;

            if (CurrentInteractable != null)
            {
                CurrentInteractable.OnInteractableUpdated += HandleInteractableUpdated;
                CurrentInteractable.OnHoverEnter(this);
            }

            OnInteractableFound?.Invoke(CurrentInteractable, this);
            OnCurrentInteractableUpdated?.Invoke();
        }

        public void ClearCurrentInteractable()
        {
            if (CurrentInteractable != null)
            {
                if (CurrentInteractable.OneTimeInteract)
                    CurrentInteractable.SetInteractedOnce(false);
                CurrentInteractable.OnHoverExit(this);
                UnsubscribeCurrent();
                OnInteractableLost?.Invoke(CurrentInteractable, this);
            }

            CurrentInteractable = null;
            CurrentInteractableObject = null;
            OnCurrentInteractableUpdated?.Invoke();
        }

        protected void RaisePerformedInteraction(GameObject obj) => OnPerformedInteraction?.Invoke(obj, this);
        protected void RaiseHoldStarted(float holdTime) => OnHoldAttemptStarted?.Invoke(holdTime);
        protected void RaiseHoldEnded() => OnHoldAttemptEnded?.Invoke();

        private void UnsubscribeCurrent()
        {
            if (CurrentInteractable != null)
                CurrentInteractable.OnInteractableUpdated -= HandleInteractableUpdated;
        }

        private void HandleInteractableUpdated() => OnCurrentInteractableUpdated?.Invoke();
    }
}
