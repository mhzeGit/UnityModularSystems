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
        [Tooltip("Layers an interactable's collider can be on. Other layers never become the current interactable.")]
        [SerializeField] private LayerMask interactableLayer = -1;
        [Tooltip("How far from the camera the player can interact, in metres. About arm's length plus a step: 2 to 3.")]
        [SerializeField] private float maxDistance = 2.5f;
        [Tooltip("A wall or other solid collider between the camera and an interactable stops the interaction. Turn off only for a game where you can reach through things.")]
        [SerializeField] private bool blockedByObstacles = true;
        [Tooltip("Layers that can stand in the way when Blocked By Obstacles is on. Triggers and the player's own colliders (including a held item) never block. Ignore Raycast is off by default.")]
        [SerializeField] private LayerMask obstacleLayers = ~(1 << 2);
        [SerializeField] private bool showDebugRay = true;

        private static readonly IComparer<RaycastHit> NearestFirst = Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

        private readonly RaycastHit[] raycastHitBuffer = new RaycastHit[32];
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
            // One query over every layer that can be interactable or obstruct, so a wall is seen as well as the interactable behind it.
            int mask = blockedByObstacles ? interactableLayer.value | obstacleLayers.value : interactableLayer.value;
            int hitCount = Physics.RaycastNonAlloc(ray, raycastHitBuffer, maxDistance, mask, QueryTriggerInteraction.Collide);

            // The physics query does not return its hits in any order: the nearest hit under the crosshair decides.
            System.Array.Sort(raycastHitBuffer, 0, hitCount, NearestFirst);
            Transform self = InteractorTransform;
            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = raycastHitBuffer[i].collider;
                if (IsPartOfPlayer(hitCollider, self)) continue;

                IInteractable interactable = (interactableLayer.value & (1 << hitCollider.gameObject.layer)) != 0
                    ? hitCollider.GetComponent<IInteractable>()
                    : null;
                if (interactable != null)
                {
                    lastRaycastHit = raycastHitBuffer[i];
                    SetCurrentInteractable(interactable, hitCollider.gameObject);
                    return;
                }

                // A solid, non-interactable collider closer than anything interactable: the player cannot reach past it.
                if (blockedByObstacles && !hitCollider.isTrigger && (obstacleLayers.value & (1 << hitCollider.gameObject.layer)) != 0)
                {
                    lastRaycastHit = raycastHitBuffer[i];
                    ClearCurrentInteractable();
                    return;
                }
            }

            lastRaycastHit = null;
            ClearCurrentInteractable();
        }

        private bool IsPartOfPlayer(Collider hitCollider, Transform self)
        {
            Transform t = hitCollider.transform;
            return (self != null && t.IsChildOf(self)) || (playerCamera != null && t.IsChildOf(playerCamera.transform));
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
