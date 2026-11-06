using System;
using System.Collections.Generic;
using UnityEngine;

namespace MHZE.InventorySystem
{
    /// <summary>What a slot needs to know about an item: which kind it is, how it is shown and how many share a slot.</summary>
    public readonly struct InventoryItemInfo
    {
        public InventoryItemInfo(UnityEngine.Object kind, string name, Sprite icon, int maxStack)
        {
            Kind = kind;
            Name = name ?? string.Empty;
            Icon = icon;
            MaxStack = Mathf.Max(1, maxStack);
        }

        /// <summary>Items stack when they share a kind (normally the item data asset). Different kinds never do.</summary>
        public UnityEngine.Object Kind { get; }

        /// <summary>Shown in the slot while the item has no icon.</summary>
        public string Name { get; }

        /// <summary>Shown in the slot; null falls back to the name.</summary>
        public Sprite Icon { get; }

        /// <summary>How many of this kind fit one slot. 1 means the kind does not stack.</summary>
        public int MaxStack { get; }
    }

    /// <summary>One slot of an <see cref="Inventory"/>: a stack of items of the same kind, possibly empty.</summary>
    public sealed class InventorySlot
    {
        internal readonly List<GameObject> items = new List<GameObject>();

        /// <summary>The kind, name, icon and stack size of what the slot holds. Default while the slot is empty.</summary>
        public InventoryItemInfo Info { get; internal set; }

        public int Count => items.Count;
        public bool IsEmpty => items.Count == 0;
        public GameObject this[int index] => items[index];

        public bool Contains(GameObject item) => items.Contains(item);

        /// <summary>The first item of the stack that still exists, or null.</summary>
        public GameObject FirstItem()
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null) return items[i];
            }

            return null;
        }
    }

    /// <summary>
    /// The slots of an inventory and the rules for filling them: an item stacks onto a slot of the same kind
    /// that has room, otherwise it takes an empty slot, otherwise it does not fit. It knows nothing about
    /// hands, input or UI.
    /// </summary>
    public sealed class Inventory
    {
        public const int MaxSlotCount = 30;

        readonly List<InventorySlot> slots = new List<InventorySlot>();

        /// <summary>The content of one slot changed (an item came, went or was lost).</summary>
        public event Action<int> SlotChanged;

        /// <summary>Slots were added or removed.</summary>
        public event Action SlotCountChanged;

        public Inventory(int slotCount)
        {
            slotCount = Mathf.Clamp(slotCount, 1, MaxSlotCount);
            for (int i = 0; i < slotCount; i++) slots.Add(new InventorySlot());
        }

        public int SlotCount => slots.Count;
        public InventorySlot this[int index] => slots[index];

        /// <summary>The slot an item is in, or -1.</summary>
        public int IndexOf(GameObject item)
        {
            if (item == null) return -1;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].items.Contains(item)) return i;
            }

            return -1;
        }

        /// <summary>A slot of the same kind with room for one more, the preferred one first. -1 when there is none.</summary>
        public int FindStackWithRoom(InventoryItemInfo info, int preferredSlot = -1)
        {
            if (HasRoomFor(preferredSlot, info)) return preferredSlot;
            for (int i = 0; i < slots.Count; i++)
            {
                if (HasRoomFor(i, info)) return i;
            }

            return -1;
        }

        /// <summary>An empty slot, the preferred one first. -1 when every slot is taken.</summary>
        public int FindEmptySlot(int preferredSlot = -1)
        {
            if (preferredSlot >= 0 && preferredSlot < slots.Count && slots[preferredSlot].IsEmpty) return preferredSlot;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].IsEmpty) return i;
            }

            return -1;
        }

        /// <summary>Whether an item of this kind would fit: onto a stack with room, or into an empty slot.</summary>
        public bool CanAdd(InventoryItemInfo info) => FindStackWithRoom(info) >= 0 || FindEmptySlot() >= 0;

        /// <summary>
        /// Puts the item in the inventory: onto a stack of its kind with room, else into an empty slot (the
        /// preferred one if it is empty). Returns the slot, or -1 when it does not fit. An item that is
        /// already inside stays where it is.
        /// </summary>
        public int Add(GameObject item, InventoryItemInfo info, int preferredSlot = -1)
        {
            if (item == null) return -1;

            int existing = IndexOf(item);
            if (existing >= 0) return existing;

            int index = FindStackWithRoom(info, preferredSlot);
            if (index < 0) index = FindEmptySlot(preferredSlot);
            if (index < 0) return -1;

            InventorySlot slot = slots[index];
            slot.Info = info;
            slot.items.Add(item);
            SlotChanged?.Invoke(index);
            return index;
        }

        /// <summary>Takes the item out of its slot. Returns the slot it was in, or -1 when it was not inside.</summary>
        public int Remove(GameObject item)
        {
            int index = IndexOf(item);
            if (index < 0) return -1;

            InventorySlot slot = slots[index];
            slot.items.Remove(item);
            if (slot.IsEmpty) slot.Info = default;
            SlotChanged?.Invoke(index);
            return index;
        }

        /// <summary>Forgets items that were destroyed. Returns how many.</summary>
        public int RemoveDestroyed()
        {
            int removed = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlot slot = slots[i];
                int gone = slot.items.RemoveAll(item => item == null);
                if (gone == 0) continue;

                removed += gone;
                if (slot.IsEmpty) slot.Info = default;
                SlotChanged?.Invoke(i);
            }

            return removed;
        }

        /// <summary>
        /// Gives the inventory a new number of slots (1 to <see cref="MaxSlotCount"/>). Slots that are cut off
        /// are returned with what they held so the caller can place it elsewhere.
        /// </summary>
        public List<KeyValuePair<GameObject, InventoryItemInfo>> Resize(int slotCount)
        {
            slotCount = Mathf.Clamp(slotCount, 1, MaxSlotCount);
            var displaced = new List<KeyValuePair<GameObject, InventoryItemInfo>>();
            if (slotCount == slots.Count) return displaced;

            while (slots.Count < slotCount) slots.Add(new InventorySlot());

            while (slots.Count > slotCount)
            {
                InventorySlot cut = slots[slots.Count - 1];
                foreach (GameObject item in cut.items)
                {
                    if (item != null) displaced.Add(new KeyValuePair<GameObject, InventoryItemInfo>(item, cut.Info));
                }

                slots.RemoveAt(slots.Count - 1);
            }

            SlotCountChanged?.Invoke();
            return displaced;
        }

        bool HasRoomFor(int index, InventoryItemInfo info)
        {
            if (index < 0 || index >= slots.Count) return false;

            InventorySlot slot = slots[index];
            return !slot.IsEmpty
                && slot.Info.Kind != null
                && slot.Info.Kind == info.Kind
                && slot.Count < info.MaxStack;
        }
    }
}
