using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MHZE.PickupSystem
{
    public class PickupSystem : MonoBehaviour
    {
        public static PickupSystem Instance { get; private set; }

        public Transform PlayerArmBase;
        public Transform PickableObjectHolder;

        [Header("Pickup Behaviour")]
        [SerializeField] private bool dropOnPickup = false;

        [Header("Drop Safety")]
        [Tooltip("Refuse the player's drop (and throw) while the held item touches something solid or a wall stands between the camera and it, so an item held over or through a wall is never let go on the far side.")]
        [SerializeField] bool checkDropClearance = true;
        [Tooltip("Where the player looks from, for the line check. Empty uses Camera.main.")]
        [SerializeField] Transform dropCheckOrigin;
        [Tooltip("Layers that can obstruct a drop.")]
        [SerializeField] LayerMask dropObstacleLayers = ~0;
        [Tooltip("Metres added around the item when testing for contact.")]
        [SerializeField, Min(0f)] float dropClearanceMargin = 0.02f;
        [Tooltip("When the held item is in or behind a wall, the drop slides it back toward the camera to the first clear spot, in steps of this many metres.")]
        [SerializeField, Min(0.01f)] float dropPullBackStep = 0.05f;
        [Tooltip("The closest to the camera a pulled-back item may be dropped. If nothing clear is found farther out than this, the drop is refused.")]
        [SerializeField, Min(0f)] float dropPullBackLimit = 0.3f;

        [Header("Inputs")]
        [SerializeField] InputActionReference DropInputAction;

        /// <summary>The player asked to drop the held item but it is held into a wall with no clear spot on the player's side; nothing was dropped.</summary>
        public event System.Action<GameObject> OnDropBlocked;

        public event System.Action<GameObject> OnPickedItemChanged;
        public event System.Action<IPickable> OnItemPicked;
        public event System.Action OnAttemptedPickWhileOccupied;

        /// <summary>A pickup was refused because the <see cref="Bag"/> has no room for the item.</summary>
        public event System.Action<GameObject> OnPickRefused;

        /// <summary>
        /// The bag behind the hand, set by an inventory. Null (the default) keeps the classic behaviour: one
        /// held item, and a pickup while occupied waits for the hand to free up. With a bag, a pickup while
        /// occupied puts the held item away and the new one takes its place.
        /// </summary>
        public IPickupBag Bag { get; set; }

        /// <summary>
        /// True only while the held item is being put away into the <see cref="Bag"/>. An
        /// <see cref="OnPickedItemChanged"/>(null) raised in that window means "stowed", not "dropped".
        /// </summary>
        public bool IsStowing { get; private set; }

        /// <summary>
        /// True only while the player's own interaction picks an item up (<see cref="CheckIfPickable"/>). An
        /// <see cref="OnPickedItemChanged"/> raised in that window is the player taking something; raised outside
        /// it, a story or a script put the item in the hand with <see cref="PickupObject"/>.
        /// </summary>
        public bool IsPlayerPickup { get; private set; }

        IPickable currentPickableItem;
        GameObject currentPickedObject;

        IPickable attemptedPickableItem;
        GameObject attemptedPickableObject;

        Coroutine delayedPickCoroutine;
        static readonly WaitForSeconds s_pickDelay = new WaitForSeconds(0.2f);

        Vector3 neutralArmBaseLocalPosition;
        Transform capturedArmBase;

        /// <summary>Bind an animated hand socket while preserving the rig's authored neutral pose.</summary>
        public void ConfigureHoldingRig(Transform armBase, Transform holder)
        {
            PlayerArmBase = armBase;
            PickableObjectHolder = holder;
            CaptureNeutralArmPosition();
        }

        void CaptureNeutralArmPosition()
        {
            if (PlayerArmBase == null || capturedArmBase == PlayerArmBase) return;
            capturedArmBase = PlayerArmBase;
            neutralArmBaseLocalPosition = PlayerArmBase.localPosition;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError($"Duplicate PickupSystem detected on '{gameObject.name}'. Destroying the newer instance.", this);
                Destroy(this);
                return;
            }

            Instance = this;
            CaptureNeutralArmPosition();
        }

        void Start()
        {
            if (PlayerArmBase != null)
            {
                CaptureNeutralArmPosition();
            }
            else
            {
                Debug.LogWarning("PlayerArmBase is not assigned — hand positioning is disabled. This is optional; assign a Transform if you want arm movement on pickup.", this);
            }

            if (PickableObjectHolder == null)
                Debug.LogWarning("PickableObjectHolder is not assigned in the Inspector — picked items will not reparent.", this);

            if (DropInputAction == null)
                Debug.LogWarning("DropInputAction is not assigned in the Inspector — drop input will not work.", this);
        }

        void OnDestroy()
        {
            if (currentPickedObject != null)
            {
                Debug.LogWarning($"PickupSystem on '{gameObject.name}' destroyed while holding '{currentPickedObject.name}'. Forcing release without callbacks.", this);
                ForceReleaseWithoutCallbacks();
            }
            Bag = null;
            if (Instance == this) Instance = null;
        }

        #region Input Setup
        void OnEnable()
        {
            if (DropInputAction == null)
            {
                Debug.LogError("DropInputAction is null — cannot enable drop input. Assign an InputActionReference in the Inspector.", this);
                return;
            }

            DropInputAction.action.performed += DropInputPerformed;
            DropInputAction.action.Enable();
        }

        void OnDisable()
        {
            if (DropInputAction == null) return;

            DropInputAction.action.performed -= DropInputPerformed;
            DropInputAction.action.Disable();
        }

        void DropInputPerformed(InputAction.CallbackContext context)
        {
            TryPlayerDrop();
        }

        /// <summary>
        /// The player's own drop: refused for an <see cref="IHeldUntilReleased"/> item; when the held item is in or
        /// behind a wall it is first slid back toward the camera to the nearest clear spot, so it lands on the
        /// player's side. Refused (raising <see cref="OnDropBlocked"/>) only when no clear spot exists.
        /// Returns true when the item was dropped.
        /// </summary>
        public bool TryPlayerDrop()
        {
            if (currentPickedObject == null) return false;
            if (HeldItemBlocksPlayerRelease) return false;

            if (IsHeldItemObstructed(out _) && !TryMoveHeldItemToPlayerSide())
            {
                OnDropBlocked?.Invoke(currentPickedObject);
                return false;
            }

            if (currentPickableItem == null)
            {
                Debug.LogWarning("currentPickedObject exists but currentPickableItem is null — dropping via transform parent only.", this);
            }

            DropObject(currentPickedObject, currentPickableItem);
            return true;
        }

        // Moves the held item (still in the hand) so a release lands it clear of the wall.
        bool TryMoveHeldItemToPlayerSide()
        {
            Transform origin = DropLineOrigin();
            if (origin == null) return false;
            return HeldItemClearance.TryMoveToPlayerSide(currentPickedObject, origin, DropPlayerRoot(), dropObstacleLayers,
                dropClearanceMargin, dropPullBackLimit, dropPullBackStep);
        }

        Transform DropLineOrigin()
        {
            if (dropCheckOrigin != null) return dropCheckOrigin;
            return Camera.main != null ? Camera.main.transform : null;
        }

        Transform DropPlayerRoot() => PickableObjectHolder != null ? PickableObjectHolder.root : null;
        #endregion

        /// <summary>True while the held item is an <see cref="IHeldUntilReleased"/> that refuses to be dropped or thrown by the player.</summary>
        public bool HeldItemBlocksPlayerRelease =>
            currentPickedObject != null
            && currentPickedObject.TryGetComponent(out IHeldUntilReleased lockedItem)
            && lockedItem.BlocksPlayerRelease;

        /// <summary>Turns the wall check on the player's drop and throw on or off at run time.</summary>
        public bool CheckDropClearance { get => checkDropClearance; set => checkDropClearance = value; }

        /// <summary>Where the line check starts (the camera). Null falls back to <c>Camera.main</c>.</summary>
        public Transform DropCheckOrigin { get => dropCheckOrigin; set => dropCheckOrigin = value; }

        /// <summary>
        /// True while the held item touches something solid, or a wall stands between the camera and it: letting go
        /// now would put it on the far side or inside the wall. <paramref name="blocker"/> is the collider in the
        /// way. Always false with nothing held or with <see cref="CheckDropClearance"/> off. The player's drop
        /// (<see cref="TryPlayerDrop"/>) answers it by sliding the item to the player's side, a throw by refusing;
        /// <see cref="DropCurrentHeldItem"/> is code and ignores it.
        /// </summary>
        public bool IsHeldItemObstructed(out Collider blocker)
        {
            blocker = null;
            if (!checkDropClearance || currentPickedObject == null) return false;

            blocker = HeldItemClearance.FindBlocker(currentPickedObject, DropLineOrigin(), DropPlayerRoot(), dropObstacleLayers, dropClearanceMargin);
            return blocker != null;
        }

        /// <summary>Raises <see cref="OnDropBlocked"/> for the held item; a throw system that refused a throw calls this.</summary>
        public void NotifyReleaseBlocked()
        {
            if (currentPickedObject != null) OnDropBlocked?.Invoke(currentPickedObject);
        }

        public void CheckIfPickable(GameObject interactedObject)
        {
            if (interactedObject == null)
            {
                Debug.LogWarning("CheckIfPickable called with null object.", this);
                return;
            }

            if (!interactedObject.TryGetComponent(out IPickable pickable))
            {
                Debug.LogWarning($"Object '{interactedObject.name}' does not have an IPickable component. Add PickableItemBase (or a custom IPickable implementation) to it.", interactedObject);
                return;
            }

            if (!pickable.GetIsPickable())
            {
                Debug.LogWarning($"Object '{interactedObject.name}' has IPickable but GetIsPickable() returned false. Check that PickableItemData is assigned and IsPickable is true.", interactedObject);
                return;
            }

            IPickupBag bag = Bag;
            if (bag != null)
            {
                // Something the story put in the hand stays there: the hand is busy, as it always was.
                if (currentPickableItem != null && currentPickedObject != interactedObject && bag.IsHandLocked)
                {
                    OnAttemptedPickWhileOccupied?.Invoke();
                    return;
                }

                // Behind the hand there is a bag, so there is no "hand is full": the held item is put away.
                // Only a bag with no room for it refuses the pickup, and nothing moves.
                if (!bag.Contains(interactedObject) && !bag.CanKeep(interactedObject, pickable))
                {
                    OnPickRefused?.Invoke(interactedObject);
                    return;
                }

                PickupByPlayer(interactedObject, pickable);
                return;
            }

            if (currentPickableItem != null)
            {
                if (HeldItemBlocksPlayerRelease)
                {
                    OnAttemptedPickWhileOccupied?.Invoke();
                    return;
                }

                if (dropOnPickup)
                {
                    DropObject(currentPickedObject, currentPickableItem);
                    PickupByPlayer(interactedObject, pickable);
                    return;
                }

                attemptedPickableItem = pickable;
                attemptedPickableObject = interactedObject;

                OnAttemptedPickWhileOccupied?.Invoke();

                if (delayedPickCoroutine != null)
                    StopCoroutine(delayedPickCoroutine);
                delayedPickCoroutine = StartCoroutine(PickAfterDelay());
            }
            else
            {
                PickupByPlayer(interactedObject, pickable);
            }
        }

        // The player's own pickup, as opposed to PickupObject called by a story or a script. Listeners of
        // OnPickedItemChanged can tell them apart with IsPlayerPickup.
        void PickupByPlayer(GameObject obj, IPickable pickable)
        {
            IsPlayerPickup = true;
            try
            {
                PickupObject(obj, pickable);
            }
            finally
            {
                IsPlayerPickup = false;
            }
        }

        IEnumerator PickAfterDelay()
        {
            yield return s_pickDelay;

            delayedPickCoroutine = null;

            // An occupied pickup request must never leave a second item attached to the hand.
            if (currentPickedObject == null && attemptedPickableObject != null && attemptedPickableItem != null)
            {
                PickupByPlayer(attemptedPickableObject, attemptedPickableItem);
            }
        }

        public void PickupObject(GameObject obj, IPickable pickable)
        {
            if (obj == null)
            {
                Debug.LogError("PickupObject called with null GameObject.", this);
                return;
            }

            if (pickable == null)
            {
                Debug.LogError("PickupObject called with null IPickable.", this);
                return;
            }

            if (PickableObjectHolder == null)
            {
                Debug.LogError("PickableObjectHolder is not assigned — cannot reparent picked object. Assign a Transform in the Inspector.", this);
                return;
            }

            if (currentPickedObject == obj) return;

            // An item the bag already keeps is drawn from it, not picked up anew: it is switched back on and
            // its pickup callbacks (which belong to taking it out of the world) do not run again.
            IPickupBag bag = Bag;
            bool fromBag = bag != null && bag.Contains(obj);

            if (currentPickedObject != null) FreeHandFor(obj, pickable);
            CaptureNeutralArmPosition();

            if (fromBag && !obj.activeSelf) obj.SetActive(true);

            currentPickableItem = pickable;
            currentPickedObject = obj;

            Reparent(obj.transform, PickableObjectHolder);
            obj.transform.localPosition = pickable.GetItemOffsetLocation();
            obj.transform.localRotation = pickable.GetItemOffsetRotation();

            if (PlayerArmBase != null)
                PlayerArmBase.localPosition = neutralArmBaseLocalPosition + pickable.GetHandOffsetLocation();

            pickable.SetPickState(false);
            if (!fromBag)
            {
                pickable.Picked();
                OnItemPicked?.Invoke(pickable);
            }

            OnPickedItemChanged?.Invoke(obj);

            attemptedPickableItem = null;
            attemptedPickableObject = null;
        }

        // The hand is needed for another item. The held one is put away when the bag keeps it and has room
        // for the newcomer too; otherwise it is dropped, which is what the hand always did.
        void FreeHandFor(GameObject incoming, IPickable incomingPickable)
        {
            IPickupBag bag = Bag;
            if (bag != null
                && bag.Contains(currentPickedObject)
                && (bag.Contains(incoming) || bag.CanKeep(incoming, incomingPickable))
                && StowHeldItem())
            {
                return;
            }

            DropObject(currentPickedObject, currentPickableItem);
        }

        /// <summary>
        /// Puts the held item away into the <see cref="Bag"/> without dropping it: it is parented under the
        /// bag's stash and switched off, its drop callbacks do not run, and listeners see
        /// <see cref="OnPickedItemChanged"/>(null) with <see cref="IsStowing"/> set. Draw it again with
        /// <see cref="PickupObject"/>. Returns false when nothing is held or there is no bag.
        /// </summary>
        public bool StowHeldItem()
        {
            IPickupBag bag = Bag;
            if (bag == null || bag.StashRoot == null || currentPickedObject == null) return false;

            GameObject stowed = currentPickedObject;
            Reparent(stowed.transform, bag.StashRoot);

            if (PlayerArmBase != null)
                PlayerArmBase.localPosition = neutralArmBaseLocalPosition;

            currentPickableItem = null;
            currentPickedObject = null;

            if (delayedPickCoroutine != null)
            {
                StopCoroutine(delayedPickCoroutine);
                delayedPickCoroutine = null;
            }

            attemptedPickableItem = null;
            attemptedPickableObject = null;

            IsStowing = true;
            try
            {
                OnPickedItemChanged?.Invoke(null);
            }
            finally
            {
                IsStowing = false;
            }

            // Last, so listeners of the change above still find it in place.
            if (stowed != null) stowed.SetActive(false);
            return true;
        }

        public void DropCurrentHeldItem()
        {
            if (currentPickedObject == null && currentPickableItem == null)
            {
                Debug.LogWarning("DropCurrentHeldItem called but nothing is currently held.", this);
                return;
            }

            if (currentPickedObject == null && currentPickableItem != null)
            {
                Debug.LogWarning("currentPickableItem is set but currentPickedObject is null. Forcing cleanup.", this);
                currentPickableItem = null;
                OnPickedItemChanged?.Invoke(null);

                if (PlayerArmBase != null)
                    PlayerArmBase.localPosition = neutralArmBaseLocalPosition;

                if (delayedPickCoroutine != null)
                {
                    StopCoroutine(delayedPickCoroutine);
                    delayedPickCoroutine = null;
                }

                attemptedPickableItem = null;
                attemptedPickableObject = null;
                return;
            }

            DropObject(currentPickedObject, currentPickableItem);
        }

        void DropObject(GameObject obj, IPickable pickable)
        {
            if (obj == null)
            {
                Debug.LogWarning("DropObject called with null GameObject.", this);
                return;
            }

            if (pickable == null)
            {
                Debug.LogWarning("DropObject called with null IPickable — dropping without calling IPickable callbacks.", this);
            }

            pickable?.SetPickState(true);
            pickable?.Dropped();

            Reparent(obj.transform, null);

            if (PlayerArmBase != null)
                PlayerArmBase.localPosition = neutralArmBaseLocalPosition;

            currentPickableItem = null;
            currentPickedObject = null;

            if (delayedPickCoroutine != null)
            {
                StopCoroutine(delayedPickCoroutine);
                delayedPickCoroutine = null;
            }

            attemptedPickableItem = null;
            attemptedPickableObject = null;

            OnPickedItemChanged?.Invoke(null);
        }

        public void ForceReleaseWithoutCallbacks()
        {
            if (currentPickedObject != null)
            {
                if (currentPickableItem != null)
                    currentPickableItem.SetPickState(true);

                Reparent(currentPickedObject.transform, null);
            }

            if (PlayerArmBase != null)
                PlayerArmBase.localPosition = neutralArmBaseLocalPosition;

            currentPickableItem = null;
            currentPickedObject = null;

            if (delayedPickCoroutine != null)
            {
                StopCoroutine(delayedPickCoroutine);
                delayedPickCoroutine = null;
            }

            attemptedPickableItem = null;
            attemptedPickableObject = null;
        }

        // A hidden hand is a zero-scale bone and reparenting through it would zero the item's scale,
        // so the hands are shown for the reparent and put straight back to how they should be.
        static void Reparent(Transform item, Transform parent)
        {
            PlayerArmVisibility arm = PlayerArmVisibility.Current;
            if (arm != null) arm.RestoreScales();
            item.parent = parent;
            if (arm != null) arm.Refresh();
        }

        public bool IsHoldingItem() => currentPickedObject != null;
        public GameObject GetCurrentObject() => currentPickedObject;
        public IPickable GetCurrentPickable() => currentPickableItem;
        public GameObject GetAttemptedObject() => attemptedPickableObject;
        public IPickable GetAttemptedPickable() => attemptedPickableItem;
    }
}
