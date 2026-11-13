using NUnit.Framework;
using UnityEngine;

namespace MHZE.PickupSystem.Tests
{
    /// <summary>A drop held over or through a wall is refused; an open drop and code drops are not.</summary>
    public sealed class DropClearanceTests
    {
        GameObject systemObject, playerRoot, cameraObject, socket, item, wall;
        PickupSystem pickup;

        [SetUp]
        public void SetUp()
        {
            systemObject = new GameObject("Pickup test");
            systemObject.SetActive(false);
            pickup = systemObject.AddComponent<PickupSystem>();

            playerRoot = new GameObject("Player");
            cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(playerRoot.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            socket = new GameObject("Socket");
            socket.transform.SetParent(cameraObject.transform, false);
            pickup.ConfigureHoldingRig(socket.transform, socket.transform);
            pickup.DropCheckOrigin = cameraObject.transform;

            // A 20 cm cube with a mesh and no collider, like an item whose colliders are off in the hand.
            item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = "Item";
            Object.DestroyImmediate(item.GetComponent<Collider>());
            item.transform.localScale = Vector3.one * 0.2f;
            item.AddComponent<Rigidbody>().isKinematic = true;
        }

        [TearDown]
        public void TearDown()
        {
            if (pickup != null && pickup.IsHoldingItem()) pickup.DropCurrentHeldItem();
            Object.DestroyImmediate(item);
            if (wall != null) Object.DestroyImmediate(wall);
            Object.DestroyImmediate(systemObject);
            Object.DestroyImmediate(playerRoot);
        }

        // The item goes into the hand and the hand reaches 'reach' metres in front of the camera (+Z).
        void Hold(float reach)
        {
            pickup.PickupObject(item, new StubItem());
            socket.transform.localPosition = new Vector3(0f, 0f, reach);
            item.transform.localPosition = Vector3.zero;
            item.transform.localRotation = Quaternion.identity;
        }

        void BuildWall(float z, float thickness)
        {
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            wall.transform.position = new Vector3(0f, 1.6f, z);
            wall.transform.localScale = new Vector3(6f, 3f, thickness);
        }

        [Test]
        public void OpenSpaceIsClear()
        {
            Hold(0.5f);
            Assert.That(pickup.IsHeldItemObstructed(out _), Is.False);
        }

        [Test]
        public void ItemPressedIntoAWallIsBlocked()
        {
            BuildWall(0.55f, 0.3f);
            Hold(0.5f);
            Assert.That(pickup.IsHeldItemObstructed(out Collider blocker), Is.True);
            Assert.That(blocker.gameObject, Is.EqualTo(wall));
        }

        [Test]
        public void ItemWhollyBehindAThinWallIsBlockedByTheLineCheck()
        {
            // The wall is 2 cm thick at z = 0.3 and the item sits at z = 0.6: nothing overlaps it, the wall is between.
            BuildWall(0.3f, 0.02f);
            Hold(0.6f);
            Assert.That(pickup.IsHeldItemObstructed(out Collider blocker), Is.True);
            Assert.That(blocker.gameObject, Is.EqualTo(wall));
        }

        [Test]
        public void WallBehindThePlayerSideOfTheItemDoesNotBlock()
        {
            // Wall 1.5 m ahead, item 0.5 m ahead: the player's side, so it drops normally.
            BuildWall(1.5f, 0.3f);
            Hold(0.5f);
            Assert.That(pickup.IsHeldItemObstructed(out _), Is.False);
        }

        [Test]
        public void TheCheckCanBeSwitchedOff()
        {
            BuildWall(0.55f, 0.3f);
            Hold(0.5f);
            pickup.CheckDropClearance = false;
            Assert.That(pickup.IsHeldItemObstructed(out _), Is.False);
        }

        [Test]
        public void PlayerAndFreeBodiesAndOtherItemsAreNotObstacles()
        {
            Hold(0.5f);
            Vector3 at = item.transform.position;

            var capsule = new GameObject("Player body", typeof(CharacterController));
            capsule.transform.SetParent(playerRoot.transform, false);
            capsule.transform.position = at;

            var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.transform.position = at;
            crate.AddComponent<Rigidbody>();

            var loose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            loose.transform.position = at;
            loose.AddComponent<Rigidbody>().isKinematic = true;
            loose.AddComponent<StubBehaviour>();

            try
            {
                Assert.That(pickup.IsHeldItemObstructed(out Collider blocker), Is.False, blocker != null ? blocker.name : "");
            }
            finally
            {
                Object.DestroyImmediate(capsule);
                Object.DestroyImmediate(crate);
                Object.DestroyImmediate(loose);
            }
        }

        [Test]
        public void NothingHeldIsNeverObstructed()
        {
            BuildWall(0.2f, 0.3f);
            Assert.That(pickup.IsHeldItemObstructed(out _), Is.False);
        }

        [Test]
        public void CodeDropStillWorksWhileObstructed()
        {
            BuildWall(0.55f, 0.3f);
            Hold(0.5f);
            Assert.That(pickup.IsHeldItemObstructed(out _), Is.True);
            pickup.DropCurrentHeldItem();
            Assert.That(pickup.IsHoldingItem(), Is.False);
        }

        [Test]
        public void DropPressedIntoAWallLandsOnThePlayersSide()
        {
            BuildWall(0.75f, 0.3f); // near face at z = 0.6
            Hold(0.7f);
            Assert.That(pickup.TryPlayerDrop(), Is.True);
            Assert.That(pickup.IsHoldingItem(), Is.False);
            Assert.That(item.transform.parent, Is.Null);
            Assert.That(item.transform.position.z, Is.LessThan(0.6f - 0.1f), "the cube must be wholly on the camera side of the wall");
            Assert.That(item.transform.position.z, Is.GreaterThanOrEqualTo(0.3f - 0.001f), "never closer to the camera than the pull-back limit");
        }

        [Test]
        public void DropBehindAThinWallLandsOnThePlayersSide()
        {
            BuildWall(0.55f, 0.02f);
            Hold(0.7f);
            Assert.That(pickup.TryPlayerDrop(), Is.True);
            Assert.That(item.transform.position.z, Is.LessThan(0.55f - 0.01f - 0.1f));
        }

        [Test]
        public void DropInOpenSpaceStaysWhereItIs()
        {
            Hold(0.5f);
            Vector3 before = item.transform.position;
            Assert.That(pickup.TryPlayerDrop(), Is.True);
            Assert.That(item.transform.position, Is.EqualTo(before));
        }

        [Test]
        public void DropIsRefusedWhenNoClearSpotExistsOnThePlayersSide()
        {
            // A wall 15 cm from the camera leaves no room between it and the 30 cm pull-back limit.
            BuildWall(0.15f, 0.02f);
            Hold(0.6f);
            Vector3 before = item.transform.position;
            bool blockedEvent = false;
            pickup.OnDropBlocked += _ => blockedEvent = true;
            Assert.That(pickup.TryPlayerDrop(), Is.False);
            Assert.That(pickup.IsHoldingItem(), Is.True);
            Assert.That(item.transform.position, Is.EqualTo(before));
            Assert.That(blockedEvent, Is.True);
        }

        sealed class StubBehaviour : MonoBehaviour, IPickable
        {
            public void Picked() { }
            public void Dropped() { }
            public bool GetIsPickable() => true;
            public Vector3 GetHandOffsetLocation() => Vector3.zero;
            public Vector3 GetItemOffsetLocation() => Vector3.zero;
            public Quaternion GetItemOffsetRotation() => Quaternion.identity;
            public float GetHandHoldPressure() => 0f;
            public float GetHandHoldTypeIndex() => 0f;
            public string GetItemName() => "Loose";
            public void SetPickState(bool enabled) { }
        }

        sealed class StubItem : IPickable
        {
            public void Picked() { }
            public void Dropped() { }
            public bool GetIsPickable() => true;
            public Vector3 GetHandOffsetLocation() => Vector3.zero;
            public Vector3 GetItemOffsetLocation() => Vector3.zero;
            public Quaternion GetItemOffsetRotation() => Quaternion.identity;
            public float GetHandHoldPressure() => 0f;
            public float GetHandHoldTypeIndex() => 0f;
            public string GetItemName() => "Stub";
            public void SetPickState(bool enabled) { }
        }
    }
}
