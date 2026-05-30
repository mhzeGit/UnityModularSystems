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

            if (cachedRigidbody == null)
                Debug.LogWarning($"No Rigidbody found on {gameObject.name}. Pickable items require a Rigidbody.", this);
        }

        public void Pickup()
        {
            if (PickupSystem.Instance == null)
            {
                Debug.LogWarning($"PickupSystem.Instance is null — no PickupSystem found in the scene. Cannot pick up {gameObject.name}.");
                return;
            }

            if (pickableItemData == null)
            {
                Debug.LogWarning($"PickableItemData is not assigned on {gameObject.name}. Assign a PickableItemData ScriptableObject and ensure IsPickable is true.", this);
                return;
            }


            PickupSystem.Instance.CheckIfPickable(gameObject);
        }

        public bool GetIsPickable()
        {
            if (pickableItemData != null)
            {
                return pickableItemData.IsPickable;
            }

            Debug.LogWarning($"PickableItemData is not assigned on {gameObject.name}. Cannot determine if pickable.", this);
            return false;
        }

        public void Picked()
        {
            OnPickedItem.Invoke();
        }
        public void Dropped()
        {
            OnDroppedItem.Invoke();
        }

        public GameObject DropSelfIfHeld()
        {
            PickupSystem pickupSystem = PickupSystem.Instance;

            if (pickupSystem == null)
            {
                Debug.LogWarning("No PickupSystem found in scene!");
                return null;
            }

            if (pickupSystem.GetCurrentObject() != gameObject)
                return null;

            pickupSystem.DropCurrentHeldItem();
            return gameObject;
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
            if (pickableItemData != null)
                return pickableItemData.ItemName;

            Debug.LogWarning($"PickableItemData is not assigned on {gameObject.name}. Returning empty name.", this);
            return string.Empty;
        }


    }
}
