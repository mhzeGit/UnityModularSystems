using System.Collections.Generic;
using System.Reflection;
using MHZE.PickupSystem;
using NUnit.Framework;
using UnityEngine;
using PickupSystemComponent = MHZE.PickupSystem.PickupSystem;

namespace MHZE.InventorySystem.Tests
{
    public sealed class PlayerInventoryTests
    {
        readonly List<Object> made = new List<Object>();
        GameObject rig;
        Transform socket;
        PickupSystemComponent pickup;
        PlayerInventory bag;

        [SetUp]
        public void SetUp()
        {
            // Like the pickup tests, the rig stays inactive: edit mode does not run Awake or OnEnable on its own,
            // so the two lifecycle calls the engine would make are made by hand.
            rig = new GameObject("Rig");
            rig.SetActive(false);
            made.Add(rig);
            pickup = rig.AddComponent<PickupSystemComponent>();
            bag = rig.AddComponent<PlayerInventory>();
            socket = new GameObject("Socket").transform;
            socket.SetParent(rig.transform, false);
            pickup.ConfigureHoldingRig(rig.transform, socket);
            bag.DrawDelay = 0f;

            Call("Awake");
            Call("OnEnable");
            made.Add(bag.StashRoot.gameObject);
        }

        [TearDown]
        public void TearDown()
        {
            Call("OnDisable");
            foreach (Object o in made)
            {
                if (o != null) Object.DestroyImmediate(o);
            }

            made.Clear();
        }

        void Call(string method)
        {
            typeof(PlayerInventory).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(bag, null);
        }

        TestItem Item(string name, Object kind = null, int maxStack = 1)
        {
            var go = new GameObject(name);
            made.Add(go);
            var item = go.AddComponent<TestItem>();
            item.Kind = kind;
            item.MaxStack = maxStack;
            item.Label = name;
            return item;
        }

        void Pick(TestItem item)
        {
            pickup.CheckIfPickable(item.gameObject);
            bag.Tick();
        }

        // ───────────────────────────── picking up ─────────────────────────────

        [Test]
        public void PickedUpItemsTakeTheNextSlotsAndTheNewestIsInTheHand()
        {
            TestItem a = Item("a"), b = Item("b"), c = Item("c");
            Pick(a);
            Pick(b);
            Pick(c);

            Assert.That(bag.Inventory.IndexOf(a.gameObject), Is.EqualTo(0));
            Assert.That(bag.Inventory.IndexOf(b.gameObject), Is.EqualTo(1));
            Assert.That(bag.Inventory.IndexOf(c.gameObject), Is.EqualTo(2));
            Assert.That(bag.SelectedIndex, Is.EqualTo(2));
            Assert.That(pickup.GetCurrentObject(), Is.EqualTo(c.gameObject));
            Assert.That(socket.childCount, Is.EqualTo(1));
        }

        [Test]
        public void PutAwayItemsAreSwitchedOffInTheStashAndStillCarried()
        {
            TestItem a = Item("a"), b = Item("b");
            Pick(a);
            Pick(b);

            Assert.That(a.gameObject.activeSelf, Is.False);
            Assert.That(a.transform.parent, Is.EqualTo(bag.StashRoot));
            Assert.That(a.PhysicsEnabled, Is.False, "it is still held by the player, not lying in the world");
            Assert.That(a.DroppedCount, Is.EqualTo(0), "putting away is not dropping");
            Assert.That(a.PickedCount, Is.EqualTo(1));
        }

        [Test]
        public void AFullBarRefusesThePickupAndLeavesTheItemInTheWorld()
        {
            bag.SetSlotCount(2);
            TestItem a = Item("a"), b = Item("b"), c = Item("c");
            GameObject refused = null;
            bag.PickupRefused += go => refused = go;
            Pick(a);
            Pick(b);

            Pick(c);

            Assert.That(refused, Is.EqualTo(c.gameObject));
            Assert.That(c.gameObject.activeSelf, Is.True);
            Assert.That(c.PhysicsEnabled, Is.True);
            Assert.That(c.PickedCount, Is.EqualTo(0));
            Assert.That(pickup.GetCurrentObject(), Is.EqualTo(b.gameObject), "the hand is untouched");
            Assert.That(bag.Holds(c.gameObject), Is.False);
        }

        [Test]
        public void WithAnEmptySlotSelectedThePickupLandsInThatSlot()
        {
            TestItem a = Item("a"), d = Item("d");
            Pick(a);
            bag.Select(3);
            bag.Tick();

            Pick(d);

            Assert.That(bag.Inventory.IndexOf(d.gameObject), Is.EqualTo(3));
            Assert.That(bag.SelectedIndex, Is.EqualTo(3));
            Assert.That(pickup.GetCurrentObject(), Is.EqualTo(d.gameObject));
        }

        // ───────────────────────────── switching ─────────────────────────────

        [Test]
        public void SelectingAnotherSlotSwapsTheItemInTheHand()
        {
            TestItem a = Item("a"), b = Item("b"), c = Item("c");
            Pick(a);
            Pick(b);
            Pick(c);

            Assert.That(bag.Select(0), Is.True);
            bag.Tick();

            Assert.That(pickup.GetCurrentObject(), Is.EqualTo(a.gameObject));
            Assert.That(a.gameObject.activeSelf, Is.True);
            Assert.That(a.transform.parent, Is.EqualTo(socket));
            Assert.That(c.gameObject.activeSelf, Is.False);
            Assert.That(c.transform.parent, Is.EqualTo(bag.StashRoot));
            Assert.That(a.PickedCount, Is.EqualTo(1), "drawing from the bag is not a new pickup");
            Assert.That(c.DroppedCount, Is.EqualTo(0));
            Assert.That(socket.childCount, Is.EqualTo(1));
        }

        [Test]
        public void SelectingAnEmptySlotLeavesTheHandEmpty()
        {
            TestItem a = Item("a"), b = Item("b");
            Pick(a);
            Pick(b);

            bag.Select(4);
            bag.Tick();

            Assert.That(pickup.IsHoldingItem(), Is.False);
            Assert.That(bag.SelectedIndex, Is.EqualTo(4));
            Assert.That(bag.Holds(a.gameObject) && bag.Holds(b.gameObject), Is.True, "nothing was lost");
            Assert.That(socket.childCount, Is.EqualTo(0));
        }

        [Test]
        public void SwitchingBackAndForthKeepsExactlyOneItemInTheHand()
        {
            TestItem a = Item("a"), b = Item("b");
            Pick(a);
            Pick(b);

            for (int i = 0; i < 6; i++)
            {
                bag.Select(i % 2 == 0 ? 0 : 1);
                bag.Tick();
                Assert.That(socket.childCount, Is.EqualTo(1), "step " + i);
            }

            Assert.That(pickup.GetCurrentObject(), Is.EqualTo(b.gameObject));
        }

        [Test]
        public void SelectionWrapsAroundBothWays()
        {
            Assert.That(bag.SelectedIndex, Is.EqualTo(0));
            bag.SelectPrevious();
            Assert.That(bag.SelectedIndex, Is.EqualTo(5));
            bag.SelectNext();
            Assert.That(bag.SelectedIndex, Is.EqualTo(0));
            bag.SelectNext();
            Assert.That(bag.SelectedIndex, Is.EqualTo(1));
        }

        [Test]
        public void ASlotThatDoesNotExistCannotBeSelected()
        {
            Assert.That(bag.Select(-1), Is.False);
            Assert.That(bag.Select(6), Is.False);
            Assert.That(bag.SelectedIndex, Is.EqualTo(0));
        }

        // ───────────────────────────── stacking ─────────────────────────────

        [Test]
        public void StackableItemsShareASlotAndTheNewestIsInTheHand()
        {
            Object apple = Item("apple kind");
            TestItem a1 = Item("a1", apple, 3), a2 = Item("a2", apple, 3), a3 = Item("a3", apple, 3);
            TestItem other = Item("other");
            Pick(a1);
            Pick(a2);
            Pick(a3);
            Pick(other);

            Assert.That(bag.Inventory[0].Count, Is.EqualTo(3));
            Assert.That(bag.Inventory.IndexOf(other.gameObject), Is.EqualTo(1));
            Assert.That(bag.SelectedIndex, Is.EqualTo(1));
            Assert.That(socket.childCount, Is.EqualTo(1));
        }

        [Test]
        public void ADroppedStackItemLeavesTheRestAndTheNextIsDrawn()
        {
            Object apple = Item("apple kind");
            TestItem a1 = Item("a1", apple, 3), a2 = Item("a2", apple, 3), a3 = Item("a3", apple, 3);
            Pick(a1);
            Pick(a2);
            Pick(a3);

            pickup.DropCurrentHeldItem();
            bag.Tick();

            Assert.That(a3.transform.parent, Is.Null);
            Assert.That(a3.PhysicsEnabled, Is.True);
            Assert.That(a3.DroppedCount, Is.EqualTo(1));
            Assert.That(bag.Inventory[0].Count, Is.EqualTo(2));
            Assert.That(pickup.IsHoldingItem(), Is.True, "the next apple is in the hand");
            Assert.That(bag.Inventory.IndexOf(pickup.GetCurrentObject()), Is.EqualTo(0));
            Assert.That(socket.childCount, Is.EqualTo(1));
        }

        [Test]
        public void ARoomyStackTakesAnotherPickupEvenWhenEverySlotIsTaken()
        {
            bag.SetSlotCount(2);
            Object apple = Item("apple kind");
            TestItem a1 = Item("a1", apple, 3), a2 = Item("a2", apple, 3), other = Item("other");
            Pick(a1);
            Pick(other);

            Pick(a2);

            Assert.That(bag.Inventory[0].Count, Is.EqualTo(2));
            Assert.That(bag.SelectedIndex, Is.EqualTo(0));
            Assert.That(pickup.GetCurrentObject(), Is.EqualTo(a2.gameObject));
        }

        // ───────────────────────────── leaving the bar ─────────────────────────────

        [Test]
        public void DroppingTheOnlyItemEmptiesTheSlotAndTheHand()
        {
            TestItem a = Item("a");
            Pick(a);

            pickup.DropCurrentHeldItem();
            bag.Tick();

            Assert.That(bag.Inventory[0].IsEmpty, Is.True);
            Assert.That(pickup.IsHoldingItem(), Is.False);
            Assert.That(bag.SelectedIndex, Is.EqualTo(0));
            Assert.That(a.DroppedCount, Is.EqualTo(1));
        }

        [Test]
        public void ADroppedItemCanBePickedUpAgain()
        {
            TestItem a = Item("a");
            Pick(a);
            pickup.DropCurrentHeldItem();
            bag.Tick();

            Pick(a);

            Assert.That(bag.Inventory.IndexOf(a.gameObject), Is.EqualTo(0));
            Assert.That(pickup.GetCurrentObject(), Is.EqualTo(a.gameObject));
            Assert.That(a.PickedCount, Is.EqualTo(2));
        }

        [Test]
        public void ItemsTheWorldTakesBackLeaveTheBar()
        {
            TestItem a = Item("a"), b = Item("b");
            Pick(a);
            Pick(b);

            // A reset or rewind in the story puts the put-away item back where it belongs.
            a.transform.SetParent(null, true);
            a.gameObject.SetActive(true);
            bag.Tick();

            Assert.That(bag.Holds(a.gameObject), Is.False);
            Assert.That(bag.Inventory[0].IsEmpty, Is.True);
            Assert.That(bag.Holds(b.gameObject), Is.True);
        }

        [Test]
        public void ATakenBackItemThatWasLeftSwitchedOffIsSwitchedOnAgain()
        {
            TestItem a = Item("a"), b = Item("b");
            Pick(a);
            Pick(b);

            a.transform.SetParent(null, true);
            bag.Tick();

            Assert.That(a.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void DestroyedItemsLeaveTheBar()
        {
            TestItem a = Item("a"), b = Item("b");
            Pick(a);
            Pick(b);

            Object.DestroyImmediate(a.gameObject);
            bag.Tick();

            Assert.That(bag.Inventory[0].IsEmpty, Is.True);
            Assert.That(bag.Holds(b.gameObject), Is.True);
        }

        [Test]
        public void AnItemDestroyedInTheHandLeavesTheBarAndTheNextOfTheStackIsDrawn()
        {
            Object apple = Item("apple kind");
            TestItem a1 = Item("a1", apple, 3), a2 = Item("a2", apple, 3);
            Pick(a1);
            Pick(a2);

            Object.DestroyImmediate(a2.gameObject);
            bag.Tick();

            Assert.That(bag.Inventory[0].Count, Is.EqualTo(1));
            Assert.That(pickup.GetCurrentObject(), Is.EqualTo(a1.gameObject));
        }

        // ───────────────────────────── the story ─────────────────────────────

        [Test]
        public void AnItemTheStoryPutsInTheHandTakesASlotAndIsSelected()
        {
            TestItem flowers = Item("flowers");

            pickup.PickupObject(flowers.gameObject, flowers);
            bag.Tick();

            Assert.That(bag.Inventory.IndexOf(flowers.gameObject), Is.EqualTo(0));
            Assert.That(bag.SelectedIndex, Is.EqualTo(0));
            Assert.That(flowers.PickedCount, Is.EqualTo(1));
        }

        [Test]
        public void AnItemInTheHandBeforeTheBarWokeUpIsAdoptedByIt()
        {
            Call("OnDisable");
            TestItem early = Item("early");
            pickup.PickupObject(early.gameObject, early);

            Call("OnEnable");

            Assert.That(bag.Inventory.IndexOf(early.gameObject), Is.EqualTo(0));
            Assert.That(bag.SelectedIndex, Is.EqualTo(0));
        }

        [Test]
        public void TheStoryDrawingAPutAwayItemDoesNotPickItUpAgain()
        {
            TestItem torch = Item("torch"), other = Item("other");
            Pick(torch);
            Pick(other);

            pickup.PickupObject(torch.gameObject, torch);
            bag.Tick();

            Assert.That(pickup.GetCurrentObject(), Is.EqualTo(torch.gameObject));
            Assert.That(torch.gameObject.activeSelf, Is.True);
            Assert.That(bag.SelectedIndex, Is.EqualTo(0));
            Assert.That(torch.PickedCount, Is.EqualTo(1));
            Assert.That(other.gameObject.activeSelf, Is.False);
            Assert.That(bag.Holds(other.gameObject), Is.True);
        }

        [Test]
        public void TheStoryTakingTheHeldItemAwayFreesItsSlot()
        {
            TestItem a = Item("a"), b = Item("b");
            Pick(a);
            Pick(b);

            // A hand-over, or an ingredient going into the pot.
            pickup.DropCurrentHeldItem();
            bag.Tick();

            Assert.That(bag.Holds(b.gameObject), Is.False);
            Assert.That(bag.Inventory[1].IsEmpty, Is.True);
            Assert.That(bag.Holds(a.gameObject), Is.True);
            Assert.That(pickup.IsHoldingItem(), Is.False, "the selected slot is empty now");
        }

        [Test]
        public void AForcedPickupInAFullBarDropsTheHeldItemToMakeRoom()
        {
            bag.SetSlotCount(2);
            TestItem a = Item("a"), b = Item("b"), forced = Item("forced");
            Pick(a);
            Pick(b);

            pickup.PickupObject(forced.gameObject, forced);
            bag.Tick();

            Assert.That(b.transform.parent, Is.Null);
            Assert.That(b.DroppedCount, Is.EqualTo(1));
            Assert.That(bag.Inventory.IndexOf(forced.gameObject), Is.EqualTo(1));
            Assert.That(pickup.GetCurrentObject(), Is.EqualTo(forced.gameObject));
            Assert.That(bag.Holds(a.gameObject), Is.True);
        }

        // ───────────────────────────── items the story puts in the hand ─────────────────────────────

        [Test]
        public void WhatTheStoryPutsInTheHandCannotBeSwitchedAway()
        {
            TestItem mine = Item("mine"), brush = Item("brush");
            Pick(mine);
            var blocked = 0;
            bag.SelectionBlocked += () => blocked++;

            pickup.PickupObject(brush.gameObject, brush);
            bag.Tick();

            Assert.That(bag.IsHandLocked, Is.True);
            Assert.That(bag.Select(4), Is.False);
            Assert.That(bag.SelectNext(), Is.False);
            bag.Tick();
            Assert.That(blocked, Is.EqualTo(2));
            Assert.That(pickup.GetCurrentObject(), Is.EqualTo(brush.gameObject), "the brush stays in the hand");
            Assert.That(bag.SelectedIndex, Is.EqualTo(1));
        }

        [Test]
        public void ThePlayersPickupIsRefusedWhileTheHandHoldsAStoryItem()
        {
            TestItem cat = Item("cat"), other = Item("other");
            pickup.PickupObject(cat.gameObject, cat);

            Pick(other);

            Assert.That(pickup.GetCurrentObject(), Is.EqualTo(cat.gameObject));
            Assert.That(cat.gameObject.activeSelf, Is.True, "the cat is not put away");
            Assert.That(bag.Holds(other.gameObject), Is.False);
            Assert.That(other.PickedCount, Is.EqualTo(0));
        }

        [Test]
        public void DroppingAStoryItemFreesTheHandAndTheKeys()
        {
            TestItem brush = Item("brush"), mine = Item("mine");
            pickup.PickupObject(brush.gameObject, brush);

            pickup.DropCurrentHeldItem();
            bag.Tick();

            Assert.That(bag.IsHandLocked, Is.False);
            Pick(mine);
            Assert.That(bag.Select(3), Is.True);
        }

        [Test]
        public void WhatThePlayerPickedUpCanBeSwitchedAwayAndDrawnAgainWithoutBecomingLocked()
        {
            TestItem a = Item("a"), b = Item("b");
            Pick(a);
            Pick(b);

            bag.Select(0);
            bag.Tick();

            Assert.That(pickup.GetCurrentObject(), Is.EqualTo(a.gameObject));
            Assert.That(bag.IsHandLocked, Is.False, "an item the bar drew itself is not locked");
        }

        [Test]
        public void TheStoryDrawingAnItemFromTheBarLocksIt()
        {
            TestItem torch = Item("torch"), other = Item("other");
            Pick(torch);
            Pick(other);

            pickup.PickupObject(torch.gameObject, torch);

            Assert.That(bag.IsHandLocked, Is.True);
            Assert.That(bag.Select(2), Is.False);
        }

        [Test]
        public void TheStoryMayReplaceALockedItemAndTheLockFollowsTheNewOne()
        {
            TestItem brush = Item("brush"), torch = Item("torch");
            pickup.PickupObject(brush.gameObject, brush);

            pickup.PickupObject(torch.gameObject, torch);

            Assert.That(bag.IsHandLocked, Is.True);
            Assert.That(pickup.GetCurrentObject(), Is.EqualTo(torch.gameObject));
            Assert.That(bag.Holds(brush.gameObject), Is.True, "the brush is put away, not lost");
        }

        // ───────────────────────────── items that must stay in the hand ─────────────────────────────

        [Test]
        public void AnItemThatMustStayInTheHandCannotBePutAwayOrReplaced()
        {
            TestItem other = Item("other"), soup = Item("soup"), third = Item("third");
            Pick(other);
            Pick(soup);
            soup.StayInHand = true;
            var blocked = 0;
            bag.SelectionBlocked += () => blocked++;
            var occupied = 0;
            pickup.OnAttemptedPickWhileOccupied += () => occupied++;

            Assert.That(bag.IsHandLocked, Is.True);
            Assert.That(bag.Select(0), Is.False);
            Assert.That(bag.SelectNext(), Is.False);
            Pick(third);
            bag.Tick();

            Assert.That(blocked, Is.EqualTo(2));
            Assert.That(pickup.GetCurrentObject(), Is.EqualTo(soup.gameObject), "the soup stays in the hand");
            Assert.That(soup.gameObject.activeSelf, Is.True);
            Assert.That(bag.Holds(third.gameObject), Is.False, "no other pickup while the hand is busy");
            Assert.That(occupied, Is.EqualTo(1));
        }

        [Test]
        public void ItemsCanBePutAwayAgainOnceTheyStopAskingToStay()
        {
            TestItem other = Item("other"), soup = Item("soup");
            Pick(other);
            Pick(soup);
            soup.StayInHand = true;
            Assert.That(bag.Select(0), Is.False);

            soup.StayInHand = false;
            Assert.That(bag.IsHandLocked, Is.False);
            Assert.That(bag.Select(0), Is.True);
            bag.Tick();

            Assert.That(pickup.GetCurrentObject(), Is.EqualTo(other.gameObject));
            Assert.That(bag.Holds(soup.gameObject), Is.True, "the soup is in the bar, put away");
        }

        [Test]
        public void AnItemThatMustStayCanStillBeDroppedAndThenTheKeysWork()
        {
            TestItem soup = Item("soup"), mine = Item("mine");
            Pick(soup);
            soup.StayInHand = true;

            pickup.DropCurrentHeldItem();
            bag.Tick();

            Assert.That(bag.IsHandLocked, Is.False);
            Assert.That(bag.Holds(soup.gameObject), Is.False);
            Pick(mine);
            Assert.That(pickup.GetCurrentObject(), Is.EqualTo(mine.gameObject));
        }
        // ───────────────────────────── slot count and release ─────────────────────────────

        [Test]
        public void ShrinkingTheBarKeepsWhatFitsAndDropsWhatDoesNot()
        {
            TestItem[] items = new TestItem[5];
            for (int i = 0; i < items.Length; i++)
            {
                items[i] = Item("item" + i);
                Pick(items[i]);
            }

            bag.SetSlotCount(3);
            bag.Tick();

            Assert.That(bag.SlotCount, Is.EqualTo(3));
            Assert.That(bag.SelectedIndex, Is.InRange(0, 2));
            Assert.That(items[4].DroppedCount, Is.EqualTo(1), "the one in the cut slot was in the hand and fell");
            Assert.That(items[3].transform.parent, Is.Null, "the other cut one fell too");
            Assert.That(items[3].PhysicsEnabled, Is.True);
            Assert.That(socket.childCount, Is.LessThanOrEqualTo(1));
        }

        [Test]
        public void ShrinkingMovesCutItemsIntoFreeSlots()
        {
            TestItem a = Item("a"), b = Item("b");
            Pick(a);
            bag.Select(4);
            bag.Tick();
            Pick(b);

            bag.SetSlotCount(3);

            Assert.That(bag.Holds(b.gameObject), Is.True);
            Assert.That(bag.Inventory.IndexOf(b.gameObject), Is.LessThan(3));
            Assert.That(bag.SelectedIndex, Is.EqualTo(bag.Inventory.IndexOf(b.gameObject)), "the selection follows the item in the hand");
        }

        [Test]
        public void GrowingTheBarChangesNothingElse()
        {
            TestItem a = Item("a"), b = Item("b");
            Pick(a);
            Pick(b);

            bag.SetSlotCount(9);

            Assert.That(bag.SlotCount, Is.EqualTo(9));
            Assert.That(bag.Inventory.IndexOf(a.gameObject), Is.EqualTo(0));
            Assert.That(bag.SelectedIndex, Is.EqualTo(1));
            Assert.That(pickup.GetCurrentObject(), Is.EqualTo(b.gameObject));
        }

        [Test]
        public void ReleasingEverythingPutsAllItemsBackInTheWorld()
        {
            TestItem a = Item("a"), b = Item("b"), c = Item("c");
            Pick(a);
            Pick(b);
            Pick(c);

            int released = bag.ReleaseAll();
            bag.Tick();

            Assert.That(released, Is.EqualTo(3));
            foreach (TestItem item in new[] { a, b, c })
            {
                Assert.That(item.gameObject.activeSelf, Is.True, item.name);
                Assert.That(item.transform.parent, Is.Null, item.name);
                Assert.That(item.PhysicsEnabled, Is.True, item.name);
                Assert.That(bag.Holds(item.gameObject), Is.False, item.name);
            }

            Assert.That(pickup.IsHoldingItem(), Is.False);
        }

        // ───────────────────────────── describing items ─────────────────────────────

        [Test]
        public void ItemDataSuppliesTheNameTheIconAndTheStackSize()
        {
            var data = ScriptableObject.CreateInstance<PickableItemData>();
            made.Add(data);
            data.ItemName = "Carrot";
            data.MaxStack = 4;
            var go = new GameObject("scene object name");
            made.Add(go);
            go.AddComponent<PickableItemBase>().pickableItemData = data;

            InventoryItemInfo info = PlayerInventory.InfoOf(go);

            Assert.That(info.Name, Is.EqualTo("Carrot"));
            Assert.That(info.MaxStack, Is.EqualTo(4));
            Assert.That(info.Kind, Is.EqualTo(data));
        }

        [Test]
        public void AnItemWithoutDataIsUniqueAndShownByItsObjectName()
        {
            var go = new GameObject("Mystery");
            made.Add(go);

            InventoryItemInfo info = PlayerInventory.InfoOf(go);

            Assert.That(info.Name, Is.EqualTo("Mystery"));
            Assert.That(info.MaxStack, Is.EqualTo(1));
            Assert.That(info.Kind, Is.EqualTo(go));
        }

        sealed class TestItem : MonoBehaviour, IPickable, IInventoryItem, IKeepInHand
        {
            public Object Kind;
            public int MaxStack = 1;
            public string Label = "item";
            public bool PhysicsEnabled = true;
            public bool StayInHand;
            public bool MustStayInHand => StayInHand;
            public int PickedCount;
            public int DroppedCount;

            public void Picked() { PickedCount++; }
            public void Dropped() { DroppedCount++; }
            public bool GetIsPickable() => true;
            public Vector3 GetHandOffsetLocation() => Vector3.zero;
            public Vector3 GetItemOffsetLocation() => Vector3.zero;
            public Quaternion GetItemOffsetRotation() => Quaternion.identity;
            public float GetHandHoldPressure() => 0.5f;
            public float GetHandHoldTypeIndex() => 0f;
            public string GetItemName() => Label;
            public void SetPickState(bool enable) { PhysicsEnabled = enable; }
            public InventoryItemInfo GetInventoryInfo() => new InventoryItemInfo(Kind != null ? Kind : this, Label, null, MaxStack);
        }
    }
}
