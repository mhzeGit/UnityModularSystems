//Made By MHZE

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




}