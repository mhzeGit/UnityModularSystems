using NUnit.Framework;
using UnityEngine;

namespace ModularNPC.Tests
{
    /// <summary>Checks the spring behind Bone Jiggle: it lags, overshoots, settles, and never drifts or whips on a warp.</summary>
    public sealed class NpcBoneJiggleTests
    {
        private const float Frame = 1f / 60f;

        private static Vector3 Run(ref NpcJiggleSpring spring, Vector3 rest, float seconds, float damping = 0.3f)
        {
            Vector3 lag = Vector3.zero;
            for (float t = 0f; t < seconds; t += Frame)
            {
                lag = spring.Advance(rest, Frame, 4f, damping, 0.6f);
            }

            return lag;
        }

        [Test] public void StillRestPointStaysAtRest()
        {
            var spring = new NpcJiggleSpring();
            Assert.AreEqual(0f, Run(ref spring, Vector3.up, 2f).magnitude, 1e-6f);
        }

        [Test] public void SteadyWalkLeavesNoPermanentLag()
        {
            var spring = new NpcJiggleSpring();
            spring.Advance(Vector3.zero, Frame, 4f, 0.3f, 0.6f);
            Vector3 lag = Vector3.zero;
            for (int i = 1; i <= 240; i++)
            {
                lag = spring.Advance(Vector3.forward * (1.4f * Frame * i), Frame, 4f, 0.3f, 0.6f);
            }

            Assert.Less(lag.magnitude, 1e-3f);
        }

        [Test] public void StepChangeLagsOvershootsAndSettles()
        {
            var spring = new NpcJiggleSpring();
            spring.Advance(Vector3.zero, Frame, 4f, 0.3f, 0.6f);
            Vector3 rest = Vector3.right * 0.05f;
            float firstLag = spring.Advance(rest, Frame, 4f, 0.3f, 0.6f).x;
            Assert.Less(firstLag, 0f, "the spring starts behind the rest point");

            bool overshot = false;
            for (int i = 0; i < 90; i++)
            {
                if (spring.Advance(rest, Frame, 4f, 0.3f, 0.6f).x > 0.001f)
                {
                    overshot = true;
                }
            }

            Assert.IsTrue(overshot, "a low damping ratio rings past the rest point");
            Assert.Less(Run(ref spring, rest, 3f).magnitude, 1e-3f, "and settles");
        }

        [Test] public void WarpRestartsInsteadOfWhipping()
        {
            var spring = new NpcJiggleSpring();
            spring.Advance(Vector3.zero, Frame, 4f, 0.3f, 0.6f);
            Assert.AreEqual(0f, spring.Advance(Vector3.right * 5f, Frame, 4f, 0.3f, 0.6f).magnitude, 1e-6f);
        }

        [Test] public void LongFrameStaysFinite()
        {
            var spring = new NpcJiggleSpring();
            spring.Advance(Vector3.zero, Frame, 12f, 0.05f, 0.6f);
            Vector3 lag = spring.Advance(Vector3.right * 0.2f, 0.1f, 12f, 0.05f, 0.6f);
            Assert.IsFalse(float.IsNaN(lag.x) || float.IsInfinity(lag.x));
            Assert.Less(lag.magnitude, 1f);
        }

        [Test] public void PositionOffsetScalesAndCaps()
        {
            Vector3 lag = Vector3.right * 0.04f;
            Assert.AreEqual(0f, NpcBoneJiggle.PositionOffset(lag, 0f, 0.03f).magnitude, 1e-6f, "0 = rotation only");
            Assert.AreEqual(0.02f, NpcBoneJiggle.PositionOffset(lag, 0.5f, 0.03f).x, 1e-6f);
            Assert.AreEqual(0.03f, NpcBoneJiggle.PositionOffset(lag, 2f, 0.03f).magnitude, 1e-6f, "never beyond the cap");
        }
    }
}
