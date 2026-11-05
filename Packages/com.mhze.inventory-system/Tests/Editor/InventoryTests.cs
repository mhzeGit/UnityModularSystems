using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MHZE.InventorySystem.Tests
{
    public sealed class InventoryTests
    {
        readonly List<Object> made = new List<Object>();
        Inventory inventory;

        [SetUp]
        public void SetUp()
        {
            inventory = new Inventory(3);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in made)
            {
                if (o != null) Object.DestroyImmediate(o);
            }

            made.Clear();
        }

        GameObject Thing(string name)
        {
            var go = new GameObject(name);
            made.Add(go);
            return go;
        }

        InventoryItemInfo Info(Object kind, int maxStack = 1) => new InventoryItemInfo(kind, kind != null ? kind.name : "", null, maxStack);

        [Test]
        public void ItemsFillTheSlotsInOrder()
        {
            Assert.That(inventory.Add(Thing("a"), Info(Thing("ka"))), Is.EqualTo(0));
            Assert.That(inventory.Add(Thing("b"), Info(Thing("kb"))), Is.EqualTo(1));
            Assert.That(inventory.Add(Thing("c"), Info(Thing("kc"))), Is.EqualTo(2));
        }

        [Test]
        public void AFullInventoryDoesNotTakeAnotherItem()
        {
            for (int i = 0; i < 3; i++) inventory.Add(Thing("i" + i), Info(Thing("k" + i)));

            InventoryItemInfo extra = Info(Thing("extra"));
            Assert.That(inventory.CanAdd(extra), Is.False);
            Assert.That(inventory.Add(Thing("extra item"), extra), Is.EqualTo(-1));
        }

        [Test]
        public void ItemsOfTheSameStackableKindShareOneSlot()
        {
            Object apple = Thing("apple kind");
            for (int i = 0; i < 3; i++) Assert.That(inventory.Add(Thing("apple " + i), Info(apple, 3)), Is.EqualTo(0));

            Assert.That(inventory[0].Count, Is.EqualTo(3));
            Assert.That(inventory.Add(Thing("apple 3"), Info(apple, 3)), Is.EqualTo(1), "a full stack overflows into the next slot");
            Assert.That(inventory[1].Count, Is.EqualTo(1));
        }

        [Test]
        public void ItemsThatDoNotStackNeverShareASlot()
        {
            Object cube = Thing("cube kind");
            Assert.That(inventory.Add(Thing("cube 1"), Info(cube, 1)), Is.EqualTo(0));
            Assert.That(inventory.Add(Thing("cube 2"), Info(cube, 1)), Is.EqualTo(1));
        }

        [Test]
        public void DifferentKindsNeverShareASlotEvenWhenBothStack()
        {
            Assert.That(inventory.Add(Thing("apple"), Info(Thing("apple kind"), 5)), Is.EqualTo(0));
            Assert.That(inventory.Add(Thing("pear"), Info(Thing("pear kind"), 5)), Is.EqualTo(1));
        }

        [Test]
        public void ARoomyStackStillTakesItemsWhenNoSlotIsEmpty()
        {
            Object apple = Thing("apple kind");
            inventory.Add(Thing("apple 1"), Info(apple, 4));
            inventory.Add(Thing("x"), Info(Thing("kx")));
            inventory.Add(Thing("y"), Info(Thing("ky")));

            InventoryItemInfo another = Info(apple, 4);
            Assert.That(inventory.CanAdd(another), Is.True);
            Assert.That(inventory.Add(Thing("apple 2"), another), Is.EqualTo(0));
        }

        [Test]
        public void ThePreferredSlotIsUsedOnlyWhenItIsEmpty()
        {
            Assert.That(inventory.Add(Thing("a"), Info(Thing("ka")), 2), Is.EqualTo(2));
            Assert.That(inventory.Add(Thing("b"), Info(Thing("kb")), 2), Is.EqualTo(0), "slot 2 is taken, so the first empty slot");
        }

        [Test]
        public void AStackIsPreferredOverAnEmptyPreferredSlot()
        {
            Object apple = Thing("apple kind");
            inventory.Add(Thing("apple 1"), Info(apple, 3));
            Assert.That(inventory.Add(Thing("apple 2"), Info(apple, 3), 2), Is.EqualTo(0));
        }

        [Test]
        public void AddingTheSameItemTwiceKeepsOneCopy()
        {
            GameObject a = Thing("a");
            InventoryItemInfo info = Info(Thing("ka"), 5);
            inventory.Add(a, info);
            Assert.That(inventory.Add(a, info), Is.EqualTo(0));
            Assert.That(inventory[0].Count, Is.EqualTo(1));
        }

        [Test]
        public void RemovingTheLastItemEmptiesTheSlotAndForgetsWhatItHeld()
        {
            GameObject a = Thing("a");
            inventory.Add(a, Info(Thing("ka")));

            Assert.That(inventory.Remove(a), Is.EqualTo(0));
            Assert.That(inventory[0].IsEmpty, Is.True);
            Assert.That(inventory[0].Info.Kind, Is.Null);
            Assert.That(inventory.Remove(a), Is.EqualTo(-1), "it is not inside any more");
        }

        [Test]
        public void RemovingOneOfAStackLeavesTheRest()
        {
            Object apple = Thing("apple kind");
            GameObject first = Thing("apple 1");
            inventory.Add(first, Info(apple, 3));
            inventory.Add(Thing("apple 2"), Info(apple, 3));

            inventory.Remove(first);
            Assert.That(inventory[0].Count, Is.EqualTo(1));
            Assert.That(inventory[0].Info.Kind, Is.EqualTo(apple));
        }

        [Test]
        public void DestroyedItemsAreForgotten()
        {
            GameObject a = Thing("a");
            GameObject b = Thing("b");
            Object kind = Thing("kind");
            inventory.Add(a, Info(kind, 3));
            inventory.Add(b, Info(kind, 3));

            Object.DestroyImmediate(a);

            Assert.That(inventory.RemoveDestroyed(), Is.EqualTo(1));
            Assert.That(inventory[0].Count, Is.EqualTo(1));
            Assert.That(inventory.IndexOf(b), Is.EqualTo(0));
        }

        [Test]
        public void SlotChangedIsRaisedForEveryChange()
        {
            var changes = new List<int>();
            inventory.SlotChanged += changes.Add;
            GameObject a = Thing("a");

            inventory.Add(a, Info(Thing("ka")), 1);
            inventory.Remove(a);

            Assert.That(changes, Is.EqualTo(new[] { 1, 1 }));
        }

        [Test]
        public void GrowingAddsEmptySlotsAndShrinkingHandsBackWhatWasCutOff()
        {
            GameObject a = Thing("a");
            GameObject b = Thing("b");
            GameObject c = Thing("c");
            inventory.Add(a, Info(Thing("ka")));
            inventory.Add(b, Info(Thing("kb")));
            inventory.Add(c, Info(Thing("kc")));

            Assert.That(inventory.Resize(5), Is.Empty);
            Assert.That(inventory.SlotCount, Is.EqualTo(5));

            var displaced = inventory.Resize(2);
            Assert.That(inventory.SlotCount, Is.EqualTo(2));
            Assert.That(displaced.Count, Is.EqualTo(1));
            Assert.That(displaced[0].Key, Is.EqualTo(c));
            Assert.That(inventory.IndexOf(c), Is.EqualTo(-1));
        }

        [Test]
        public void TheSlotCountIsKeptInRange()
        {
            Assert.That(new Inventory(0).SlotCount, Is.EqualTo(1));
            Assert.That(new Inventory(500).SlotCount, Is.EqualTo(Inventory.MaxSlotCount));
        }
    }
}
