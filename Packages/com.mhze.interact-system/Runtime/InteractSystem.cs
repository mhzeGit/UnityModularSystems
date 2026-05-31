// Main interaction system that raycasts from the camera center to find IInteractable objects. Tracks the current interactable, fires events when something is found or lost, and supports both instant interactions and hold-to-interact (with a timer). Exposes the current binding display string for prompt UI. Debug ray visualization included.

using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MHZE.InteractSystem
{
    public class InteractSystem : MonoBehaviour, IInteractor
    {
        [Header("Inputs")]
        [SerializeField] private InputActionReference interactInputAction;

        [Header("Raycast Settings")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private LayerMask interactableLayer = -1;
        [SerializeField] private float maxDistance = 5f;

        [Header("Debug")]
        [SerializeField] private bool showDebugRay = true;

        public IInteractable CurrentInteractable { get; private set; }
        public GameObject CurrentInteractableObject { get; private set; }
        Camera IInteractor.PlayerCamera => playerCamera;

        public string InteractionBindingDisplayString
        {
            get
            {
                if (interactInputAction == null) return "E";
                return interactInputAction.action.GetBindingDisplayString(0);
            }
        }

        private RaycastHit? lastRaycastHit;
        private RaycastHit[] raycastHitBuffer = new RaycastHit[8];

        public event Action<IInteractable, IInteractor> OnInteractableFound;
        public event Action<GameObject, IInteractor> OnPerformedInteraction;
        public event Action<IInteractable, IInteractor> OnInteractableLost;

        public event Action<float> OnHoldAttemptStarted;
        public event Action OnHoldAttemptEnded;

        public event Action OnCurrentInteractableUpdated;

        private bool isHolding;
        private float holdTimer;

        void Awake()
        {
            TryFindCamera();
        }

        private void TryFindCamera()
        {
            if (playerCamera != null) return;

            playerCamera = Camera.main;

            if (playerCamera == null)
                playerCamera = FindAnyObjectByType<Camera>();

            if (playerCamera != null)
                Debug.Log("PlayerCamera was not assigned, auto-found " + playerCamera.name, this);
            else
                Debug.LogWarning("Could not find a Camera in the scene. InteractSystem will not function until a camera is available.", this);
        }

        void OnEnable()
        {
            if (interactInputAction == null)
            {
                Debug.LogWarning("InteractInputAction is not assigned in " + name, this);
                return;
            }
            interactInputAction.action.started += InteractInputStarted;
            interactInputAction.action.canceled += InteractInputCanceled;
            interactInputAction.action.Enable();
        }

        void OnDisable()
        {
            if (interactInputAction == null) return;
            interactInputAction.action.started -= InteractInputStarted;
            interactInputAction.action.canceled -= InteractInputCanceled;
            interactInputAction.action.Disable();
        }

        void InteractInputStarted(InputAction.CallbackContext context)
        {
            if (CurrentInteractable == null || !CurrentInteractable.IsInteractable) return;
            if (CurrentInteractable.OneTimeInteract && CurrentInteractable.InteractedOnce) return;

            float holdTime = CurrentInteractable.HoldTime;

            if (holdTime > 0f)
            {
                isHolding = true;
                holdTimer = 0f;
                OnHoldAttemptStarted?.Invoke(holdTime);
            }
            else
            {
                CurrentInteractable.OnInteract(this);
                OnPerformedInteraction?.Invoke(CurrentInteractableObject, this);
            }
        }

        void InteractInputCanceled(InputAction.CallbackContext context)
        {
            if (CurrentInteractable != null)
                CurrentInteractable.OnInteractReleased(this);

            if (isHolding)
                OnHoldAttemptEnded?.Invoke();

            isHolding = false;
            holdTimer = 0f;
        }

        void Update()
        {
            if (CurrentInteractableObject == null && CurrentInteractable != null)
                OnLostObjectPerformed();

            PerformRaycast();
            DrawDebugRay();

            if (isHolding && CurrentInteractable != null)
            {
                holdTimer += Time.deltaTime;
                float holdTime = CurrentInteractable.HoldTime;

                if (holdTime > 0f && holdTimer >= holdTime)
                {
                    CurrentInteractable.OnInteract(this);
                    OnPerformedInteraction?.Invoke(CurrentInteractableObject, this);
                    OnHoldAttemptEnded?.Invoke();
                    isHolding = false;
                    holdTimer = 0f;
                }
            }
        }

        private void DrawDebugRay()
        {
            if (!showDebugRay) return;
            if (playerCamera == null) TryFindCamera();
            if (playerCamera == null) return;

            Ray ray = playerCamera.ViewportPointToRay(Vector3.one * 0.5f);
            Vector3 endPoint;

            if (lastRaycastHit.HasValue)
            {
                endPoint = lastRaycastHit.Value.point;
                bool hitInteractable = CurrentInteractable != null;
                Debug.DrawLine(ray.origin, endPoint, hitInteractable ? Color.green : Color.yellow);
                Debug.DrawLine(endPoint, ray.origin + ray.direction * maxDistance, Color.red);
            }
            else
            {
                endPoint = ray.origin + ray.direction * maxDistance;
                Debug.DrawLine(ray.origin, endPoint, Color.red);
            }
        }

        private void PerformRaycast()
        {
            if (playerCamera == null) TryFindCamera();
            if (playerCamera == null) return;

            Ray ray = playerCamera.ViewportPointToRay(Vector3.one * 0.5f);
            int hitCount = Physics.RaycastNonAlloc(ray, raycastHitBuffer, maxDistance, interactableLayer);

            if (hitCount > 0)
            {
                for (int i = 0; i < hitCount; i++)
                {
                    RaycastHit hit = raycastHitBuffer[i];
                    var interactable = hit.collider.GetComponent<IInteractable>();
                    if (interactable != null)
                    {
                        lastRaycastHit = hit;
                        CheckInteractable(interactable, hit.collider.gameObject);
                        return;
                    }
                }
            }

            lastRaycastHit = null;
            OnLostObjectPerformed();
        }

        public void OnDetectedObjectPerformed(RaycastHit hitResult)
        {
            var interactable = hitResult.collider.GetComponent<IInteractable>();
            if (interactable != null)
            {
                lastRaycastHit = hitResult;
                CheckInteractable(interactable, hitResult.collider.gameObject);
            }
            else
            {
                OnLostObjectPerformed();
            }
        }

        private void CheckInteractable(IInteractable interactable, GameObject obj)
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

        public void OnLostObjectPerformed()
        {
            if (CurrentInteractable != null)
            {
                if (CurrentInteractable.OneTimeInteract)
                    CurrentInteractable.SetInteractedOnce(false);

                CurrentInteractable.OnHoverExit(this);
                UnsubscribeCurrent();

                OnInteractableLost?.Invoke(CurrentInteractable, this);

                CurrentInteractable = null;
                CurrentInteractableObject = null;
            }

            isHolding = false;
            holdTimer = 0f;
            OnCurrentInteractableUpdated?.Invoke();
        }

        private void UnsubscribeCurrent()
        {
            if (CurrentInteractable != null)
                CurrentInteractable.OnInteractableUpdated -= HandleInteractableUpdated;
        }

        private void HandleInteractableUpdated()
        {
            OnCurrentInteractableUpdated?.Invoke();
        }
    }
}
