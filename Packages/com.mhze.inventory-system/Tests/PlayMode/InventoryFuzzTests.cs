using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using MHZE.InventorySystem;
using MHZE.PickupSystem;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using PickupSystemComponent = MHZE.PickupSystem.PickupSystem;

// Random play against the inventory: pickups, key presses, drops, story hand-overs, resets, destroys and slot count
// changes in any order, checking after every step that the bar, the hand and the world still agree.
namespace MHZE.InventorySystem.Tests
{
    public sealed class InventoryFuzzTests : InputTestFixture
    {
        GameObject rig;
        PickupSystemComponent pickup;
        PlayerInventory bag;
        Transform socket;
        Keyboard keyboard;
        Mouse mouse;
        Transform shelf;
        readonly List<Object> made = new List<Object>();
        readonly List<PickableItemBase> items = new List<PickableItemBase>();
        readonly StringBuilder trail = new StringBuilder();
        PickableItemData[] kinds;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
        }

        public override void TearDown()
        {
            foreach (Object o in made) if (o != null) Object.Destroy(o);
            if (rig != null) Object.Destroy(rig);
            made.Clear();
            items.Clear();
            base.TearDown();
        }

        PickableItemData Data(string name, int maxStack)
        {
            var data = ScriptableObject.CreateInstance<PickableItemData>();
            data.name = name;
            data.ItemName = name;
            data.IsPickable = true;
            data.MaxStack = maxStack;
            made.Add(data);
            return data;
        }

        void Build(int slots)
        {
            kinds = new[] { Data("Apple", 3), Data("Pear", 5), Data("Cube", 1), Data("Bowl", 1), Data("Key", 1), Data("Egg", 2) };

            rig = new GameObject("Rig");
            rig.SetActive(false);
            pickup = rig.AddComponent<PickupSystemComponent>();
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            made.Add(asset);
            InputAction drop = asset.AddActionMap("Player").AddAction("Drop", InputActionType.Button, "<Keyboard>/q");
            typeof(PickupSystemComponent).GetField("DropInputAction", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(pickup, InputActionReference.Create(drop));
            var arm = new GameObject("Arm").transform;
            arm.SetParent(rig.transform, false);
            socket = new GameObject("Socket").transform;
            socket.SetParent(arm, false);
            pickup.ConfigureHoldingRig(arm, socket);
            bag = rig.AddComponent<PlayerInventory>();
            typeof(PlayerInventory).GetField("slotCount", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(bag, slots);
            bag.DrawDelay = 0f;
            rig.SetActive(true);
            shelf = new GameObject("Shelf").transform;
            made.Add(shelf.gameObject);
        }

        PickableItemBase NewItem(int index)
        {
            var go = new GameObject("Item " + index);
            go.transform.position = new Vector3(index, 0f, 2f);
            go.AddComponent<Rigidbody>();
            go.AddComponent<BoxCollider>();
            var item = go.AddComponent<PickableItemBase>();
            item.pickableItemData = kinds[index % kinds.Length];
            made.Add(go);
            items.Add(item);
            return item;
        }

        static bool Alive(PickableItemBase item) => item != null;

        // Everything that has to be true, whatever happened before.
        void CheckInvariants(int step)
        {
            string where = "step " + step + " after: " + trail + "\n" + Describe();

            // One item at most in the hand, and it is what the pickup system says.
            int attached = 0;
            foreach (Transform child in socket) if (child.GetComponent<IPickable>() != null) attached++;
            Assert.That(attached, Is.LessThanOrEqualTo(1), "more than one item in the hand. " + where);
            GameObject held = pickup.IsHoldingItem() ? pickup.GetCurrentObject() : null;
            if (held != null) Assert.That(held.transform.parent, Is.EqualTo(socket), "held item is not on the socket. " + where);
            Assert.That(attached, Is.EqualTo(held != null ? 1 : 0), "hand and pickup system disagree. " + where);

            Inventory inv = bag.Inventory;
            var seen = new HashSet<GameObject>();
            for (int s = 0; s < inv.SlotCount; s++)
            {
                InventorySlot slot = inv[s];
                Assert.That(slot.Count, Is.LessThanOrEqualTo(Mathf.Max(1, slot.IsEmpty ? 1 : slot.Info.MaxStack)), "stack too big in slot " + (s + 1) + ". " + where);
                for (int i = 0; i < slot.Count; i++)
                {
                    GameObject go = slot[i];
                    Assert.That(go != null, Is.True, "destroyed item left in slot " + (s + 1) + ". " + where);
                    Assert.That(seen.Add(go), Is.True, go.name + " is in the bar twice. " + where);
                    var info = PlayerInventory.InfoOf(go);
                    Assert.That(info.Kind, Is.EqualTo(slot.Info.Kind), "mixed kinds in slot " + (s + 1) + ". " + where);
                    if (go == held)
                    {
                        Assert.That(go.activeSelf, Is.True, "the held item is switched off. " + where);
                    }
                    else
                    {
                        Assert.That(go.activeSelf, Is.False, go.name + " is in the bar but switched on and not in the hand. " + where);
                        Assert.That(go.transform.parent, Is.EqualTo(bag.StashRoot), go.name + " is in the bar but not in the stash. " + where);
                        Assert.That(go.GetComponent<Rigidbody>().isKinematic, Is.True, go.name + " is put away but its physics is on. " + where);
                    }
                }
            }

            // The held item is in the bar, unless the bar was full when the story forced it in.
            if (held != null && bag.SelectedIndex >= 0) Assert.That(inv.IndexOf(held), Is.EqualTo(bag.SelectedIndex), "the selected slot is not the held item's slot. " + where);
            if (held != null && bag.SelectedIndex < 0) Assert.That(inv.IndexOf(held), Is.EqualTo(-1), "an unkept item in the hand is also in the bar. " + where);

            // Nothing in the stash is outside the bar.
            foreach (Transform child in bag.StashRoot)
                Assert.That(seen.Contains(child.gameObject), Is.True, child.name + " sits in the stash but the bar does not know it. " + where);

            // With the hand free to follow (not locked), the hand matches the selected slot.
            if (!bag.IsHandLocked && bag.SelectedIndex >= 0 && pickup.isActiveAndEnabled)
            {
                InventorySlot sel = inv[bag.SelectedIndex];
                if (sel.IsEmpty) Assert.That(held == null, Is.True, "an empty slot is selected but something is in the hand. " + where);
                else Assert.That(held != null && inv.IndexOf(held) == bag.SelectedIndex, Is.True, "the selected slot has items but the hand is not holding one of them. " + where);
            }
        }

        string Describe()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < bag.SlotCount; i++)
            {
                InventorySlot s = bag.Inventory[i];
                sb.Append(i + 1).Append(':').Append(s.IsEmpty ? "-" : s.Info.Name + "x" + s.Count).Append(' ');
            }

            sb.Append("| selected=").Append(bag.SelectedIndex + 1).Append(" locked=").Append(bag.IsHandLocked);
            sb.Append(" hand=").Append(pickup.IsHoldingItem() ? pickup.GetCurrentObject().name : "-");
            return sb.ToString();
        }

        IEnumerator Fuzz(int seed, int steps, int slots)
        {
            Random.InitState(seed);
            Build(slots);
            yield return null;
            for (int i = 0; i < 14; i++) NewItem(i);
            trail.Length = 0;
            int refused = 0, blocked = 0, selections = 0, maxItems = 0, fullBars = 0, stacksSeen = 0, lockedSteps = 0, heldSteps = 0, emptySlotHand = 0, orphans = 0;
            bag.PickupRefused += go => refused++;
            bag.SelectionBlocked += () => blocked++;
            bag.SelectionChanged += i => selections++;

            for (int step = 0; step < steps; step++)
            {
                int roll = Random.Range(0, 100);
                string op;
                items.RemoveAll(i => i == null);
                PickableItemBase pick = items.Count > 0 ? items[Random.Range(0, items.Count)] : null;

                if (roll < 22 && pick != null)
                {
                    op = "pickup " + pick.name;
                    if (!bag.Holds(pick.gameObject) && pick.gameObject.activeInHierarchy) pick.Pickup();
                    else op += " (skipped: not lying in the world)";
                }
                else if (roll < 40)
                {
                    int key = Random.Range(0, 10);
                    op = "key " + (key + 1);
                    var control = new[] { keyboard.digit1Key, keyboard.digit2Key, keyboard.digit3Key, keyboard.digit4Key, keyboard.digit5Key, keyboard.digit6Key, keyboard.digit7Key, keyboard.digit8Key, keyboard.digit9Key, keyboard.digit0Key }[key];
                    Press(control);
                    yield return null;
                    Release(control);
                }
                else if (roll < 48)
                {
                    float notch = Random.value < 0.5f ? 120f : -120f;
                    op = "wheel " + notch;
                    Set(mouse.scroll, new Vector2(0f, notch));
                    yield return null;
                    Set(mouse.scroll, Vector2.zero);
                }
                else if (roll < 58 && pickup.IsHoldingItem())
                {
                    op = "drop " + pickup.GetCurrentObject().name;
                    Press(keyboard.qKey);
                    yield return null;
                    Release(keyboard.qKey);
                }
                else if (roll < 64 && pick != null)
                {
                    op = "story gives " + pick.name;
                    if (!pick.gameObject.activeSelf && Random.value < 0.5f) pick.gameObject.SetActive(true);
                    if (pick.gameObject.activeSelf || bag.Holds(pick.gameObject)) pickup.PickupObject(pick.gameObject, pick);
                    else op += " (skipped: hidden by the story)";
                }
                else if (roll < 68 && pickup.IsHoldingItem())
                {
                    op = "story takes " + pickup.GetCurrentObject().name;
                    pickup.DropCurrentHeldItem();
                }
                else if (roll < 72 && pick != null)
                {
                    // The story moves an item that is put away (a reset), sometimes hiding it afterwards.
                    op = "story moves " + pick.name;
                    if (bag.Holds(pick.gameObject) && pick.gameObject != pickup.GetCurrentObject())
                    {
                        pick.transform.SetParent(shelf, true);
                        if (Random.value < 0.4f) pick.gameObject.SetActive(false);
                        op += " (and hides)";
                    }
                }
                else if (roll < 74 && pick != null)
                {
                    op = "destroy " + pick.name;
                    Object.Destroy(pick.gameObject);
                    items.Remove(pick);
                    yield return null;
                }
                else if (roll < 78)
                {
                    int count = Random.Range(1, 9);
                    op = "slots " + count;
                    bag.SetSlotCount(count);
                }
                else if (roll < 80)
                {
                    op = "pause";
                    pickup.enabled = false;
                    yield return null;
                    pickup.enabled = true;
                }
                else if (roll < 81)
                {
                    op = "release all";
                    bag.ReleaseAll();
                }
                else if (roll < 83)
                {
                    op = "spawn";
                    if (items.Count < 20) NewItem(items.Count);
                }
                else
                {
                    op = "idle";
                }

                if (trail.Length > 600) trail.Remove(0, trail.Length - 400);
                trail.Append(op).Append("; ");
                yield return null;
                // A hidden-by-story item is not ours to switch on again, and an item the story hid and left in the world
                // is then neither in the bar nor active: that is the story's business, not an invariant.
                CheckInvariants(step);
                int total = 0; bool anyStack = false, anyFree = false;
                for (int s = 0; s < bag.SlotCount; s++) { total += bag.Inventory[s].Count; if (bag.Inventory[s].Count > 1) anyStack = true; if (bag.Inventory[s].IsEmpty) anyFree = true; }
                if (total > maxItems) maxItems = total;
                if (!anyFree) fullBars++;
                if (anyStack) stacksSeen++;
                if (bag.IsHandLocked) lockedSteps++;
                if (pickup.IsHoldingItem()) heldSteps++;
                if (!pickup.IsHoldingItem() && bag.SelectedIndex >= 0 && bag.Inventory[bag.SelectedIndex].IsEmpty) emptySlotHand++;
                if (bag.SelectedIndex < 0) orphans++;
            }

            Debug.Log("FUZZ seed " + seed + ": steps=" + steps + " refusedPickups=" + refused + " blockedSwitches=" + blocked + " selectionChanges=" + selections + " maxItemsInBar=" + maxItems + " stepsWithFullBar=" + fullBars + " stepsWithAStack=" + stacksSeen + " stepsLocked=" + lockedSteps + " stepsHolding=" + heldSteps + " stepsEmptySlotSelected=" + emptySlotHand + " orphanSteps=" + orphans);
        }

        [UnityTest]
        public IEnumerator RandomPlayKeepsTheBarTheHandAndTheWorldInStep([ValueSource(nameof(Seeds))] int seed)
        {
            yield return Fuzz(seed, 1500, slots: 6);
        }

        [UnityTest]
        public IEnumerator RandomPlayWithAFewSlots()
        {
            yield return Fuzz(777, 1200, slots: 3);
        }

        static int[] Seeds = { 1, 2, 3, 4 };
    }
}
