using UnityEngine;
using MHZE.UseSystem;

namespace MHZE.PickupSystem
{
    [RequireComponent(typeof(PlayerArmAnimator))]
    public sealed class PickupArmUseBridge : MonoBehaviour
    {
        [SerializeField] private MHZE.UseSystem.UseSystem useSystem;
        private PlayerArmAnimator arm;
        private void OnEnable()
        {
            arm = GetComponent<PlayerArmAnimator>();
            if (useSystem == null) useSystem = FindObjectOfType<MHZE.UseSystem.UseSystem>();
            if (useSystem != null)
            {
                useSystem.playerHand = arm.RightHandSocket;
                useSystem.OnUseItem += Used;
            }
        }
        private void OnDisable() { if (useSystem != null) useSystem.OnUseItem -= Used; }
        private void Used(IUsable item) { if (item is Component component) arm.TriggerUse(component.gameObject); }
    }
}
