using UnityEngine;
using MHZE.ThrowSystem;

namespace MHZE.PickupSystem
{
    [RequireComponent(typeof(PlayerArmAnimator))]
    public sealed class PickupArmThrowBridge : MonoBehaviour
    {
        [SerializeField] private ThrowingSystem throwingSystem;
        private PlayerArmAnimator arm;
        private void OnEnable()
        {
            arm = GetComponent<PlayerArmAnimator>();
            if (throwingSystem == null) throwingSystem = FindObjectOfType<ThrowingSystem>();
            if (throwingSystem != null) throwingSystem.OnItemThrown += arm.NotifyThrown;
        }
        private void OnDisable() { if (throwingSystem != null && arm != null) throwingSystem.OnItemThrown -= arm.NotifyThrown; }
    }
}
