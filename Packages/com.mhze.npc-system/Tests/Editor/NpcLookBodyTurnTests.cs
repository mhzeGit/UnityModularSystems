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
