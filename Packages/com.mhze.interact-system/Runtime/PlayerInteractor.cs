using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MHZE.InteractSystem
{
    public class PlayerInteractor : InteractSystem
    {
        [SerializeField] private InputActionReference interactInputAction;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private LayerMask interactableLayer = -1;
        [SerializeField] private float maxDistance = 5f;
        [SerializeField] private bool showDebugRay = true;

        private static readonly IComparer<RaycastHit> NearestFirst = Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

        private readonly RaycastHit[] raycastHitBuffer = new RaycastHit[8];
        private RaycastHit? lastRaycastHit;
        private bool isHolding;
        private float holdTimer;
        private IInteractable holdTarget;

        public override Camera PlayerCamera => playerCamera;
        public override Transform InteractorTransform => playerTransform != null
            ? playerTransform
            : playerCamera != null ? playerCamera.transform.root : transform;
        public override string InteractionBindingDisplayString => interactInputAction == null
            ? "E"
            : interactInputAction.action.GetBindingDisplayString(0);

        private void Awake()
        {
            if (playerCamera == null)
                playerCamera = Camera.main;
            if (playerTransform == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                    playerTransform = player.transform;
            }
        }

        private void OnEnable()
        {
            if (interactInputAction == null) return;
            interactInputAction.action.started += HandleInteractStarted;
            interactInputAction.action.canceled += HandleInteractCanceled;
            interactInputAction.action.Enable();
        }

        private void OnDisable()
        {
            CancelHold();
            if (interactInputAction == null) return;
            interactInputAction.action.started -= HandleInteractStarted;
            interactInputAction.action.canceled -= HandleInteractCanceled;
            interactInputAction.action.Disable();
        }

        private void Update()
        {
            PerformRaycast();
            DrawDebugRay();

            if (!isHolding) return;
            if (holdTarget != CurrentInteractable || holdTarget == null ||
                (holdTarget is Object targetObject && !targetObject) || !holdTarget.IsInteractable)
            {
                CancelHold();
                return;
            }
            holdTimer += Time.deltaTime;
            if (holdTimer < CurrentInteractable.HoldTime) return;

            TryInteract(CurrentInteractable);
            RaisePerformedInteraction(CurrentInteractableObject);
            RaiseHoldEnded();
            isHolding = false;
            holdTimer = 0f;
            holdTarget = null;
        }

        private void HandleInteractStarted(InputAction.CallbackContext context)
        {
            if (CurrentInteractable == null || !CurrentInteractable.IsInteractable) return;
            if (CurrentInteractable.OneTimeInteract && CurrentInteractable.InteractedOnce) return;

            if (CurrentInteractable.HoldTime > 0f)
            {
                isHolding = true;
                holdTimer = 0f;
                holdTarget = CurrentInteractable;
                RaiseHoldStarted(CurrentInteractable.HoldTime);
                return;
            }

            TryInteract(CurrentInteractable);
            RaisePerformedInteraction(CurrentInteractableObject);
        }

        private void HandleInteractCanceled(InputAction.CallbackContext context)
        {
            ReleaseInteract(isHolding ? holdTarget : CurrentInteractable);
            if (isHolding) RaiseHoldEnded();
            isHolding = false;
            holdTimer = 0f;
            holdTarget = null;
        }

        private void CancelHold()
        {
            if (!isHolding) return;
            var target = holdTarget;
            isHolding = false; holdTimer = 0f; holdTarget = null;
            if (target != null && !(target is Object targetObject && !targetObject)) ReleaseInteract(target);
            RaiseHoldEnded();
        }

        private void PerformRaycast()
        {
            if (playerCamera == null)
            {
                ClearCurrentInteractable();
                return;
            }

            Ray ray = playerCamera.ViewportPointToRay(Vector3.one * 0.5f);
            int hitCount = Physics.RaycastNonAlloc(ray, raycastHitBuffer, maxDistance, interactableLayer);

            // The physics query does not return its hits in any order: the nearest interactable under the crosshair is the one to use.
            System.Array.Sort(raycastHitBuffer, 0, hitCount, NearestFirst);
            for (int i = 0; i < hitCount; i++)
            {
                IInteractable interactable = raycastHitBuffer[i].collider.GetComponent<IInteractable>();
                if (interactable == null) continue;
                lastRaycastHit = raycastHitBuffer[i];
                SetCurrentInteractable(interactable, raycastHitBuffer[i].collider.gameObject);
                return;
            }

            lastRaycastHit = null;
            ClearCurrentInteractable();
        }

        private void DrawDebugRay()
        {
            if (!showDebugRay || playerCamera == null) return;
            Ray ray = playerCamera.ViewportPointToRay(Vector3.one * 0.5f);
            Vector3 end = lastRaycastHit.HasValue ? lastRaycastHit.Value.point : ray.origin + ray.direction * maxDistance;
            Debug.DrawLine(ray.origin, end, CurrentInteractable != null ? Color.green : Color.red);
        }
    }
}
