using NUnit.Framework;
using UnityEngine;

namespace MHZE.PickupSystem.Tests
{
    public sealed class HoldingTests
    {
        private GameObject systemObject, rigObject, socketObject, first, second;
        private PickupSystem pickup;
        [SetUp] public void SetUp()
        {
            systemObject = new GameObject("Pickup test"); systemObject.SetActive(false);
            pickup = systemObject.AddComponent<PickupSystem>();
            rigObject = new GameObject("Authored arm"); rigObject.transform.localPosition = new Vector3(0.1f, -0.15f, 0.4f);
            socketObject = new GameObject("Animated socket"); socketObject.transform.SetParent(rigObject.transform, false);
            pickup.ConfigureHoldingRig(rigObject.transform, socketObject.transform);
            first = new GameObject("First"); second = new GameObject("Second");
        }
        [TearDown] public void TearDown()
        {
            if (pickup.IsHoldingItem()) pickup.DropCurrentHeldItem();
            Object.DestroyImmediate(first); Object.DestroyImmediate(second); Object.DestroyImmediate(systemObject);
            Object.DestroyImmediate(socketObject); Object.DestroyImmediate(rigObject);
        }
        [Test] public void PickupBeforeStartPreservesAuthoredArmPositionAndDropRestoresIt()
        {
            var neutral = rigObject.transform.localPosition;
            var item = new TestItem { HandOffset = new Vector3(0.02f, 0.03f, -0.04f) };
            pickup.PickupObject(first, item);
            Assert.That(rigObject.transform.localPosition, Is.EqualTo(neutral + item.HandOffset));
            Assert.That(first.transform.parent, Is.EqualTo(socketObject.transform));
            pickup.DropCurrentHeldItem();
            Assert.That(rigObject.transform.localPosition, Is.EqualTo(neutral));
            Assert.That(item.PhysicsEnabled, Is.True);
        }
        [Test] public void PickingUpWhileTheRightHandIsHiddenKeepsTheItemsScale()
        {
            Transform bone = new GameObject("Right Arm").transform;
            bone.SetParent(new GameObject("Root").transform, false);
            bone.parent.SetParent(new GameObject("Armature").transform, false);
            bone.parent.parent.SetParent(rigObject.transform, false);
            socketObject.transform.SetParent(bone, false);
            var visibility = rigObject.AddComponent<PlayerArmVisibility>();
            typeof(PlayerArmVisibility).GetMethod("OnEnable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(visibility, null);
            visibility.Refresh();
            Assert.That(bone.localScale, Is.EqualTo(Vector3.zero), "the idle right hand is hidden");

            pickup.PickupObject(first, new TestItem());
            Assert.That(first.transform.localScale, Is.EqualTo(Vector3.one));
            pickup.DropCurrentHeldItem();
            Assert.That(first.transform.lossyScale, Is.EqualTo(Vector3.one));
        }

        [Test] public void DirectReplacementReleasesPreviousItemRatherThanLeavingTwoInHand()
        {
            var previous = new TestItem(); pickup.PickupObject(first, previous);
            pickup.PickupObject(second, new TestItem());
            Assert.That(first.transform.parent, Is.Null);
            Assert.That(previous.PhysicsEnabled, Is.True);
            Assert.That(pickup.GetCurrentObject(), Is.EqualTo(second));
            Assert.That(socketObject.transform.childCount, Is.EqualTo(1));
        }
        [Test] public void ProfileOverridesLegacyGripAndAddsArmOffset()
        {
            first.SetActive(false);
            var item = first.AddComponent<PickableItemBase>();
            var data = ScriptableObject.CreateInstance<PickableItemData>();
            var profile = ScriptableObject.CreateInstance<ItemHoldProfile>();
            try
            {
                item.pickableItemData = data; data.HandHoldTypeIndex = 2f; data.HandHoldPressure = 0.9f;
                Assert.That(item.GetHandHoldTypeIndex(), Is.EqualTo(2f));
                profile.Grip = ItemGrip.Stick; profile.FingerClosure = 0.6f; profile.ArmOffset = Vector3.right;
                data.HandLocationOffset = Vector3.up; data.HoldProfile = profile;
                Assert.That(item.GetHandHoldTypeIndex(), Is.EqualTo(1f));
                Assert.That(item.GetHandHoldPressure(), Is.EqualTo(0.6f));
                Assert.That(item.GetHandOffsetLocation(), Is.EqualTo(Vector3.right + Vector3.up));
            }
            finally { Object.DestroyImmediate(data); Object.DestroyImmediate(profile); }
        }
        private sealed class TestItem : IPickable
        {
            public Vector3 HandOffset;
            public bool PhysicsEnabled = true;
            public void Picked() { }
            public void Dropped() { }
            public bool GetIsPickable() => true;
            public Vector3 GetHandOffsetLocation() => HandOffset;
            public Vector3 GetItemOffsetLocation() => Vector3.zero;
            public Quaternion GetItemOffsetRotation() => Quaternion.identity;
            public float GetHandHoldPressure() => 0.5f;
            public float GetHandHoldTypeIndex() => 0f;
            public string GetItemName() => "Test";
            public void SetPickState(bool enabled) { PhysicsEnabled = enabled; }
        }
    }
}
