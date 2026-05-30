using UnityEngine;

namespace MHZE.PickupSystem
{
    public interface IPickable
    {
        void Picked();
        void Dropped();

        GameObject TakeThisPickableItem();

        bool GetIsPickable();

        Vector3 GetHandOffsetLocation();
        Vector3 GetItemOffsetLocation();
        Quaternion GetItemOffsetRotation();

        float GetHandHoldPressure();
        float GetHandHoldTypeIndex();
        string GetItemName();

        void SetPickState(bool enable);
    }
}
