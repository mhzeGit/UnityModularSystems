using System.Collections.Generic;
using UnityEngine;

namespace MHZE.PickupSystem
{
    public enum PlayerHand { Left, Right }

    /// <summary>Auto shows a hand only while it is in use; Show and Hide force it.</summary>
    public enum HandVisibility { Auto, Show, Hide }

    /// <summary>
    /// Shows each first-person hand only when it is needed. The arm model draws both arms as one mesh, so
    /// a hidden hand has its arm bone scaled to zero (the same trick the reach and the offer use), which
    /// also takes whatever it holds out of sight. Each hand is decided on its own every frame: the right
    /// hand shows while it holds an item, reaches, or its animator is anywhere but the resting state, so a
    /// release is seen all the way down; the left hand has no use of its own and stays hidden. Anything
    /// else can force a hand with <see cref="Show"/> or <see cref="Hide"/> under an owner; Hide beats Show
    /// beats Auto, and <see cref="Clear"/> (or the owner being destroyed) gives the hand back, so a skipped
    /// or rewound caller cannot leave a hand stuck. <see cref="PlayerArmAnimator"/> adds this to its rig.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(600)]
    [AddComponentMenu("MHZE/Pickup/Player Arm Visibility")]
    public sealed class PlayerArmVisibility : MonoBehaviour
    {
        private struct Request
        {
            public PlayerHand hand;
            public HandVisibility mode;
            public object owner;
        }

        [Header("Base setting (Auto unless a hand should always or never show)")]
        [SerializeField] private HandVisibility left = HandVisibility.Auto;
        [SerializeField] private HandVisibility right = HandVisibility.Auto;

        [Header("Rig (found by name when empty)")]
        [Tooltip("The left arm: the model's second, mirrored arm.")]
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightArm;

        [Tooltip("The animator state the arm rests in when nothing is held. Hidden there; shown in every other state or transition.")]
        [SerializeField] private string idleState = "Idle";

        private readonly List<Request> requests = new List<Request>();
        private Animator animator;
        private PlayerArmReach reach;
        private int idleHash;
        private Vector3 leftRest = Vector3.one;
        private Vector3 rightRest = Vector3.one;
        private bool resolved;

        /// <summary>The player's arm rig, while it is enabled.</summary>
        public static PlayerArmVisibility Current { get; private set; }

        /// <summary>Whether a hand is shown right now, from its setting, requests and use.</summary>
        public bool IsVisible(PlayerHand hand)
        {
            Resolve();
            bool hide = (hand == PlayerHand.Left ? left : right) == HandVisibility.Hide;
            bool show = (hand == PlayerHand.Left ? left : right) == HandVisibility.Show;
            for (int i = requests.Count - 1; i >= 0; i--)
            {
                Request r = requests[i];
                if (r.owner is UnityEngine.Object o && o == null)
                {
                    requests.RemoveAt(i);
                    continue;
                }

                if (r.hand != hand) continue;
                if (r.mode == HandVisibility.Hide) hide = true;
                else show = true;
            }

            if (hide) return false;
            return show || (hand == PlayerHand.Right && RightInUse());
        }

        /// <summary>Forces a hand to show for <paramref name="owner"/>, unless something hides it.</summary>
        public void Show(PlayerHand hand, object owner) => Set(hand, HandVisibility.Show, owner);

        /// <summary>Forces a hand out of sight for <paramref name="owner"/>.</summary>
        public void Hide(PlayerHand hand, object owner) => Set(hand, HandVisibility.Hide, owner);

        /// <summary>Gives back every hand <paramref name="owner"/> forced; they go back to their own rules.</summary>
        public void Clear(object owner) => requests.RemoveAll(r => r.owner == owner);

        /// <summary>
        /// Puts both arm bones at their true scale until the next <see cref="Refresh"/>. Call it before
        /// reparenting something to or from a hand: a hidden hand is a zero-scale bone, and keeping the
        /// world scale of a child through it would zero the child's own scale for good.
        /// </summary>
        public void RestoreScales()
        {
            Resolve();
            Apply(leftArm, leftRest, true);
            Apply(rightArm, rightRest, true);
        }

        /// <summary>Applies the hands now instead of at the end of the frame.</summary>
        public void Refresh()
        {
            Resolve();
            Apply(leftArm, leftRest, IsVisible(PlayerHand.Left));
            Apply(rightArm, rightRest, IsVisible(PlayerHand.Right));
        }

        private void Set(PlayerHand hand, HandVisibility mode, object owner)
        {
            if (owner == null) throw new System.ArgumentNullException(nameof(owner));
            requests.RemoveAll(r => r.owner == owner && r.hand == hand);
            requests.Add(new Request { hand = hand, mode = mode, owner = owner });
        }

        private void Resolve()
        {
            if (resolved) return;
            resolved = true;
            animator = GetComponent<Animator>();
            reach = GetComponent<PlayerArmReach>();
            idleHash = Animator.StringToHash(idleState);
            if (leftArm == null) leftArm = transform.Find("Armature/Root/Right Arm.001");
            if (rightArm == null) rightArm = transform.Find("Armature/Root/Right Arm");
            leftRest = RestScale(leftArm);
            rightRest = RestScale(rightArm);
        }

        private static Vector3 RestScale(Transform arm) =>
            arm != null && arm.localScale.sqrMagnitude > 1e-6f ? arm.localScale : Vector3.one;

        private bool RightInUse()
        {
            PickupSystem pickup = PickupSystem.Instance;
            if (pickup != null && pickup.IsHoldingItem()) return true;
            if (reach != null && reach.IsReaching) return true;
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return false;
            // A state that is not the resting one (or none yet, as under a custom playable graph) is use.
            return animator.IsInTransition(0) || animator.GetCurrentAnimatorStateInfo(0).shortNameHash != idleHash;
        }

        private static void Apply(Transform arm, Vector3 rest, bool visible)
        {
            if (arm != null) arm.localScale = visible ? rest : Vector3.zero;
        }

        private void OnEnable()
        {
            Resolve();
            Current = this;
        }

        private void OnDisable()
        {
            if (Current == this) Current = null;
            if (!resolved) return;
            Apply(leftArm, leftRest, true);
            Apply(rightArm, rightRest, true);
        }

        private void LateUpdate() => Refresh();
    }
}
