using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace MHZE.PickupSystem
{
    [MovedFrom(true, "BlahBlahFamily", "Assembly-CSharp", null)]
    [RequireComponent(typeof(Animator))]
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerArmAnimator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private PickupSystem pickupSystem;
        [SerializeField] private Transform rightHandSocket;
        [Header("Animation Parameter Names")]
        [SerializeField] private string isHoldingParam = "IsHolding";
        [SerializeField] private string holdTypeParam = "HoldType";
        [SerializeField] private string holdPressureParam = "HoldPressure";
        [SerializeField] private string attackTriggerParam = "Attack";
        [SerializeField] private string throwTriggerParam = "Throw";

        private GameObject observedItem;
        private GameObject lastReleasedItem;
        private ItemHoldProfile currentProfile;
        private int holdingHash, typeHash, pressureHash;
        public Transform RightHandSocket => rightHandSocket;
        public Animator Animator => animator;
        public PickupSystem Pickup => pickupSystem;

        private void Awake()
        {
            holdingHash = UnityEngine.Animator.StringToHash(isHoldingParam);
            typeHash = UnityEngine.Animator.StringToHash(holdTypeParam);
            pressureHash = UnityEngine.Animator.StringToHash(holdPressureParam);
            // Every arm rig hides the hand it is not using; add it by hand only to change its settings.
            if (!TryGetComponent<PlayerArmVisibility>(out _)) gameObject.AddComponent<PlayerArmVisibility>();
            ResolveReferences();
        }

        private void ResolveReferences()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (pickupSystem == null) pickupSystem = GetComponentInParent<PickupSystem>();
            if (pickupSystem == null) pickupSystem = PickupSystem.Instance;
            if (pickupSystem == null) pickupSystem = FindObjectOfType<PickupSystem>();
            if (rightHandSocket == null)
                rightHandSocket = transform.Find("Armature/Root/Right Arm/Right Forearm/Right Hand/Right Hand Socket");
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (pickupSystem != null)
            {
                pickupSystem.ConfigureHoldingRig(transform, rightHandSocket != null ? rightHandSocket : pickupSystem.PickableObjectHolder);
                pickupSystem.OnPickedItemChanged += HandlePickedItemChanged;
                HandlePickedItemChanged(pickupSystem.GetCurrentObject());
            }
        }

        private void Start()
        {
            // With Fast Play, a story may give an item before the arm's Start runs.
            if (pickupSystem != null) HandlePickedItemChanged(pickupSystem.GetCurrentObject());
        }

        private void OnDisable()
        {
            if (pickupSystem != null) pickupSystem.OnPickedItemChanged -= HandlePickedItemChanged;
            observedItem = null;
            lastReleasedItem = null;
            currentProfile = null;
        }

        private void LateUpdate()
        {
            // Also handles destroyed/disabled objects and ForceReleaseWithoutCallbacks.
            if (pickupSystem == null) return;
            var item = pickupSystem.GetCurrentObject();
            if (item != observedItem || (item == null && animator != null && animator.GetBool(holdingHash)))
                HandlePickedItemChanged(item);
        }

        private void HandlePickedItemChanged(GameObject item)
        {
            if (item == null && observedItem != null) lastReleasedItem = observedItem;
            observedItem = item;
            currentProfile = item != null && item.TryGetComponent<PickableItemBase>(out var pickable) ? pickable.HoldProfile : null;
            if (item != null) UpdateHoldingState(pickupSystem.GetCurrentPickable());
            else SetIdleState();
        }

        public void UpdateHoldingState(IPickable item)
        {
            if (item == null) { SetIdleState(); return; }
            if (animator == null || animator.runtimeAnimatorController == null) return;
            animator.SetBool(holdingHash, true);
            animator.SetInteger(typeHash, Mathf.Clamp(Mathf.RoundToInt(item.GetHandHoldTypeIndex()), 0, 2) + 1);
            animator.SetFloat(pressureHash, Mathf.Clamp01(item.GetHandHoldPressure()));
        }

        public void SetIdleState()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            animator.SetBool(holdingHash, false);
            animator.SetInteger(typeHash, 0);
            animator.SetFloat(pressureHash, 0f);
            animator.ResetTrigger(attackTriggerParam);
        }

        /// <summary>Use animations are opt-in per profile; everyday objects must not trigger attacks.</summary>
        public void TriggerUse(GameObject usedItem)
        {
            if (usedItem != observedItem || currentProfile == null || string.IsNullOrEmpty(currentProfile.UseTrigger)) return;
            Trigger(currentProfile.UseTrigger);
        }

        public void TriggerAttack() => Trigger(attackTriggerParam);
        public void TriggerThrow() => Trigger(throwTriggerParam);
        public void NotifyThrown(GameObject item)
        {
            if (item == observedItem || item == lastReleasedItem) { TriggerThrow(); lastReleasedItem = null; }
        }
        private void Trigger(string parameter)
        {
            if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(parameter)) return;
            foreach (var p in animator.parameters)
                if (p.name == parameter && p.type == AnimatorControllerParameterType.Trigger) { animator.SetTrigger(parameter); return; }
        }
    }
}
