// Interface any pickable object must implement. Defines the contract for picking up, dropping, checking if pickable, getting hand/item offset positions and rotations, hold pressure, hold type, and the item name.

using UnityEngine;

public interface IPickable
{
    // Called when ever this tool is picked
    void Picked();
    void Dropped();

    GameObject TakeThisPickableItem();

    // Returns if this item is pickable or not
    bool GetIsPickable();

    Vector3 GetHandOffsetLocation();
    Vector3 GetItemOffsetLocation();
    Quaternion GetItemOffsetRotation();

    float GetHandHoldPressure();
    float GetHandHoldTypeIndex();
    string GetItemName();

    void SetPickState(bool enable);
}