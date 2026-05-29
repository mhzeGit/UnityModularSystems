// Handles picking up and dropping objects. When the player interacts with a pickable item, it parents the object to a holder transform, applies position/rotation offsets, disables physics and shadow casting, and fires events. If the player already holds an item, it queues the new pickup for a short delay and fires an occupied-attempt event. Also supports a drop input action to let go.

using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PickupSystem : MonoBehaviour
{
    public Transform PlayerArmBase;
    public Transform PickableObjectHolder;
    [Header("Inputs")]
    [SerializeField] InputActionReference DropInputAction;

    public event System.Action<GameObject> OnPickedItemChanged;      // Fires when picked object changes
    public event System.Action<IPickable> OnItemPicked;             // Fires whenever an item is picked
    public event System.Action OnAttemptedPickWhileOccupied;        // Fires when trying to pick with item held

    IPickable currentPickableItem;
    GameObject currentPickedObject;

    // Stores the item the player tried to pick while already holding one
    IPickable attemptedPickableItem;
    GameObject attemptedPickableObject;

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

        IPickable pickable = interactedObject.GetComponent<IPickable>();
        if (pickable != null && pickable.GetIsPickable())
        {
            if (currentPickableItem != null)
            {
                // Already holding an item -> fire event and schedule pickup
                attemptedPickableItem = pickable;
                attemptedPickableObject = interactedObject;

                OnAttemptedPickWhileOccupied?.Invoke();
                StartCoroutine(PickAfterDelay(0.2f));
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
        SetObjectPickState(obj, false);
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

        SetObjectPickState(obj, true);
        pickable.Dropped();

        obj.transform.parent = null;

        currentPickableItem = null;
        currentPickedObject = null;

        OnPickedItemChanged?.Invoke(null);
    }

    void SetObjectPickState(GameObject obj, bool enable)
    {
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null)
            rb.isKinematic = !enable;

        foreach (MeshRenderer mr in obj.GetComponentsInChildren<MeshRenderer>(true))
            mr.shadowCastingMode = enable
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;

        foreach (Collider col in obj.GetComponentsInChildren<Collider>(true))
            col.enabled = enable;
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
