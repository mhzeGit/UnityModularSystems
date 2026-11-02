using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace MHZE.PickupSystem.Tests
{
    /// <summary>Checks fixed-shoulder reach, wrist correction and neutral-pose restoration.</summary>
    public sealed class ArmReachTests
    {
        private GameObject body;
        private Transform arm, shoulder, elbow, wrist, socket, anchor;
        private PlayerArmReach reach;

        [SetUp] public void SetUp()
        {
            body = new GameObject("Body");
            arm = Child(body.transform, "Arm", Vector3.zero);
            shoulder = Child(arm, "Shoulder", Vector3.zero);
            elbow = Child(shoulder, "Elbow", Vector3.forward * .3f);
            wrist = Child(elbow, "Wrist", Vector3.forward * .3f);
            socket = Child(wrist, "Socket", Vector3.forward * .1f);
            anchor = Child(body.transform, "Anchor", new Vector3(.35f, -.45f, -.2f));
            reach = arm.gameObject.AddComponent<PlayerArmReach>();
            Set("upperArm", shoulder); Set("forearm", elbow); Set("hand", wrist); Set("socket", socket);
            Invoke("Resolve");
            reach.Begin(socket);
            Set("_weight", 1f);
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(body);

        [Test] public void AnchoredShoulderStaysPutAndSocketReachesDistantTargets()
        {
            Set("shoulderAnchor", anchor);
            foreach (var target in new[] { new Vector3(-2, 2, 4), new Vector3(3, -1, 5), new Vector3(0, 0, .3f) })
            {
                reach.SetTarget(target, (anchor.position - target).normalized);
                Invoke("LateUpdate");
                Assert.That(Vector3.Distance(shoulder.position, anchor.position), Is.LessThan(.0001f));
                Assert.That(Vector3.Distance(socket.position, target), Is.LessThan(.001f));
                Assert.That(wrist.localScale, Is.EqualTo(Vector3.one));
            }
            body.transform.SetPositionAndRotation(new Vector3(2, 1, -3), Quaternion.Euler(15, 70, 0));
            var movedTarget = body.transform.TransformPoint(new Vector3(-1, 1, 3));
            reach.SetTarget(movedTarget, (anchor.position - movedTarget).normalized);
            Invoke("LateUpdate");
            Assert.That(Vector3.Distance(shoulder.position, anchor.position), Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(socket.position, movedTarget), Is.LessThan(.001f));
        }

        [Test] public void EndingAnchoredReachRestoresRootAndBoneLengths()
        {
            Set("shoulderAnchor", anchor);
            reach.SetTarget(new Vector3(3, 2, 5), Vector3.back);
            Invoke("LateUpdate");
            reach.EndNow();
            Assert.That(arm.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(elbow.localPosition, Is.EqualTo(Vector3.forward * .3f));
            Assert.That(wrist.localPosition, Is.EqualTo(Vector3.forward * .3f));
            Assert.That(reach.IsReaching, Is.False);
        }

        [Test] public void UnanchoredReachDoesNotApplyUnlimitedWristCorrection()
        {
            reach.SetTarget(new Vector3(0, 0, 20), Vector3.back);
            Invoke("LateUpdate");
            Assert.That(wrist.localPosition.magnitude, Is.LessThanOrEqualTo(.3f * 2.1f));
            Assert.That(Vector3.Distance(socket.position, new Vector3(0, 0, 20)), Is.GreaterThan(10));
        }

        private void Invoke(string name) => typeof(PlayerArmReach).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(reach, null);
        private void Set(string name, object value) => typeof(PlayerArmReach).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(reach, value);
        private static Transform Child(Transform parent, string name, Vector3 position)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = position;
            return child;
        }
    }
}
