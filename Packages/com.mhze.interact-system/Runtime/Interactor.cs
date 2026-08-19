using UnityEngine;

namespace MHZE.InteractSystem
{
    public class Interactor : MonoBehaviour, IInteractor
    {
        [SerializeField] private Transform interactorTransform;
        [SerializeField] private Camera interactionCamera;
        [SerializeField] private string interactionBindingDisplayString = string.Empty;

        public Camera PlayerCamera => interactionCamera;
        public Transform InteractorTransform => interactorTransform != null ? interactorTransform : transform;
        public string InteractionBindingDisplayString => interactionBindingDisplayString;

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
    }
}