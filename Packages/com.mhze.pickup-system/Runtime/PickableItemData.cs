using UnityEngine;

namespace MHZE.PickupSystem
{
    [CreateAssetMenu(fileName = "NewPickableItemData", menuName = "ScriptableObjects/PickableItemData")]
    public class PickableItemData : ScriptableObject
    {
        public string ItemName;

        [Header("Shop Data")]
        [TextArea]
        public string ItemDescription = "This is an Item!";
        public int ItemCost = 100;
        public Sprite ItemIcon;
        public GameObject ItemPrefab;

        [Header("Pickup Settings")]
        public bool IsPickable;
        [Tooltip("Shared grip, finger closure and arm presentation. Null retains the legacy hand fields below.")]
        public ItemHoldProfile HoldProfile;
        [Tooltip("Position of this model's root in the animated hand socket's local coordinates.")]
        public Vector3 ItemLocationOffset;
        [Tooltip("Rotation of this model's root in the animated hand socket's local coordinates.")]
        public Vector3 ItemRotationOffset;
        [Tooltip("Additional camera-local arm offset; never an absolute arm position.")]
        public Vector3 HandLocationOffset;
        [Range(0f, 1f)]
        public float HandHoldPressure;
        [Tooltip("Legacy grip index: 0 = palm, 1 = stick, 2 = pinch. Used only without HoldProfile.")]
        public float HandHoldTypeIndex;
    }
}
