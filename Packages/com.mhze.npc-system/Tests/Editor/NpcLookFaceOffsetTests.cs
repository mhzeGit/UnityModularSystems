using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ModularNPC.Tests
{
    /// <summary>
    /// Checks Look At Target on a head bone whose own axes do not point out of the face (a four-legged rig's
    /// head bone that runs along the skull): the face rotation offset makes the face, not the bone's +Z, meet
    /// the target, keeps the top of the head up, and measures the yaw limit in the face frame.
    /// </summary>
    public sealed class NpcLookFaceOffsetTests
    {
        private GameObject root, target, scheduler;
        private Transform head;
        private Npc npc;
        private NpcLookAtTarget look;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        // The bone's +Z points straight up and its +Y backwards; this offset turns that into a face looking +Z.
        private static readonly Vector3 Offset = new Vector3(90f, 0f, 0f);

        [SetUp] public void SetUp()
        {
            var schedulerType = typeof(Npc).Assembly.GetType("ModularNPC.NpcScheduler");
            scheduler = new GameObject("test scheduler");
            schedulerType.GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, scheduler.AddComponent(schedulerType));
            root = new GameObject("dog look test");
            var neck = new GameObject("Neck").transform;
            neck.SetParent(root.transform, false);
            neck.localPosition = new Vector3(0f, 0.6f, 0.1f);
            neck.localRotation = Quaternion.Euler(-70f, 0f, 0f);   // a neck lying forward along the body
            head = new GameObject("Head").transform;
            head.SetParent(neck, false);
            head.localPosition = new Vector3(0f, 0f, 0.15f);
            head.rotation = Quaternion.Euler(-90f, 0f, 0f);        // bone +Z up, bone +Y backwards
            target = new GameObject("target");
            npc = root.AddComponent<Npc>();
            look = npc.AddFeature<NpcLookAtTarget>();
            look.FaceRotationOffset = Offset;
            typeof(NpcFeature).GetMethod("HandleInitialize", Private).Invoke(look, new object[] { npc });
            typeof(NpcFeature).GetMethod("HandleOwnerAvailabilityChanged", Private).Invoke(look, new object[] { true });
        }

        [TearDown] public void TearDown()
        {
            look.StopLooking();
            Object.DestroyImmediate(root); Object.DestroyImmediate(target); Object.DestroyImmediate(scheduler);
        }

        private Quaternion Face => head.rotation * Quaternion.Euler(Offset);

        [Test] public void TheFaceMeetsTheTargetWithTheTopOfTheHeadUp()
        {
            target.transform.position = head.position + new Vector3(0.6f, 0.9f, 1.4f);   // a person standing to one side
            Assert.That(look.LookAt(target.transform), Is.True);
            for (int i = 0; i < 30; i++) look.Tick(1f / 60f);
            Vector3 toTarget = target.transform.position - head.position;
            Assert.That(Vector3.Angle(Face * Vector3.forward, toTarget), Is.LessThan(0.1f));
            Assert.That((Face * Vector3.up).y, Is.GreaterThan(0.5f));            // not rolled or upside down
            Assert.That(Vector3.Angle(head.forward, toTarget), Is.GreaterThan(45f)); // the bone itself does not aim
        }

        [Test] public void YawLimitIsMeasuredInTheFaceFrame()
        {
            var limit = typeof(NpcLookAtTarget);
            limit.GetField("_limitAngles", Private).SetValue(look, true);
            limit.GetField("_minimumYaw", Private).SetValue(look, -30f);
            limit.GetField("_maximumYaw", Private).SetValue(look, 30f);
            target.transform.position = head.position + Quaternion.Euler(0f, 70f, 0f) * Vector3.forward * 2f;
            look.LookAt(target.transform);
            for (int i = 0; i < 30; i++) look.Tick(1f / 60f);
            Vector3 face = Face * Vector3.forward;
            float yaw = Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg;
            Assert.That(yaw, Is.EqualTo(30f).Within(0.5f));
        }

        [Test] public void StoppingReturnsTheBoneToItsRestPose()
        {
            Quaternion rest = head.localRotation;
            target.transform.position = head.position + new Vector3(-1f, 0.5f, 1f);
            look.LookAt(target.transform);
            for (int i = 0; i < 30; i++) look.Tick(1f / 60f);
            Assert.That(Quaternion.Angle(rest, head.localRotation), Is.GreaterThan(10f));
            look.StopLooking();
            for (int i = 0; i < 240; i++) look.Tick(1f / 60f);
            Assert.That(Quaternion.Angle(rest, head.localRotation), Is.LessThan(0.6f));
        }
    }
}
