// Default pickable item that sits on any object the player can pick up. Holds a reference to PickableItemData for settings and fires UnityEvents when picked or dropped. Also has a helper to force-drop itself from the pickup system and return the dropped GameObject.

using UnityEngine;
using UnityEngine.Events;

public class PickableItemBase : MonoBehaviour, IPickable
{
    public PickableItemData pickableItemData;
    public UnityEvent OnPickedItem;
    public UnityEvent OnDroppedItem;


    public bool GetIsPickable()
    {
        if (pickableItemData != null)
        {
            return pickableItemData.IsPickable;
        }
        else { return false; }
    }
    
    public void Picked()
    {
        OnPickedItem.Invoke();
    }
    public void Dropped()
    {
        OnDroppedItem.Invoke();
    }
    
    /// Forces this item to be dropped from the pickup system and returns the dropped GameObject
    /// <returns>The GameObject that was dropped, or null if not currently held</returns>
    public GameObject TakeThisPickableItem()
    {
        // Find the pickup system in the scene
        PickupSystem pickupSystem = FindFirstObjectByType<PickupSystem>();
        
        if (pickupSystem == null)
        {
            Debug.LogWarning("No PickupSystem found in scene!");
            return null;
        }
        
        // Check if this item is currently being held
        GameObject currentHeldObject = pickupSystem.GetCurrentObject();
        if (currentHeldObject != this.gameObject)
        {
            return null;
        }
        
        // Force drop the item
        pickupSystem.DropCurrentHeldItem();
        
        // Return reference to this GameObject (now dropped)
        return this.gameObject;
    }

    public Vector3 GetHandOffsetLocation()
    {
        if (pickableItemData != null)
        {
            return pickableItemData.HandLocationOffset;
        }
        else { return Vector3.zero; }
    }
    public Vector3 GetItemOffsetLocation()
    {
        if (pickableItemData != null)
        {
            return pickableItemData.ItemLocationOffset;
        }
        else { return Vector3.zero; }
    }
        

    public Quaternion GetItemOffsetRotation()
    {
        if (pickableItemData != null)
        {
            return Quaternion.Euler(pickableItemData.ItemRotationOffset);
        }
        else { return Quaternion.Euler(0,0,0); }
    } 

    public float GetHandHoldPressure()
    {
        if (pickableItemData != null)
        {
            return pickableItemData.HandHoldPressure;
        }
        else { return 0; }
    } 

    public float GetHandHoldTypeIndex()
    {
        if (pickableItemData != null)
        {
            return pickableItemData.HandHoldTypeIndex;
        }
        else { return 0; }
    }

    public string GetItemName()
    {
        return pickableItemData.ItemName;
    }

    
}
