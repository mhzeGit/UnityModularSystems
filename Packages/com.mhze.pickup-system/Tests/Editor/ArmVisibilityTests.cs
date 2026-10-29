using NUnit.Framework;
using UnityEngine;

namespace MHZE.PickupSystem.Tests
{
    public sealed class ArmVisibilityTests
    {
        private GameObject arm, owner;
        private Transform left, right;
        private PlayerArmVisibility visibility;

        [SetUp] public void SetUp()
        {
            arm = new GameObject("Arm rig"); arm.AddComponent<Animator>();
            Transform root = new GameObject("Root").transform;
            root.SetParent(new GameObject("Armature").transform, false);
            root.parent.SetParent(arm.transform, false);
            right = new GameObject("Right Arm").transform; right.SetParent(root, false);
            left = new GameObject("Right Arm.001").transform; left.SetParent(root, false);
            visibility = arm.AddComponent<PlayerArmVisibility>();
            owner = new GameObject("Owner");
        }

        [TearDown] public void TearDown() { Object.DestroyImmediate(arm); Object.DestroyImmediate(owner); }

        private bool Shown(Transform bone) { visibility.Refresh(); return bone.localScale != Vector3.zero; }

        [Test] public void BothHandsAreHiddenWhenNothingUsesThem()
        {
            Assert.That(Shown(left), Is.False);
            Assert.That(Shown(right), Is.False);
        }

        [Test] public void ShowingOneHandLeavesTheOtherHidden()
        {
            visibility.Show(PlayerHand.Right, owner);
            Assert.That(Shown(right), Is.True);
            Assert.That(Shown(left), Is.False);
            visibility.Clear(owner);
            Assert.That(Shown(right), Is.False);
        }

        [Test] public void HideBeatsShowAndEachOwnerIsIndependent()
        {
            var other = new GameObject("Other");
            visibility.Show(PlayerHand.Left, owner);
            visibility.Hide(PlayerHand.Left, other);
            Assert.That(Shown(left), Is.False);
            visibility.Clear(other);
            Assert.That(Shown(left), Is.True);
            Object.DestroyImmediate(other);
        }

        [Test] public void ADestroyedOwnerGivesTheHandBack()
        {
            visibility.Show(PlayerHand.Left, owner);
            Assert.That(Shown(left), Is.True);
            Object.DestroyImmediate(owner);
            Assert.That(Shown(left), Is.False);
        }
    }
}
