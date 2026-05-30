using UnityEngine;
using UnityEngine.Events;

namespace MHZE.PickupSystem
{
    public class PickableItemBase : MonoBehaviour, IPickable
    {
        public PickableItemData pickableItemData;
        public UnityEvent OnPickedItem;
        public UnityEvent OnDroppedItem;

        Rigidbody cachedRigidbody;
        MeshRenderer[] cachedRenderers;
        Collider[] cachedColliders;

        void Awake()
        {
            cachedRigidbody = GetComponent<Rigidbody>();
            cachedRenderers = GetComponentsInChildren<MeshRenderer>(true);
            cachedColliders = GetComponentsInChildren<Collider>(true);
        }

        public void Pickup()
        {
            if (PickupSystem.Instance != null)
                PickupSystem.Instance.CheckIfPickable(gameObject);
        }

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

        public GameObject TakeThisPickableItem()
        {
            PickupSystem pickupSystem = PickupSystem.Instance;

            if (pickupSystem == null)
            {
                Debug.LogWarning("No PickupSystem found in scene!");
                return null;
            }

            GameObject currentHeldObject = pickupSystem.GetCurrentObject();
            if (currentHeldObject != this.gameObject)
            {
                return null;
            }

            pickupSystem.DropCurrentHeldItem();

            return this.gameObject;
        }

        public void SetPickState(bool enable)
        {
            if (cachedRigidbody != null)
                cachedRigidbody.isKinematic = !enable;

            foreach (MeshRenderer mr in cachedRenderers)
                mr.shadowCastingMode = enable
                    ? UnityEngine.Rendering.ShadowCastingMode.On
                    : UnityEngine.Rendering.ShadowCastingMode.Off;

            foreach (Collider col in cachedColliders)
                col.enabled = enable;
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
            else { return Quaternion.Euler(0, 0, 0); }
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
}
