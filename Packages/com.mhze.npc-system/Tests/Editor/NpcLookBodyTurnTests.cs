using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ModularNPC.Tests
{
    /// <summary>Checks angle-triggered body turns and rotation ownership during continuous looks.</summary>
    public sealed class NpcLookBodyTurnTests
    {
        private GameObject root, target, scheduler;
        private Npc npc;
        private NpcRotation rotation;
        private NpcLookAtTarget look;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp] public void SetUp()
        {
            // Tick manually in Edit Mode without the runtime scheduler's DontDestroyOnLoad.
            var schedulerType = typeof(Npc).Assembly.GetType("ModularNPC.NpcScheduler");
            scheduler = new GameObject("test scheduler");
            schedulerType.GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, scheduler.AddComponent(schedulerType));
            root = new GameObject("look test");
            new GameObject("Head").transform.SetParent(root.transform);
            var head = root.transform.Find("Head");
            foreach (var name in new[] { "LeftEye", "RightEye" })
            {
                var eye = new GameObject(name).transform;
                eye.SetParent(head, false);
                eye.localPosition = new Vector3(name == "LeftEye" ? -0.04f : 0.04f, 0.1f, 0.08f);
                eye.localRotation = Quaternion.Euler(name == "LeftEye" ? 13f : -9f, 17f, 5f);
            }
            target = new GameObject("target");
            npc = root.AddComponent<Npc>();
            rotation = npc.AddFeature<NpcRotation>();
            look = npc.AddFeature<NpcLookAtTarget>();
            Activate(rotation); Activate(look);
            look.BodyTurnThresholdDegrees = 90f;
        }

        private void Activate(NpcFeature feature)
        {
            typeof(NpcFeature).GetMethod("HandleInitialize", Private).Invoke(feature, new object[] { npc });
            typeof(NpcFeature).GetMethod("HandleOwnerAvailabilityChanged", Private).Invoke(feature, new object[] { true });
        }

        [TearDown] public void TearDown()
        {
            look.StopLooking();
            Object.DestroyImmediate(root); Object.DestroyImmediate(target);
            Object.DestroyImmediate(scheduler);
        }

        private void Aim(float yaw)
        {
            target.transform.position = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * 2f;
            Assert.That(look.LookAt(target.transform), Is.True);
            look.Tick(1f / 60f);
        }

        [Test] public void EyesConvergeWithDifferentAuthoredBoneAxesAndRelease()
        {
            var head = root.transform.Find("Head");
            var left = head.Find("LeftEye"); var right = head.Find("RightEye");
            Quaternion leftRest = left.localRotation, rightRest = right.localRotation;
            Vector3 leftAxis = Quaternion.Inverse(left.rotation) * head.forward;
            Vector3 rightAxis = Quaternion.Inverse(right.rotation) * head.forward;
            target.transform.position = new Vector3(0.3f, 0.2f, 1f);
            look.LookAt(target.transform);
            for (int i = 0; i < 120; i++) look.Tick(1f / 60f);
            Assert.That(Vector3.Angle(left.rotation * leftAxis, target.transform.position-left.position), Is.LessThan(0.1f));
            Assert.That(Vector3.Angle(right.rotation * rightAxis, target.transform.position-right.position), Is.LessThan(0.1f));
            look.StopLooking();
            for (int i = 0; i < 120; i++) look.Tick(1f / 60f);
            Assert.That(Quaternion.Angle(left.localRotation, leftRest), Is.LessThan(0.1f));
            Assert.That(Quaternion.Angle(right.localRotation, rightRest), Is.LessThan(0.1f));
        }

        [Test] public void EyesLeadSlowHeadWithoutExceedingTheirLimits()
        {
            look.DragDelay = 10f;
            var eye = root.transform.Find("Head/LeftEye");
            Quaternion rest = eye.localRotation;
            Aim(80f);
            for (int i = 0; i < 10; i++) look.Tick(1f / 60f);
            float turn = Quaternion.Angle(rest, eye.localRotation);
            Assert.That(turn, Is.GreaterThan(20f));
            Assert.That(turn, Is.LessThan(37f)); // combined yaw/pitch cone
            Object.DestroyImmediate(target);
            for (int i = 0; i < 240; i++) look.Tick(1f / 60f);
            Assert.That(look.IsLooking, Is.False);
            Assert.That(Quaternion.Angle(rest, eye.localRotation), Is.LessThan(0.1f));
        }

        [Test] public void ZeroDragTracksImmediatelyIncludingRetargeting()
        {
            look.BodyTurnThresholdDegrees = 0f;
            look.DragDelay = 0f;
            Aim(70f);
            Assert.That(look.RemainingAngle, Is.LessThan(0.01f));
            Aim(-50f);
            Assert.That(look.RemainingAngle, Is.LessThan(0.01f));
        }

        [Test] public void HigherDragTrailsMoreAndConvergesWithoutOvershoot()
        {
            look.BodyTurnThresholdDegrees = 0f;
            target.transform.position = Vector3.right * 2f;
            look.DragDelay = 0.2f;
            look.LookAt(target.transform);
            look.Tick(0.1f);
            float fast = look.RemainingAngle;
            look.StopLooking(); look.Tick(1f);
            look.DragDelay = 0.8f;
            look.LookAt(target.transform);
            look.Tick(0.1f);
            Assert.That(look.RemainingAngle, Is.GreaterThan(fast));
            float previous = look.RemainingAngle;
            for (int i = 0; i < 600; i++)
            {
                look.Tick(1f / 60f);
                Assert.That(look.RemainingAngle, Is.LessThanOrEqualTo(previous + 0.001f));
                previous = look.RemainingAngle;
            }
            Assert.That(look.RemainingAngle, Is.LessThan(0.1f));
        }

        [Test] public void DragIsFrameRateIndependentAndClampsNegativeValues()
        {
            look.BodyTurnThresholdDegrees = 0f;
            look.DragDelay = -1f;
            Assert.That(look.DragDelay, Is.Zero);
            look.DragDelay = 0.4f;
            target.transform.position = Vector3.right * 2f;
            look.LookAt(target.transform);
            for (int i = 0; i < 30; i++) look.Tick(1f / 30f);
            Quaternion at30 = look.LookTransform.rotation;
            look.StopLooking(); look.Tick(1f);
            look.LookAt(target.transform);
            for (int i = 0; i < 60; i++) look.Tick(1f / 60f);
            Assert.That(Quaternion.Angle(at30, look.LookTransform.rotation), Is.LessThan(0.1f));
        }

        [TestCase(89f, false)]
        [TestCase(90f, false)]
        [TestCase(91f, true)]
        [TestCase(-91f, true)]
        [TestCase(180f, true)]
        public void BodyTurnsOnlyBeyondTheLimit(float yaw, bool expected)
        {
            Aim(yaw);
            Assert.That(rotation.IsRotating, Is.EqualTo(expected));
            if (!expected) return;
            for (int i = 0; i < 120; i++) { rotation.Tick(1f / 60f); look.Tick(1f / 60f); }
            Assert.That(rotation.IsRotating, Is.False);
            Assert.That(Vector3.Angle(root.transform.forward, target.transform.position), Is.LessThan(0.6f));
        }

        [Test] public void DisabledBodyTurnsPreserveHeadOnlyTracking()
        {
            look.BodyTurnThresholdDegrees = 0f;
            Aim(120f);
            Assert.That(rotation.IsRotating, Is.False);
            Assert.That(look.IsLooking, Is.True);
        }

        [Test] public void ExistingScriptedTurnKeepsControl()
        {
            var command = rotation.FaceDirection(Vector3.left, NpcRotationOptions.Default,
                new NpcCommandRequest(this)).Handle;
            Aim(120f);
            Assert.That(rotation.ActiveCommand, Is.EqualTo(command));
            look.StopLooking();
            Assert.That(command.IsRunning, Is.True);
        }

        [Test] public void StoppingLookCancelsItsBodyTurn()
        {
            Aim(120f);
            Assert.That(rotation.IsRotating, Is.True);
            look.StopLooking();
            Assert.That(rotation.IsRotating, Is.False);
        }

        [Test] public void NavigationTakesControlAndCancelsTheLookTurn()
        {
            var navigation = npc.AddFeature<NpcNavMeshNavigation>();
            Activate(navigation);
            Aim(120f);
            Assert.That(rotation.IsRotating, Is.True);
            typeof(NpcNavMeshNavigation).GetField("_state", Private).SetValue(navigation, NpcNavigationState.Moving);
            look.Tick(1f / 60f);
            Assert.That(rotation.IsRotating, Is.False);
            look.Tick(1f / 60f);
            Assert.That(rotation.IsRotating, Is.False);
        }

        [Test] public void ConfiguredLimitIsUsedInsteadOfNinetyDegrees()
        {
            look.BodyTurnThresholdDegrees = 45f;
            Aim(46f);
            Assert.That(rotation.IsRotating, Is.True);
        }

        [Test] public void RetargetingCancelsThePreviousBodyTurn()
        {
            Aim(120f);
            var next = new GameObject("next target");
            try
            {
                next.transform.position = Vector3.forward * 2f;
                look.LookAt(next.transform);
                Assert.That(rotation.IsRotating, Is.False);
            }
            finally { Object.DestroyImmediate(next); }
        }
    }
}
