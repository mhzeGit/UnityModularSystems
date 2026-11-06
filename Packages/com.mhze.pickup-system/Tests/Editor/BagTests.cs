using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MHZE.PickupSystem.Tests
{
    public sealed class BagTests
    {
        private GameObject systemObject, rigObject, socketObject, stashObject;
        private PickupSystem pickup;
        private FakeBag bag;
        private BagItem first, second;

        [SetUp] public void SetUp()
        {
            systemObject = new GameObject("Pickup test"); systemObject.SetActive(false);
            pickup = systemObject.AddComponent<PickupSystem>();
            rigObject = new GameObject("Authored arm");
            socketObject = new GameObject("Animated socket"); socketObject.transform.SetParent(rigObject.transform, false);
            pickup.ConfigureHoldingRig(rigObject.transform, socketObject.transform);
            stashObject = new GameObject("Stash");
            bag = new FakeBag(pickup, stashObject.transform);
            first = new GameObject("First").AddComponent<BagItem>();
            second = new GameObject("Second").AddComponent<BagItem>();
        }

        [TearDown] public void TearDown()
        {
            bag.Detach();
            Object.DestroyImmediate(first.gameObject); Object.DestroyImmediate(second.gameObject);
            Object.DestroyImmediate(systemObject); Object.DestroyImmediate(socketObject);
            Object.DestroyImmediate(rigObject); Object.DestroyImmediate(stashObject);
        }

        [Test] public void APickupWhileOccupiedPutsTheHeldItemAwayInsteadOfDroppingIt()
        {
            pickup.CheckIfPickable(first.gameObject);

            pickup.CheckIfPickable(second.gameObject);

            Assert.That(pickup.GetCurrentObject(), Is.EqualTo(second.gameObject));
            Assert.That(first.gameObject.activeSelf, Is.False);
            Assert.That(first.transform.parent, Is.EqualTo(stashObject.transform));
            Assert.That(first.PhysicsEnabled, Is.False, "put away, not released into the world");
            Assert.That(first.DroppedCount, Is.EqualTo(0));
            Assert.That(second.PickedCount, Is.EqualTo(1));
            Assert.That(socketObject.transform.childCount, Is.EqualTo(1));
        }

        [Test] public void APickupWithTheHandEmptyWorksAsBefore()
        {
            pickup.CheckIfPickable(first.gameObject);

            Assert.That(pickup.GetCurrentObject(), Is.EqualTo(first.gameObject));
            Assert.That(first.PickedCount, Is.EqualTo(1));
            Assert.That(first.transform.parent, Is.EqualTo(socketObject.transform));
        }

        [Test] public void StowingRaisesTheChangeEventWhileTheStowFlagIsSet()
        {
            pickup.PickupObject(first.gameObject, first);
            var seen = new List<string>();
            pickup.OnPickedItemChanged += item => seen.Add((item == null ? "none" : item.name) + (pickup.IsStowing ? " (stowing)" : ""));

            Assert.That(pickup.StowHeldItem(), Is.True);

            Assert.That(seen, Is.EqualTo(new[] { "none (stowing)" }));
            Assert.That(pickup.IsStowing, Is.False);
            Assert.That(pickup.IsHoldingItem(), Is.False);
        }

        [Test] public void StowingNeedsABagAndSomethingHeld()
        {
            Assert.That(pickup.StowHeldItem(), Is.False, "nothing is held");

            pickup.PickupObject(first.gameObject, first);
            bag.Detach();
            Assert.That(pickup.StowHeldItem(), Is.False, "no bag");
            Assert.That(pickup.GetCurrentObject(), Is.EqualTo(first.gameObject));
        }

        [Test] public void AnItemTheBagKeepsIsDrawnWithoutThePickupCallbacksAndSwitchedBackOn()
        {
            pickup.PickupObject(first.gameObject, first);
            pickup.StowHeldItem();
            var announced = 0;
            pickup.OnItemPicked += _ => announced++;

            pickup.PickupObject(first.gameObject, first);

            Assert.That(first.gameObject.activeSelf, Is.True);
            Assert.That(first.transform.parent, Is.EqualTo(socketObject.transform));
            Assert.That(first.PickedCount, Is.EqualTo(1), "only the first pickup counts");
            Assert.That(announced, Is.EqualTo(0));
        }

        [Test] public void ABagWithNoRoomRefusesThePickupAndMovesNothing()
        {
            pickup.PickupObject(first.gameObject, first);
            bag.HasRoom = false;
            GameObject refused = null;
            pickup.OnPickRefused += item => refused = item;

            pickup.CheckIfPickable(second.gameObject);

            Assert.That(refused, Is.EqualTo(second.gameObject));
            Assert.That(pickup.GetCurrentObject(), Is.EqualTo(first.gameObject));
            Assert.That(second.PickedCount, Is.EqualTo(0));
            Assert.That(second.PhysicsEnabled, Is.True);
        }

        [Test] public void WhenTheBagCannotKeepTheNewcomerTheHeldItemIsDroppedAsBefore()
        {
            pickup.PickupObject(first.gameObject, first);
            bag.HasRoom = false;

            pickup.PickupObject(second.gameObject, second);

            Assert.That(first.transform.parent, Is.Null);
            Assert.That(first.DroppedCount, Is.EqualTo(1));
            Assert.That(first.PhysicsEnabled, Is.True);
            Assert.That(pickup.GetCurrentObject(), Is.EqualTo(second.gameObject));
        }

        [Test] public void AHeldItemTheBagDoesNotKnowIsDroppedWhenAnotherIsTaken()
        {
            bag.Register = false;
            pickup.PickupObject(first.gameObject, first);

            pickup.PickupObject(second.gameObject, second);

            Assert.That(first.transform.parent, Is.Null);
            Assert.That(first.DroppedCount, Is.EqualTo(1));
        }

        [Test] public void AHandHoldingSomethingTheStoryPutThereRefusesThePlayersPickup()
        {
            pickup.PickupObject(first.gameObject, first);
            bag.Locked = true;
            var occupied = 0;
            pickup.OnAttemptedPickWhileOccupied += () => occupied++;

            pickup.CheckIfPickable(second.gameObject);

            Assert.That(pickup.GetCurrentObject(), Is.EqualTo(first.gameObject), "the held item stays");
            Assert.That(first.gameObject.activeSelf, Is.True);
            Assert.That(second.PickedCount, Is.EqualTo(0));
            Assert.That(occupied, Is.EqualTo(1), "the hand is busy, as before");
        }

        [Test] public void TheStoryMayStillReplaceALockedItem()
        {
            pickup.PickupObject(first.gameObject, first);
            bag.Locked = true;

            pickup.PickupObject(second.gameObject, second);

            Assert.That(pickup.GetCurrentObject(), Is.EqualTo(second.gameObject));
            Assert.That(first.gameObject.activeSelf, Is.False, "put away, the bag keeps it");
        }

        [Test] public void ListenersCanTellThePlayersPickupFromAStoryPickup()
        {
            var seen = new List<string>();
            pickup.OnPickedItemChanged += item => { if (item != null) seen.Add(item.name + (pickup.IsPlayerPickup ? " by the player" : " by the story")); };

            pickup.CheckIfPickable(first.gameObject);
            pickup.PickupObject(second.gameObject, second);

            Assert.That(seen, Is.EqualTo(new[] { "First by the player", "Second by the story" }));
            Assert.That(pickup.IsPlayerPickup, Is.False, "and the flag is not left on");
        }

        // Keeps whatever the hand picks up, the way an inventory does.
        private sealed class FakeBag : IPickupBag
        {
            private readonly PickupSystem pickup;
            private readonly Transform stash;
            private readonly HashSet<GameObject> kept = new HashSet<GameObject>();
            public bool HasRoom = true;
            public bool Register = true;
            public bool Locked;

            public FakeBag(PickupSystem pickup, Transform stash)
            {
                this.pickup = pickup;
                this.stash = stash;
                pickup.Bag = this;
                pickup.OnPickedItemChanged += Changed;
            }

            public void Detach()
            {
                pickup.OnPickedItemChanged -= Changed;
                if (pickup.Bag == this) pickup.Bag = null;
            }

            private void Changed(GameObject item)
            {
                if (item != null && Register) kept.Add(item);
            }

            public Transform StashRoot => stash;
            public bool Contains(GameObject item) => kept.Contains(item);
            public bool CanKeep(GameObject item, IPickable pickable) => HasRoom;
            public bool IsHandLocked => Locked;
        }

        private sealed class BagItem : MonoBehaviour, IPickable
        {
            public bool PhysicsEnabled = true;
            public int PickedCount, DroppedCount;
            public void Picked() { PickedCount++; }
            public void Dropped() { DroppedCount++; }
            public bool GetIsPickable() => true;
            public Vector3 GetHandOffsetLocation() => Vector3.zero;
            public Vector3 GetItemOffsetLocation() => Vector3.zero;
            public Quaternion GetItemOffsetRotation() => Quaternion.identity;
            public float GetHandHoldPressure() => 0.5f;
            public float GetHandHoldTypeIndex() => 0f;
            public string GetItemName() => "Bag item";
            public void SetPickState(bool enable) { PhysicsEnabled = enable; }
        }
    }
}
