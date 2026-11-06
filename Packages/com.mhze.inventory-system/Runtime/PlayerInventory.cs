using System;
using MHZE.PickupSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using PickupSystemComponent = MHZE.PickupSystem.PickupSystem;

namespace MHZE.InventorySystem
{
    /// <summary>
    /// A hotbar behind the player's hand. Every item the player picks up takes a slot (or stacks onto a slot of
    /// the same kind), the selected slot's item is the one in the hand, and the number keys or the scroll wheel
    /// change the selection. Selecting an empty slot leaves the hand empty. It sits on top of
    /// <see cref="PickupSystemComponent"/>: whatever the hand takes or lets go of, from the player or from the
    /// story, the bar follows; items that are put away wait switched off in a stash under this object.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)]
    [AddComponentMenu("MHZE/Inventory/Player Inventory")]
    public sealed class PlayerInventory : MonoBehaviour, IPickupBag
    {
        const string StashName = "Inventory Stash";

        /// <summary>The player's inventory in the loaded scene, if there is one.</summary>
        public static PlayerInventory Instance { get; private set; }

        [Header("Slots")]
        [Tooltip("How many slots the bar has. It can be changed while playing.")]
        [SerializeField, Range(1, Inventory.MaxSlotCount)] int slotCount = 6;

        [Header("Switching")]
        [Tooltip("Keys 1 to 9 and 0 select the first ten slots.")]
        [SerializeField] bool numberKeys = true;
        [SerializeField] bool scrollWheel = true;
        [Tooltip("Scrolling up selects the previous slot. Turn on to reverse it.")]
        [SerializeField] bool invertScroll;
        [Tooltip("Scrolling past the last slot comes back to the first, and the other way round.")]
        [SerializeField] bool wrapAround = true;
        [Tooltip("Seconds between putting an item away and drawing the next, so the arm is seen lowering and raising.")]
        [SerializeField, Min(0f)] float drawDelay = 0.12f;

        [Header("References")]
        [Tooltip("Found automatically when empty.")]
        [SerializeField] PickupSystemComponent pickupSystem;
        [Tooltip("Builds the bar on its own screen-space canvas when no view is assigned.")]
        [SerializeField] bool createBar = true;
        [SerializeField] InventoryBarView barView;

        Inventory inventory;
        PickupSystemComponent linked;
        Transform stash;
        int selected;
        int lastSelected;
        GameObject handItem;
        GameObject lockedItem;
        bool drawing;
        float handChangedAt;
        float scrollAccumulator;
        float nextLinkTry;

        /// <summary>The selection changed. The argument is the new slot, or -1 when the hand holds something the bar does not keep.</summary>
        public event Action<int> SelectionChanged;

        /// <summary>What a slot holds changed.</summary>
        public event Action<int> SlotChanged;

        /// <summary>The number of slots changed.</summary>
        public event Action SlotCountChanged;

        /// <summary>The player tried to pick an item up but every slot is taken.</summary>
        public event Action<GameObject> PickupRefused;

        /// <summary>The player tried to switch away from an item the story put in the hand, which has to stay there.</summary>
        public event Action SelectionBlocked;

        /// <summary>
        /// The player asked for the slot that is already selected (pressed its key, or scrolled against the end of a bar
        /// that does not wrap). Nothing changed; a bar that hides itself comes back for it.
        /// </summary>
        public event Action SelectionRepeated;

        /// <summary>The slots and their content.</summary>
        public Inventory Inventory
        {
            get
            {
                Initialize();
                return inventory;
            }
        }

        public int SlotCount => inventory != null ? inventory.SlotCount : slotCount;

        /// <summary>The selected slot, or -1 when the hand holds something the bar does not keep.</summary>
        public int SelectedIndex => selected;

        /// <summary>The item in the hand, if any.</summary>
        public GameObject HeldItem => handItem;

        /// <summary>
        /// The item in the hand has to stay there until it is released: the story or a script put it there (flowers at
        /// the door, a brush, a torch, a cat to carry) rather than the player picking it up, or it says so itself
        /// (<see cref="IKeepInHand"/>). The keys and the wheel do nothing and the player's pickups are refused
        /// meanwhile. Dropping it with the drop key ends this.
        /// </summary>
        public bool IsHandLocked
        {
            get
            {
                if (handItem == null) return false;
                if (lockedItem != null && lockedItem == handItem) return true;
                return handItem.TryGetComponent(out IKeepInHand keep) && keep.MustStayInHand;
            }
        }

        /// <summary>Where put-away items wait, switched off.</summary>
        public Transform StashRoot => GetOrCreateStash();

        /// <summary>Seconds between putting an item away and drawing the next one.</summary>
        public float DrawDelay
        {
            get => drawDelay;
            set => drawDelay = Mathf.Max(0f, value);
        }

        /// <summary>Master switch for the keys and the wheel. The inventory still follows the hand while off.</summary>
        public bool InputEnabled { get; set; } = true;

        /// <summary>
        /// Optional extra condition for the keys and the wheel, asked every frame (for example "the player has control").
        /// Input is also off while the pickup system itself is disabled, which is what pausing does.
        /// </summary>
        public Func<bool> InputGate { get; set; }

        // ───────────────────────────── lifecycle ─────────────────────────────

        void Awake()
        {
            // Fast Play keeps non-serialized fields from the previous run: start from nothing.
            ResetState();

            if (barView == null) barView = GetComponent<InventoryBarView>();
            if (barView == null && createBar && Application.isPlaying) barView = gameObject.AddComponent<InventoryBarView>();
        }

        void OnEnable()
        {
            Initialize();
            Instance = this;
            Link();
        }

        void OnDisable()
        {
            Unlink();
            if (Instance == this) Instance = null;
        }

        void OnDestroy()
        {
            Unlink();
            if (Instance == this) Instance = null;
        }

        void OnValidate()
        {
            if (Application.isPlaying && inventory != null) SetSlotCount(slotCount);
        }

        void Update()
        {
            // The pickup system may appear after this component does; look again now and then, not every frame.
            if (linked == null && Time.unscaledTime >= nextLinkTry)
            {
                nextLinkTry = Time.unscaledTime + 0.5f;
                Link();
            }

            Tick();
        }

        /// <summary>
        /// One step of the inventory: tidy up, read the keys and the wheel, bring the hand in line with the
        /// selection. <c>Update</c> calls it every frame; it is public so tools and tests can step by hand.
        /// </summary>
        public void Tick()
        {
            if (inventory == null || linked == null) return;

            Housekeeping();
            if (InputAllowed) ReadInput();
            SyncHand();
        }

        void ResetState()
        {
            inventory = null;
            InputEnabled = true;
            Initialize();

            // A bar that was already listening starts over with the new, empty slots.
            SlotCountChanged?.Invoke();
        }

        /// <summary>Creates the slots. Safe to call again; it only does anything once.</summary>
        public void Initialize()
        {
            if (inventory != null) return;

            inventory = new Inventory(slotCount);
            inventory.SlotChanged += index => SlotChanged?.Invoke(index);
            inventory.SlotCountChanged += () => SlotCountChanged?.Invoke();
            selected = 0;
            lastSelected = 0;
            handItem = null;
            lockedItem = null;
            drawing = false;
            handChangedAt = -999f;
            scrollAccumulator = 0f;
        }

        // A root object of the player's scene rather than a child of the player: put-away items belong to the story
        // as much as to the player, so they must not be destroyed or moved with the player's rig.
        Transform GetOrCreateStash()
        {
            if (stash != null) return stash;

            var holder = new GameObject(StashName);
            if (gameObject.scene.IsValid() && gameObject.scene.isLoaded) SceneManager.MoveGameObjectToScene(holder, gameObject.scene);
            holder.AddComponent<InventoryStash>().ChildrenChanged = ReclaimItems;
            stash = holder.transform;
            return stash;
        }

        void Link()
        {
            PickupSystemComponent found = pickupSystem;
            if (found == null) found = GetComponentInParent<PickupSystemComponent>(true);
            if (found == null) found = PickupSystemComponent.Instance;
            if (found == null) found = FindAnyObjectByType<PickupSystemComponent>();
            if (found == null || found == linked) return;

            Unlink();
            linked = found;
            linked.Bag = this;
            linked.OnPickedItemChanged += HandleHandChanged;
            linked.OnPickRefused += HandleRefused;

            // Something may already be in the hand (the story gives items before this component wakes up).
            GameObject inHand = linked.IsHoldingItem() ? linked.GetCurrentObject() : null;
            if (inHand != null) HandleHandChanged(inHand);
        }

        void Unlink()
        {
            if (linked == null) return;

            linked.OnPickedItemChanged -= HandleHandChanged;
            linked.OnPickRefused -= HandleRefused;
            if (ReferenceEquals(linked.Bag, this)) linked.Bag = null;
            linked = null;
        }

        // ───────────────────────────── public API ─────────────────────────────

        /// <summary>Changes the number of slots (1 to 30), also while playing. Items in slots that are cut off move to free slots, or fall to the floor.</summary>
        public void SetSlotCount(int count)
        {
            count = Mathf.Clamp(count, 1, Inventory.MaxSlotCount);
            slotCount = count;
            if (inventory == null || count == inventory.SlotCount) return;

            var displaced = inventory.Resize(count);
            foreach (var cut in displaced)
            {
                if (inventory.Add(cut.Key, cut.Value) >= 0) continue;

                // No free slot left for it: the hand lets go through the pickup system, anything else just falls.
                if (cut.Key == handItem && linked != null) linked.DropCurrentHeldItem();
                else DropToWorld(cut.Key);
            }

            int heldSlot = handItem != null ? inventory.IndexOf(handItem) : -1;
            if (heldSlot >= 0) SetSelected(heldSlot);
            else if (selected >= count) SetSelected(count - 1);
            else if (lastSelected >= count) lastSelected = count - 1;
        }

        /// <summary>
        /// Selects a slot; the hand follows. Returns false for a slot that does not exist, one that is already
        /// selected (<see cref="SelectionRepeated"/> is raised), or while the hand holds something the story put there
        /// (<see cref="IsHandLocked"/>).
        /// </summary>
        public bool Select(int index)
        {
            if (inventory == null || index < 0 || index >= inventory.SlotCount) return false;

            if (index == selected)
            {
                SelectionRepeated?.Invoke();
                return false;
            }

            if (IsHandLocked)
            {
                SelectionBlocked?.Invoke();
                return false;
            }

            SetSelected(index);
            return true;
        }

        public bool SelectNext() => Step(1);

        public bool SelectPrevious() => Step(-1);

        /// <summary>The player's inventory holds this object, in the hand or put away.</summary>
        public bool Holds(GameObject item) => inventory != null && inventory.IndexOf(item) >= 0;

        /// <summary>
        /// Gives every put-away item back to the world, in front of the player, and empties the bar. The item in
        /// the hand is dropped too. Returns how many items were released.
        /// </summary>
        public int ReleaseAll()
        {
            if (inventory == null) return 0;

            int released = 0;
            if (linked != null && linked.IsHoldingItem() && inventory.IndexOf(linked.GetCurrentObject()) >= 0)
            {
                linked.DropCurrentHeldItem();
                released++;
            }

            released += ReleaseStowedItems();
            return released;
        }

        // ───────────────────────────── the hand ─────────────────────────────

        // The pickup system changed what is in the hand: a pickup (by the player or the story), a drop, a throw, a
        // hand-over, an item used up, or one of our own switches.
        void HandleHandChanged(GameObject now)
        {
            GameObject previous = handItem;
            handItem = now;
            handChangedAt = Time.unscaledTime;

            if (now == null)
            {
                lockedItem = null;

                // Put away by the bar itself: the item keeps its slot.
                if (linked != null && linked.IsStowing) return;

                // Dropped, thrown, given away or used up: it leaves the bar.
                if (previous != null) inventory.Remove(previous);
                if (selected < 0) SetSelected(Mathf.Clamp(lastSelected, 0, inventory.SlotCount - 1));
                return;
            }

            int slot = inventory.IndexOf(now);
            if (slot < 0) slot = inventory.Add(now, InfoOf(now), PreferredEmptySlot());

            // What the player picked up, or the bar drew itself, can be switched away. What the story put in the
            // hand cannot: it expects the item there (a brush to paint with, a torch, a cat in his arms).
            lockedItem = linked != null && (linked.IsPlayerPickup || drawing) ? null : now;
            SetSelected(slot);
        }

        void HandleRefused(GameObject item)
        {
            PickupRefused?.Invoke(item);
        }

        // Makes the hand match the selection. It is state based, so a switch, a drop, a story hand-over and a lost
        // item all end up here the same way, once per frame at most.
        void SyncHand()
        {
            if (selected < 0 || linked.PickableObjectHolder == null || IsHandLocked) return;

            GameObject held = linked.IsHoldingItem() ? linked.GetCurrentObject() : null;
            if (held != null)
            {
                int heldSlot = inventory.IndexOf(held);
                if (heldSlot == selected) return;

                if (heldSlot >= 0) linked.StowHeldItem();
                else linked.DropCurrentHeldItem();
                if (linked.IsHoldingItem()) return;
            }

            InventorySlot slot = inventory[selected];
            if (slot.IsEmpty || Time.unscaledTime - handChangedAt < drawDelay) return;

            GameObject next = slot.FirstItem();
            IPickable pickable = next != null ? next.GetComponent<IPickable>() : null;
            if (pickable == null)
            {
                if (next != null) inventory.Remove(next);
                return;
            }

            drawing = true;
            try
            {
                linked.PickupObject(next, pickable);
            }
            finally
            {
                drawing = false;
            }

            if (linked.GetCurrentObject() != next) handChangedAt = Time.unscaledTime + 1f;
        }

        // Things the story and the world do behind the bar's back.
        void Housekeeping()
        {
            // An item destroyed in the hand raises no event.
            if (!ReferenceEquals(handItem, null) && handItem == null)
            {
                handItem = null;
                handChangedAt = Time.unscaledTime;
            }

            // The hand changed without an event (a release without callbacks).
            GameObject current = linked.IsHoldingItem() ? linked.GetCurrentObject() : null;
            if (current != handItem) HandleHandChanged(current);

            inventory.RemoveDestroyed();
            ReclaimItems();
        }

        // An item the story took back out of the stash (a rewind, a reset) is no longer ours: it leaves the bar and
        // is switched on again, as it was before it was put away. The stash calls this the moment an item is moved,
        // so a story that hides the item right after moving it still gets the last word.
        void ReclaimItems()
        {
            if (inventory == null || stash == null) return;

            GameObject inHand = linked != null && linked.IsHoldingItem() ? linked.GetCurrentObject() : null;
            for (int s = 0; s < inventory.SlotCount; s++)
            {
                InventorySlot slot = inventory[s];
                for (int i = slot.Count - 1; i >= 0; i--)
                {
                    GameObject item = slot[i];
                    if (item == null || item == handItem || item == inHand || item.transform.parent == stash) continue;

                    inventory.Remove(item);
                    if (!item.activeSelf) item.SetActive(true);
                }
            }
        }

        // ───────────────────────────── input ─────────────────────────────

        bool InputAllowed =>
            InputEnabled
            && linked != null
            && linked.isActiveAndEnabled
            && (InputGate == null || InputGate());

        void ReadInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (numberKeys && keyboard != null)
            {
                int keys = Mathf.Min(inventory.SlotCount, 10);
                for (int i = 0; i < keys; i++)
                {
                    if (keyboard[KeyForSlot(i)].wasPressedThisFrame) Select(i);
                }
            }

            Mouse mouse = Mouse.current;
            if (scrollWheel && mouse != null) ReadScroll(mouse.scroll.ReadValue().y);
        }

        void ReadScroll(float y)
        {
            if (invertScroll) y = -y;
            if (Mathf.Approximately(y, 0f)) return;

            // A wheel notch arrives as one big step (1 or 120 depending on the platform), a trackpad as many small
            // ones that add up. Either moves the selection one slot.
            if (Mathf.Abs(y) >= 1f)
            {
                scrollAccumulator = 0f;
                Step(y > 0f ? -1 : 1);
                return;
            }

            scrollAccumulator += y;
            if (Mathf.Abs(scrollAccumulator) < 1f) return;

            Step(scrollAccumulator > 0f ? -1 : 1);
            scrollAccumulator = 0f;
        }

        bool Step(int delta)
        {
            if (inventory == null) return false;

            int count = inventory.SlotCount;
            int from = selected >= 0 ? selected : lastSelected;
            int to = from + delta;
            to = wrapAround ? ((to % count) + count) % count : Mathf.Clamp(to, 0, count - 1);
            return Select(to);
        }

        static Key KeyForSlot(int index) => index == 9 ? Key.Digit0 : Key.Digit1 + index;

        // ───────────────────────────── bag contract ─────────────────────────────

        bool IPickupBag.Contains(GameObject item) => Holds(item);

        bool IPickupBag.CanKeep(GameObject item, IPickable pickable)
        {
            Initialize();
            return item != null && inventory.CanAdd(InfoOf(item));
        }

        // ───────────────────────────── helpers ─────────────────────────────

        void SetSelected(int index)
        {
            if (index == selected) return;

            selected = index;
            if (index >= 0) lastSelected = index;
            SelectionChanged?.Invoke(index);
        }

        int PreferredEmptySlot()
        {
            return selected >= 0 && selected < inventory.SlotCount && inventory[selected].IsEmpty ? selected : -1;
        }

        int ReleaseStowedItems()
        {
            int released = 0;
            for (int s = 0; s < inventory.SlotCount; s++)
            {
                InventorySlot slot = inventory[s];
                for (int i = slot.Count - 1; i >= 0; i--)
                {
                    GameObject item = slot[i];
                    if (item == null || item == handItem) continue;

                    inventory.Remove(item);
                    DropToWorld(item);
                    released++;
                }
            }

            return released;
        }

        // Puts an item that was away back into the world, just in front of the player, with physics on.
        void DropToWorld(GameObject item)
        {
            if (item == null) return;

            Camera view = Camera.main;
            Transform reference = view != null ? view.transform : transform;
            Vector3 position = reference.position + reference.forward * 0.7f + UnityEngine.Random.insideUnitSphere * 0.12f;

            item.SetActive(true);
            item.transform.SetParent(null, true);
            item.transform.SetPositionAndRotation(position, UnityEngine.Random.rotation);

            IPickable pickable = item.GetComponent<IPickable>();
            if (pickable != null)
            {
                pickable.SetPickState(true);
                pickable.Dropped();
            }
        }

        /// <summary>How the inventory shows and stacks an object.</summary>
        public static InventoryItemInfo InfoOf(GameObject item)
        {
            if (item.TryGetComponent(out IInventoryItem custom)) return custom.GetInventoryInfo();

            if (item.TryGetComponent(out PickableItemBase pickable) && pickable.pickableItemData != null)
            {
                PickableItemData data = pickable.pickableItemData;
                string label = string.IsNullOrWhiteSpace(data.ItemName) ? item.name : data.ItemName;
                return new InventoryItemInfo(data, label, data.ItemIcon, data.MaxStack);
            }

            // Nothing says what it is: a unique object, shown by its name.
            return new InventoryItemInfo(item, item.name, null, 1);
        }
    }
}
