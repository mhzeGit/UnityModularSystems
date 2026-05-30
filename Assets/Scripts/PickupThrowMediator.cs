using MHZE.PickupSystem;
using MHZE.ThrowSystem;
using UnityEngine;

public class PickupThrowMediator : MonoBehaviour
{
    [SerializeField] private PickupSystem pickupSystem;
    [SerializeField] private ThrowingSystem throwingSystem;

    private void OnEnable()
    {
        if (pickupSystem == null)
        {
            Debug.LogError("[PickupThrowMediator] pickupSystem is not assigned.", this);
            return;
        }

        if (throwingSystem == null)
        {
            Debug.LogError("[PickupThrowMediator] throwingSystem is not assigned.", this);
            return;
        }

        pickupSystem.OnPickedItemChanged += HandlePickedItemChanged;
        throwingSystem.OnItemThrown += HandleItemThrown;
    }

    private void OnDisable()
    {
        if (pickupSystem != null)
            pickupSystem.OnPickedItemChanged -= HandlePickedItemChanged;

        if (throwingSystem != null)
            throwingSystem.OnItemThrown -= HandleItemThrown;
    }

    private void HandlePickedItemChanged(GameObject obj)
    {
        throwingSystem.SetCurrentHeldObject(obj);
    }

    private void HandleItemThrown(GameObject thrownObject)
    {
        pickupSystem.DropCurrentHeldItem();
    }
}
