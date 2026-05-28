//Made By MHZE

using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InteractSystem : MonoBehaviour
{

    [Header("Inputs")]
    [SerializeField] InputActionReference InteractInputAction;

    [Header("Raycast Settings")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private LayerMask interactableLayer = -1;
    [SerializeField] private float maxDistance = 5f;

    [Header("Debug")]
    [SerializeField] private bool showDebugRay = true;

    [HideInInspector] public IInteractable currentInteractableInterface;
    [HideInInspector] public GameObject currentInteractableGameobject;

    private RaycastHit? lastRaycastHit;

    public event Action<IInteractable> OnInteractableFound;
    public event Action<GameObject> OnPerformedInteraction;
    public event Action OnInteractableLost;

    public event Action<float> OnHoldAttemptStarted;  // only if holdTime > 0
    public event Action OnHoldAttemptEnded;           // only if holdTime > 0

    public event Action OnCurrentInteractableUpdated; // The main event that gets triggerd each time something changes (anything)

    private bool isHolding;
    public float HoldTimer;

    void OnEnable()
    {
        InteractInputAction.action.started += InteractInputStarted;
        InteractInputAction.action.canceled += InteractInputCanceled;
        InteractInputAction.action.Enable();
    }

    void OnDisable()
    {
        InteractInputAction.action.started -= InteractInputStarted;
        InteractInputAction.action.canceled -= InteractInputCanceled;
        InteractInputAction.action.Disable();
    }

    void InteractInputStarted(InputAction.CallbackContext context)
    {
        if (currentInteractableInterface == null) return;
        if (currentInteractableInterface.GetIsInteractable() != true) return; // checking if interactable item is acctully interactable or not.
        float holdTime = currentInteractableInterface.GetInteractHoldTime();

        if (holdTime > 0f)
        {
            isHolding = true;
            HoldTimer = 0f;

            OnHoldAttemptStarted?.Invoke(holdTime);

            if (currentInteractableInterface is InteractableItemBase item)
                item.TriggerHoldStarted(holdTime);
        }
        else
        {
            currentInteractableInterface.OnInteract();
            OnPerformedInteraction?.Invoke(currentInteractableGameobject);
        }
    }

    void InteractInputCanceled(InputAction.CallbackContext context)
    {
        if (currentInteractableInterface is InteractableItemBase item)
        {
            if (currentInteractableInterface.GetInteractHoldTime() > 0f)
            {
                OnHoldAttemptEnded?.Invoke();
                item.TriggerHoldEnded();
            }

            item.TriggerInteractReleased();
        }

        isHolding = false;
        HoldTimer = 0f;
    }

    void Update()
    {
        PerformRaycast();
        DrawDebugRay();

        if (isHolding && currentInteractableInterface != null)
        {
            HoldTimer += Time.deltaTime;
            float holdTime = currentInteractableInterface.GetInteractHoldTime();

            if (holdTime > 0f && HoldTimer >= holdTime)
            {
                currentInteractableInterface.OnInteract();
                OnPerformedInteraction?.Invoke(currentInteractableGameobject);

                if (currentInteractableInterface is InteractableItemBase item)
                    item.TriggerHoldEnded();

                OnHoldAttemptEnded?.Invoke();

                isHolding = false;
                HoldTimer = 0f;
            }
        }
    }

    private void DrawDebugRay()
    {
        if (!showDebugRay || playerCamera == null) return;

        Ray ray = playerCamera.ViewportPointToRay(Vector3.one * 0.5f);
        Vector3 endPoint;

        if (lastRaycastHit.HasValue)
        {
            endPoint = lastRaycastHit.Value.point;
            bool hitInteractable = currentInteractableInterface != null;
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
        if (playerCamera == null) return;

        Ray ray = playerCamera.ViewportPointToRay(Vector3.one * 0.5f);

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, interactableLayer))
        {
            lastRaycastHit = hit;
            CheckInteractable(hit.collider.gameObject);
        }
        else
        {
            lastRaycastHit = null;
            OnLostObjectPerformed();
        }
    }

    public void OnDetectedObjectPerformed(RaycastHit hitResult)
    {
        CheckInteractable(hitResult.collider.gameObject);
    }

    public void CheckInteractable(GameObject ObjectFound)
    {
        if (ObjectFound != null)
        {
            var interactable = ObjectFound.GetComponent<IInteractable>();
            if (interactable != null)
            {
                SwitchCurrentInteractable(interactable, ObjectFound);


                // invoke regardless of interactable state
                OnInteractableFound?.Invoke(currentInteractableInterface);


                // always say "something changed"
                OnCurrentInteractableUpdated?.Invoke();
                return;
            }
        }
        OnLostObjectPerformed();
    }

    public void OnLostObjectPerformed() // when lost focus from the interactable item in the past (looked away or something blocking the view)
    {
        if (currentInteractableInterface != null)
        {
            UnsubscribeCurrent();
            currentInteractableGameobject = null;
            currentInteractableInterface = null;
            OnInteractableLost?.Invoke();
        }

        isHolding = false;
        HoldTimer = 0f;

        // lost = also a change
        OnCurrentInteractableUpdated?.Invoke();
    }

    // subscription and unsubscription of each new found interactable item to detect if anything is changed (for example when the isinteractable state change or prompt change) it would let the interacy system know about it.
    void SwitchCurrentInteractable(IInteractable newInteractable, GameObject obj)
    {
        UnsubscribeCurrent();

        currentInteractableInterface = newInteractable;
        currentInteractableGameobject = obj;

        if (currentInteractableInterface is InteractableItemBase item)
            item.OnInteractableUpdated += HandleInteractableUpdated;
    }

    void UnsubscribeCurrent() 
    {
        if (currentInteractableInterface is InteractableItemBase item)
            item.OnInteractableUpdated -= HandleInteractableUpdated;
    }

    void HandleInteractableUpdated()
    {
        OnCurrentInteractableUpdated?.Invoke();
    }
}
