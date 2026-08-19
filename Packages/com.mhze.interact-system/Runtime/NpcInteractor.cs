using UnityEngine;

namespace MHZE.InteractSystem
{
    public class NpcInteractor : InteractSystem
    {
        [SerializeField] private Transform interactorTransform;
        [SerializeField] private Transform interactionOrigin;
        [SerializeField] private Vector3 localDirection = Vector3.forward;
        [SerializeField] private float maxDistance = 2f;
        [SerializeField] private LayerMask interactableLayer = -1;

        private readonly RaycastHit[] hitBuffer = new RaycastHit[8];

        public override Camera PlayerCamera => null;
        public override Transform InteractorTransform => interactorTransform != null ? interactorTransform : transform;
        public override string InteractionBindingDisplayString => string.Empty;

        public bool RequestInteract()
        {
            Transform origin = interactionOrigin != null ? interactionOrigin : InteractorTransform;
            Vector3 direction = origin.TransformDirection(localDirection.normalized);
            return RequestInteract(origin.position, direction);
        }

        public bool RequestInteract(Transform origin, Vector3 direction)
        {
            if (origin == null) return false;
            return RequestInteract(origin.position, direction);
        }

        public bool RequestInteract(Vector3 origin, Vector3 direction)
        {
            int hitCount = Physics.RaycastNonAlloc(origin, direction.normalized, hitBuffer, maxDistance, interactableLayer);
            for (int i = 0; i < hitCount; i++)
            {
                IInteractable interactable = hitBuffer[i].collider.GetComponent<IInteractable>();
                if (interactable == null) continue;
                SetCurrentInteractable(interactable, hitBuffer[i].collider.gameObject);
                bool interacted = TryInteract(interactable);
                ClearCurrentInteractable();
                return interacted;
            }

            ClearCurrentInteractable();
            return false;
        }
    }
}
