using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ModularNPC.Tests
{
    /// <summary>
    /// Checks Look At Target's angle limits on a head whose target goes behind the character: with a swing speed
    /// the head never passes its limits, swings across the front when the target changes sides and keeps its side
    /// while the target is close to directly behind; after the look it eases back with a body that turns.
    /// </summary>
    public sealed class NpcLookLimitTests
    {
        private GameObject root, target, scheduler;
        private Transform neck, head;
        private Npc npc;
        private NpcLookAtTarget look;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const float Frame = 1f / 60f;

        [SetUp] public void SetUp()
        {
            var schedulerType = typeof(Npc).Assembly.GetType("ModularNPC.NpcScheduler");
            scheduler = new GameObject("test scheduler");
            schedulerType.GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, scheduler.AddComponent(schedulerType));
            root = new GameObject("look limit test");
            neck = new GameObject("Neck").transform;
            neck.SetParent(root.transform, false);
            neck.localPosition = new Vector3(0f, 1f, 0f);
            head = new GameObject("Head").transform;
            head.SetParent(neck, false);
            head.localPosition = new Vector3(0f, 0.1f, 0f);
            target = new GameObject("target");
            npc = root.AddComponent<Npc>();
            look = npc.AddFeature<NpcLookAtTarget>();
            Set("_limitAngles", true);
            Set("_minimumYaw", -80f);
            Set("_maximumYaw", 80f);
            typeof(NpcFeature).GetMethod("HandleInitialize", Private).Invoke(look, new object[] { npc });
            typeof(NpcFeature).GetMethod("HandleOwnerAvailabilityChanged", Private).Invoke(look, new object[] { true });
        }

        [TearDown] public void TearDown()
        {
            look.StopLooking();
            Object.DestroyImmediate(root); Object.DestroyImmediate(target); Object.DestroyImmediate(scheduler);
        }

        private void Set(string field, object value) =>
            typeof(NpcLookAtTarget).GetField(field, Private).SetValue(look, value);

        // The head's yaw as seen from the body.
        private float Yaw
        {
            get
            {
                Vector3 f = root.transform.InverseTransformDirection(head.forward);
                return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            }
        }

        private void PlaceTarget(float bearing) =>
            target.transform.position = head.position +
                root.transform.rotation * Quaternion.Euler(0f, bearing, 0f) * Vector3.forward * 2f;

        private void Run(int frames)
        {
            for (int i = 0; i < frames; i++) look.Tick(Frame);
        }

        [Test] public void WithoutASwingSpeedTheLimitedHeadSnaps()
        {
            PlaceTarget(130f);
            look.LookAt(target.transform);
            Run(1);
            Assert.That(Yaw, Is.EqualTo(80f).Within(0.5f));
            PlaceTarget(-130f);
            Run(1);
            Assert.That(Yaw, Is.EqualTo(-80f).Within(0.5f));
        }

        [Test] public void ANewLookEasesInAtTheSwingSpeed()
        {
            Set("_limitedDegreesPerSecond", 360f);
            PlaceTarget(60f);
            look.LookAt(target.transform);
            Run(1);
            Assert.That(Yaw, Is.EqualTo(360f * Frame).Within(0.5f));
            Run(30);
            Assert.That(Yaw, Is.EqualTo(60f).Within(0.5f));
        }

        [Test] public void ATargetCrossingBehindSwingsTheHeadAcrossTheFront()
        {
            Set("_limitedDegreesPerSecond", 360f);
            PlaceTarget(130f);
            look.LookAt(target.transform);
            Run(30);
            Assert.That(Yaw, Is.EqualTo(80f).Within(0.5f));
            PlaceTarget(-130f);
            float last = Yaw;
            for (int i = 0; i < 40; i++)
            {
                look.Tick(Frame);
                float yaw = Yaw;
                Assert.That(Mathf.Abs(yaw), Is.LessThan(80.5f));
                Assert.That(yaw, Is.LessThan(last + 0.1f));
                Assert.That(last - yaw, Is.LessThan(360f * Frame + 0.5f));
                last = yaw;
            }
            Assert.That(last, Is.EqualTo(-80f).Within(0.5f));
        }

        [Test] public void ATargetNearlyDirectlyBehindDoesNotMakeTheHeadChangeSides()
        {
            Set("_limitedDegreesPerSecond", 360f);
            PlaceTarget(130f);
            look.LookAt(target.transform);
            Run(30);
            foreach (float bearing in new[] { 179f, -179f, -160f, 170f, -170f })
            {
                PlaceTarget(bearing);
                Run(5);
                Assert.That(Yaw, Is.EqualTo(80f).Within(0.5f), "bearing " + bearing);
            }
        }

        [Test] public void ASpinningBodyNeverTwistsTheHeadPastItsLimits()
        {
            Set("_limitedDegreesPerSecond", 540f);
            PlaceTarget(0f);
            look.LookAt(target.transform);
            Run(10);
            for (int i = 1; i <= 120; i++)
            {
                root.transform.rotation = Quaternion.Euler(0f, -12f * i, 0f);
                look.Tick(Frame);
                Assert.That(Mathf.Abs(Yaw), Is.LessThan(80.5f), "frame " + i);
            }
        }

        [Test] public void EasingBackFollowsABodyThatTurns()
        {
            PlaceTarget(70f);
            look.LookAt(target.transform);
            Run(5);
            look.StopLooking();
            for (int i = 1; i <= 40; i++)
            {
                float before = Mathf.Abs(Yaw);
                root.transform.rotation = Quaternion.Euler(0f, -12f * i, 0f);
                look.Tick(Frame);
                Assert.That(Mathf.Abs(Yaw), Is.LessThan(before + 0.5f), "frame " + i);
            }
            Assert.That(Mathf.Abs(Yaw), Is.LessThan(0.6f));
        }
    }
}
