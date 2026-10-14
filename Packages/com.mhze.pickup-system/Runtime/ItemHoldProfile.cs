using UnityEngine;

namespace MHZE.PickupSystem
{
    public enum ItemGrip { Palm = 0, Stick = 1, Pinch = 2 }

    /// <summary>Shared hand presentation. Model-specific placement stays on PickableItemData.</summary>
    [CreateAssetMenu(menuName = "MHZE/Pickup/Item Hold Profile", fileName = "HoldProfile")]
    public sealed class ItemHoldProfile : ScriptableObject
    {
        public ItemGrip Grip = ItemGrip.Palm;
        [Range(0f, 1f)] public float FingerClosure = 0.5f;
        [Tooltip("Added to the rig's authored neutral position and the item's HandLocationOffset.")]
        public Vector3 ArmOffset;
        [Tooltip("Optional Animator trigger for using this item. Empty means no arm action.")]
        public string UseTrigger = "";
    }
}
