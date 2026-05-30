// Handles picking up and dropping objects. When the player interacts with a pickable item, it parents the object to a holder transform, applies position/rotation offsets, disables physics and shadow casting, and fires events. If the player already holds an item, it queues the new pickup for a short delay and fires an occupied-attempt event. Also supports a drop input action to let go.

using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PickupSystem : MonoBehaviour
{
    public static PickupSystem Instance { get; private set; }

    public Transform PlayerArmBase;
    public Transform PickableObjectHolder;
    [Header("Inputs")]
    [SerializeField] InputActionReference DropInputAction;

    public event System.Action<GameObject> OnPickedItemChanged;
    public event System.Action<IPickable> OnItemPicked;
    public event System.Action OnAttemptedPickWhileOccupied;

    IPickable currentPickableItem;
    GameObject currentPickedObject;

    IPickable attemptedPickableItem;
    GameObject attemptedPickableObject;

    Coroutine delayedPickCoroutine;

    void Awake()
    {
        Instance = this;
    }

    #region Input Setup
    void OnEnable()
    {
        DropInputAction.action.performed += DropInputPerformed;
        DropInputAction.action.Enable();
    }

    void OnDisable()
    {
        DropInputAction.action.performed -= DropInputPerformed;
        DropInputAction.action.Disable();
    }

    void DropInputPerformed(InputAction.CallbackContext context)
    {
        if (currentPickedObject != null)
            DropObject(currentPickedObject, currentPickableItem);
    }
    #endregion

    public void CheckIfPickable(GameObject interactedObject)
    {
        if (interactedObject == null) return;

        if (interactedObject.TryGetComponent(out IPickable pickable) && pickable.GetIsPickable())
        {
            if (currentPickableItem != null)
            {
                attemptedPickableItem = pickable;
                attemptedPickableObject = interactedObject;

                OnAttemptedPickWhileOccupied?.Invoke();

                if (delayedPickCoroutine != null)
                    StopCoroutine(delayedPickCoroutine);
                delayedPickCoroutine = StartCoroutine(PickAfterDelay(0.2f));
            }
            else
            {
                PickupObject(interactedObject, pickable);
            }
        }
    }

    IEnumerator PickAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        delayedPickCoroutine = null;

        if (attemptedPickableObject != null && attemptedPickableItem != null)
        {
            PickupObject(attemptedPickableObject, attemptedPickableItem);
        }
    }

    public void PickupObject(GameObject obj, IPickable pickable)
    {
        currentPickableItem = pickable;
        currentPickedObject = obj;

        obj.transform.parent = PickableObjectHolder;
        obj.transform.localPosition = pickable.GetItemOffsetLocation();
        obj.transform.localRotation = pickable.GetItemOffsetRotation();
        PlayerArmBase.localPosition = pickable.GetHandOffsetLocation();
        pickable.SetPickState(false);
        pickable.Picked();

        OnItemPicked?.Invoke(pickable);
        OnPickedItemChanged?.Invoke(obj);

        attemptedPickableItem = null;
        attemptedPickableObject = null;
    }

    public void DropCurrentHeldItem()
    {
        DropObject(currentPickedObject, currentPickableItem);
    }
    void DropObject(GameObject obj, IPickable pickable)
    {
        if (obj == null || pickable == null) return;

        pickable.SetPickState(true);
        pickable.Dropped();

        obj.transform.parent = null;

        currentPickableItem = null;
        currentPickedObject = null;

        OnPickedItemChanged?.Invoke(null);
    }

    public void ForceReleaseWithoutCallbacks()
    {
        currentPickableItem = null;
        currentPickedObject = null;
        OnPickedItemChanged?.Invoke(null);
    }

    // Helpers for external scripts
    public bool IsHoldingItem() => currentPickedObject != null;
    public GameObject GetCurrentObject() => currentPickedObject;
    public IPickable GetCurrentPickable() => currentPickableItem;
    public GameObject GetAttemptedObject() => attemptedPickableObject;
    public IPickable GetAttemptedPickable() => attemptedPickableItem;
}
