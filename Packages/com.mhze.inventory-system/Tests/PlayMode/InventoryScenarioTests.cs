using System.Collections;
using System.Reflection;
using MHZE.InventorySystem;
using MHZE.PickupSystem;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PickupSystemComponent = MHZE.PickupSystem.PickupSystem;

// Play mode scenarios for the inventory: the real Update loop, real (virtual-device) keyboard and mouse and the real
// bar UI, with several kinds of items.
namespace MHZE.InventorySystem.Tests
{
    public sealed class InventoryScenarioTests : InputTestFixture
    {

        GameObject rig;
        PickupSystemComponent pickup;
        PlayerInventory bag;
        Transform socket;
        Keyboard keyboard;
        Mouse mouse;
        readonly System.Collections.Generic.List<Object> made = new System.Collections.Generic.List<Object>();

        PickableItemData apple, pear, cube, bowl;

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
            base.TearDown();
        }

        // ─────────────── building blocks ───────────────

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

        void BuildRig(int slots = 6, float drawDelay = 0f)
        {
            apple = Data("Apple", 3);
            pear = Data("Pear", 5);
            cube = Data("Test Pickup Cube", 1);
            bowl = Data("Bowl of soup", 1);

            rig = new GameObject("Rig");
            rig.SetActive(false);
            pickup = rig.AddComponent<PickupSystemComponent>();

            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            made.Add(asset);
            InputAction drop = asset.AddActionMap("Player").AddAction("Drop", InputActionType.Button, "<Keyboard>/q");
            typeof(PickupSystemComponent).GetField("DropInputAction", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(pickup, InputActionReference.Create(drop));

            var arm = new GameObject("Arm").transform;
            arm.SetParent(rig.transform, false);
            socket = new GameObject("Socket").transform;
            socket.SetParent(arm, false);
            pickup.ConfigureHoldingRig(arm, socket);

            bag = rig.AddComponent<PlayerInventory>();
            typeof(PlayerInventory).GetField("slotCount", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(bag, slots);
            bag.DrawDelay = drawDelay;
            rig.SetActive(true);
        }

        PickableItemBase Item(string name, PickableItemData data, float x = 0f)
        {
            var go = new GameObject(name);
            go.transform.position = new Vector3(x, 0f, 2f);
            go.AddComponent<Rigidbody>();
            go.AddComponent<BoxCollider>();
            var item = go.AddComponent<PickableItemBase>();
            item.pickableItemData = data;
            made.Add(go);
            return item;
        }

        IEnumerator PressKey(UnityEngine.InputSystem.Controls.KeyControl key)
        {
            Press(key);
            yield return null;
            Release(key);
            yield return null;
        }

        IEnumerator Scroll(float amount)
        {
            Set(mouse.scroll, new Vector2(0f, amount));
            yield return null;
            Set(mouse.scroll, Vector2.zero);
            yield return null;
        }

        Transform SlotRoot(int index) => bag.GetComponentInChildren<InventoryBarView>(true).transform.Find("Inventory Bar/Bar/Slot " + (index + 1));

        string SlotText(int index, string child)
        {
            Transform t = SlotRoot(index).Find(child);
            return t != null && t.gameObject.activeSelf ? t.GetComponent<TextMeshProUGUI>().text : "";
        }

        static string Describe(PlayerInventory b)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < b.SlotCount; i++)
            {
                InventorySlot s = b.Inventory[i];
                sb.Append(i + 1).Append(':').Append(s.IsEmpty ? "-" : s.Info.Name + "x" + s.Count).Append(' ');
            }
            sb.Append("| selected=").Append(b.SelectedIndex + 1);
            return sb.ToString();
        }

        // ─────────────── scenarios ───────────────

        [UnityTest]
        public IEnumerator PickingUpDifferentItemsFillsTheBarAndStacksTheSameKind()
        {
            BuildRig();
            yield return null;

            PickableItemBase cubeItem = Item("Cube", cube);
            PickableItemBase bowlItem = Item("Bowl", bowl);
            PickableItemBase apple1 = Item("Apple 1", apple), apple2 = Item("Apple 2", apple), apple3 = Item("Apple 3", apple);
            PickableItemBase pear1 = Item("Pear 1", pear);

            cubeItem.Pickup();
            yield return null;
            Assert.That(bag.Inventory.IndexOf(cubeItem.gameObject), Is.EqualTo(0), Describe(bag));
            Assert.That(pickup.GetCurrentObject(), Is.EqualTo(cubeItem.gameObject));

            bowlItem.Pickup();
            yield return null;
            Assert.That(bag.Inventory.IndexOf(bowlItem.gameObject), Is.EqualTo(1), Describe(bag));
            Assert.That(cubeItem.gameObject.activeSelf, Is.False, "the cube was put away");
            Assert.That(pickup.GetCurrentObject(), Is.EqualTo(bowlItem.gameObject));

            apple1.Pickup(); yield return null;
            apple2.Pickup(); yield return null;
            apple3.Pickup(); yield return null;
            Assert.That(bag.Inventory[2].Count, Is.EqualTo(3), Describe(bag));
            Assert.That(SlotText(2, "Count"), Is.EqualTo("3"));
            Assert.That(SlotText(2, "Name"), Is.EqualTo("Apple"));

            pear1.Pickup(); yield return null;
            Assert.That(bag.Inventory.IndexOf(pear1.gameObject), Is.EqualTo(3), Describe(bag));
            Assert.That(SlotText(3, "Count"), Is.EqualTo(""), "a single item shows no number");
            Debug.Log("BAR after pickups: " + Describe(bag));
            yield return new WaitForSeconds(0.5f);

            // Number keys: the hand follows, exactly one item in it each time.
            yield return PressKey(keyboard.digit1Key);
            Assert.That(bag.SelectedIndex, Is.EqualTo(0));
            Assert.That(pickup.GetCurrentObject(), Is.EqualTo(cubeItem.gameObject));
            Assert.That(socket.childCount, Is.EqualTo(1));

            yield return PressKey(keyboard.digit3Key);
            Assert.That(bag.SelectedIndex, Is.EqualTo(2));
            Assert.That(bag.Inventory.IndexOf(pickup.GetCurrentObject()), Is.EqualTo(2), "an apple is in the hand");
            Assert.That(socket.childCount, Is.EqualTo(1));

            // An empty slot, by key and by wheel.
            yield return PressKey(keyboard.digit6Key);
            Assert.That(bag.SelectedIndex, Is.EqualTo(5));
            Assert.That(pickup.IsHoldingItem(), Is.False, "an empty slot leaves the hand empty");
            Assert.That(socket.childCount, Is.EqualTo(0));

            yield return Scroll(120f);   // up: previous slot
            Assert.That(bag.SelectedIndex, Is.EqualTo(4));
            yield return Scroll(120f);
            Assert.That(bag.SelectedIndex, Is.EqualTo(3));
            Assert.That(pickup.GetCurrentObject(), Is.EqualTo(pear1.gameObject));
            yield return Scroll(-120f);  // down: next slot
            Assert.That(bag.SelectedIndex, Is.EqualTo(4));

            // The wheel wraps.
            yield return PressKey(keyboard.digit6Key);
            yield return Scroll(-120f);
            Assert.That(bag.SelectedIndex, Is.EqualTo(0), "after the last slot comes the first");
            yield return Scroll(120f);
            Assert.That(bag.SelectedIndex, Is.EqualTo(5), "before the first slot comes the last");

            // Picking up with an empty slot selected fills that slot.
            PickableItemBase late = Item("Late cube", cube);
            late.Pickup();
            yield return null;
            Assert.That(bag.Inventory.IndexOf(late.gameObject), Is.EqualTo(5), Describe(bag));
            Assert.That(pickup.GetCurrentObject(), Is.EqualTo(late.gameObject));

            // Drop (the Q key): one apple leaves the stack and the next apple is drawn.
            yield return PressKey(keyboard.digit3Key);
            GameObject heldApple = pickup.GetCurrentObject();
            yield return PressKey(keyboard.qKey);
            yield return new WaitForSeconds(0.1f);
            Assert.That(bag.Inventory[2].Count, Is.EqualTo(2), Describe(bag));
            Assert.That(heldApple.transform.parent, Is.Null, "the dropped apple is in the world");
            Assert.That(heldApple.GetComponent<Rigidbody>().isKinematic, Is.False);
            Assert.That(pickup.IsHoldingItem(), Is.True, "the next apple is in the hand");
            Assert.That(bag.Inventory.IndexOf(pickup.GetCurrentObject()), Is.EqualTo(2));
            Assert.That(SlotText(2, "Count"), Is.EqualTo("2"));

            // The dropped apple can be picked up again and rejoins the stack.
            heldApple.GetComponent<PickableItemBase>().Pickup();
            yield return null;
            Assert.That(bag.Inventory[2].Count, Is.EqualTo(3), Describe(bag));

            Debug.Log("BAR end: " + Describe(bag));
        }

        [UnityTest]
        public IEnumerator AFullBarRefusesAndSaysSo()
        {
            BuildRig(slots: 3);
            yield return null;
            Item("A", cube).Pickup(); yield return null;
            Item("B", bowl).Pickup(); yield return null;
            Item("C", pear).Pickup(); yield return null;
            PickableItemBase extra = Item("Extra", apple);
            extra.Pickup();
            yield return null;

            Assert.That(bag.Holds(extra.gameObject), Is.False);
            Assert.That(extra.gameObject.activeSelf, Is.True);
            Assert.That(extra.GetComponent<Rigidbody>().isKinematic, Is.False, "it is still lying in the world");
            TMP_Text notice = bag.GetComponentInChildren<InventoryBarView>(true).transform.Find("Inventory Bar/Bar/Notice").GetComponent<TMP_Text>();
            Assert.That(notice.text, Is.EqualTo("Inventory full"));
            Assert.That(notice.color.a, Is.GreaterThan(0.5f));

            // Room again after dropping one.
            pickup.DropCurrentHeldItem();
            yield return null;
            extra.Pickup();
            yield return null;
            Assert.That(bag.Holds(extra.gameObject), Is.True);
        }

        [UnityTest]
        public IEnumerator TheKeysAndTheWheelObeyThePauseAndTheGate()
        {
            BuildRig();
            yield return null;
            Item("A", cube).Pickup(); yield return null;
            Item("B", bowl).Pickup(); yield return null;
            Assert.That(bag.SelectedIndex, Is.EqualTo(1));

            // Pausing the game disables the pickup system: the bar stops listening.
            pickup.enabled = false;
            yield return PressKey(keyboard.digit1Key);
            Assert.That(bag.SelectedIndex, Is.EqualTo(1), "paused: key ignored");
            yield return Scroll(120f);
            Assert.That(bag.SelectedIndex, Is.EqualTo(1), "paused: wheel ignored");
            pickup.enabled = true;
            yield return PressKey(keyboard.digit1Key);
            Assert.That(bag.SelectedIndex, Is.EqualTo(0), "resumed: key works");

            // A gate, for example "the player has control".
            bool control = false;
            bag.InputGate = () => control;
            yield return PressKey(keyboard.digit2Key);
            Assert.That(bag.SelectedIndex, Is.EqualTo(0), "no control: key ignored");
            control = true;
            yield return PressKey(keyboard.digit2Key);
            Assert.That(bag.SelectedIndex, Is.EqualTo(1), "control: key works");

            bag.InputEnabled = false;
            yield return PressKey(keyboard.digit1Key);
            Assert.That(bag.SelectedIndex, Is.EqualTo(1), "switched off: key ignored");
            bag.InputEnabled = true;

            // Keys past the last slot do nothing.
            yield return PressKey(keyboard.digit9Key);
            Assert.That(bag.SelectedIndex, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator TheArmIsSeenToLowerBetweenTwoItems()
        {
            BuildRig(drawDelay: 0.2f);
            yield return null;
            PickableItemBase a = Item("A", cube), b = Item("B", bowl);
            a.Pickup(); yield return null;
            b.Pickup(); yield return null;

            yield return PressKey(keyboard.digit1Key);
            Assert.That(pickup.IsHoldingItem(), Is.False, "one frame after the switch the hand is empty");
            yield return new WaitForSeconds(0.35f);
            Assert.That(pickup.GetCurrentObject(), Is.EqualTo(a.gameObject), "then the new item is drawn");
        }

        [UnityTest]
        public IEnumerator TheSlotCountCanChangeWhilePlaying()
        {
            BuildRig(slots: 6);
            yield return null;
            Item("A", cube).Pickup(); yield return null;
            Item("B", bowl).Pickup(); yield return null;
            Assert.That(bag.GetComponentInChildren<InventoryBarView>(true).transform.Find("Inventory Bar/Bar").childCount, Is.EqualTo(6 + 1), "six slots and the notice");

            bag.SetSlotCount(9);
            yield return null;
            Assert.That(bag.GetComponentInChildren<InventoryBarView>(true).transform.Find("Inventory Bar/Bar").childCount, Is.EqualTo(9 + 1));
            Assert.That(bag.GetComponentInChildren<InventoryBarView>(true).transform.Find("Inventory Bar/Bar/Slot 9/Key"), Is.Null);

            bag.SetSlotCount(2);
            yield return null;
            Assert.That(bag.SlotCount, Is.EqualTo(2));
            Assert.That(bag.Holds(bag.HeldItem), Is.True);

            bag.SetSlotCount(12);
            yield return null;
            Assert.That(bag.GetComponentInChildren<InventoryBarView>(true).transform.Find("Inventory Bar/Bar/Slot 10/Key"), Is.Null);
            Assert.That(bag.GetComponentInChildren<InventoryBarView>(true).transform.Find("Inventory Bar/Bar/Slot 11/Key"), Is.Null);
            yield return PressKey(keyboard.digit0Key);
            Assert.That(bag.SelectedIndex, Is.EqualTo(9));
        }

        [UnityTest]
        public IEnumerator ThrowingOrGivingAwayTheHeldItemFreesItsSlot()
        {
            BuildRig();
            yield return null;
            PickableItemBase a = Item("A", cube), b = Item("B", bowl);
            a.Pickup(); yield return null;
            b.Pickup(); yield return null;

            // The story takes the held bowl away (a hand-over, an ingredient into the pot).
            pickup.DropCurrentHeldItem();
            a.gameObject.GetComponent<PickableItemBase>();
            yield return null;
            Assert.That(bag.Holds(b.gameObject), Is.False);
            Assert.That(bag.Inventory[1].IsEmpty, Is.True);
            Assert.That(pickup.IsHoldingItem(), Is.False);
            Assert.That(bag.SelectedIndex, Is.EqualTo(1), "the selection stays on the emptied slot");

            // An item destroyed while put away just disappears from the bar.
            Object.Destroy(a.gameObject);
            yield return null;
            Assert.That(bag.Inventory[0].IsEmpty, Is.True);
        }

        [UnityTest]
        public IEnumerator TheStoryMovingAPutAwayItemSwitchesItOnAtOnceAndMayHideItAgain()
        {
            BuildRig();
            yield return null;
            PickableItemBase brush = Item("Brush", cube), other = Item("Other", bowl);
            brush.Pickup(); yield return null;
            other.Pickup(); yield return null;
            Assert.That(brush.gameObject.activeSelf, Is.False, "the brush is put away");

            // What the painting chapter does when it ends: put the brush back where it lives and hide it.
            Transform home = new GameObject("Brush home").transform;
            made.Add(home.gameObject);
            brush.transform.SetParent(home, false);
            Assert.That(brush.gameObject.activeSelf, Is.True, "moved by the story: switched on in the same moment");
            brush.gameObject.SetActive(false);

            yield return null;
            Assert.That(brush.gameObject.activeSelf, Is.False, "the story hid it, and that stays");
            Assert.That(bag.Holds(brush.gameObject), Is.False, "it left the bar");
            Assert.That(bag.Holds(other.gameObject), Is.True);
            Assert.That(pickup.GetCurrentObject(), Is.EqualTo(other.gameObject), "the hand is untouched");

            // A rewind that only moves an item (no hiding afterwards) leaves it in view.
            PickableItemBase flowers = Item("Flowers", pear);
            flowers.Pickup(); yield return null;
            other.gameObject.GetComponent<PickableItemBase>();
            Assert.That(other.gameObject.activeSelf, Is.False, "the bowl was put away for the flowers");
            other.transform.SetParent(home, false);
            Assert.That(other.gameObject.activeSelf, Is.True);
            yield return null;
            Assert.That(bag.Holds(other.gameObject), Is.False);
            Assert.That(pickup.GetCurrentObject(), Is.EqualTo(flowers.gameObject));
            Assert.That(socket.childCount, Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator WhatTheStoryPutsInTheHandIgnoresTheKeysAndTheWheelUntilItIsDropped()
        {
            BuildRig();
            yield return null;
            PickableItemBase mine = Item("Mine", bowl), other = Item("Other", pear);
            mine.Pickup(); yield return null;

            // The story gives him a brush (a direct PickupObject, not the player's own interaction).
            PickableItemBase brush = Item("Brush", cube);
            pickup.PickupObject(brush.gameObject, brush);
            yield return null;
            Assert.That(bag.IsHandLocked, Is.True);
            Assert.That(bag.Inventory.IndexOf(brush.gameObject), Is.EqualTo(1));

            yield return PressKey(keyboard.digit1Key);
            Assert.That(pickup.GetCurrentObject(), Is.EqualTo(brush.gameObject), "key 1 does nothing");
            yield return Scroll(120f);
            Assert.That(pickup.GetCurrentObject(), Is.EqualTo(brush.gameObject), "the wheel does nothing");
            Assert.That(bag.SelectedIndex, Is.EqualTo(1));

            other.Pickup();
            yield return null;
            Assert.That(bag.Holds(other.gameObject), Is.False, "picking something else up is refused while the hand is busy");
            Assert.That(brush.gameObject.activeSelf, Is.True);

            // The drop key still works, and then everything is free again.
            yield return PressKey(keyboard.qKey);
            yield return new WaitForSeconds(0.1f);
            Assert.That(pickup.IsHoldingItem(), Is.False);
            Assert.That(bag.IsHandLocked, Is.False);
            yield return PressKey(keyboard.digit1Key);
            Assert.That(bag.SelectedIndex, Is.EqualTo(0));
            Assert.That(pickup.GetCurrentObject(), Is.EqualTo(mine.gameObject));
        }

        [UnityTest]
        public IEnumerator TheBarLogsNoFontWarningsForLongNames()
        {
            var warnings = new System.Collections.Generic.List<string>();
            Application.LogCallback capture = (message, stack, type) =>
            {
                if (type == LogType.Warning && message.Contains("font asset")) warnings.Add(message);
            };

            Application.logMessageReceived += capture;
            try
            {
                BuildRig();
                yield return null;
                PickableItemData longName = Data("A very long item name that cannot possibly fit one slot of the bar", 1);
                Item("Long", longName).Pickup();
                yield return null;
                Canvas.ForceUpdateCanvases();
                yield return new WaitForSeconds(0.3f);
            }
            finally
            {
                Application.logMessageReceived -= capture;
            }

            Assert.That(warnings, Is.Empty, string.Join("\n", warnings));
        }
    }
}
