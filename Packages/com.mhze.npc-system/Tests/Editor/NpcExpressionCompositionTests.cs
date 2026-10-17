using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ModularNPC.Tests
{
    /// <summary>Checks held facial offsets across fresh animation writes, unanimated frames and resets.</summary>
    public sealed class NpcExpressionCompositionTests
    {
        private GameObject root;
        private Transform bone;
        private NpcExpressionLibrary library;
        private NpcExpressions face;
        private NpcExpressionLibrary.BonePose[] held;
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp] public void SetUp()
        {
            root = new GameObject("expression test"); bone = root.transform;
            library = ScriptableObject.CreateInstance<NpcExpressionLibrary>();
            library.neutral = new[] { new NpcExpressionLibrary.BonePose { position = Vector3.zero, rotation = Quaternion.identity, scale = Vector3.one } };
            held = new[] { new NpcExpressionLibrary.BonePose { position = new Vector3(.01f, .02f, 0), rotation = Quaternion.Euler(35, 4, 0), scale = new Vector3(1, 1.1f, 1) } };
            face = new NpcExpressions();
            Set("_library", library); Set("_bones", new[] { bone });
            Set("_applied", (NpcExpressionLibrary.BonePose[])library.neutral.Clone());
            bone.localScale = Vector3.one;
        }
        [TearDown] public void TearDown() { Object.DestroyImmediate(root); Object.DestroyImmediate(library); }
        private void Set(string field, object value) => typeof(NpcExpressions).GetField(field, Private).SetValue(face, value);
        private void Apply(NpcExpressionLibrary.BonePose[] pose) => typeof(NpcExpressions).GetMethod("Apply", Private).Invoke(face, new object[] { pose });
        private void AssertHeld(Quaternion animated)
        {
            Assert.That(Quaternion.Angle(bone.localRotation, animated * held[0].rotation), Is.LessThan(.01f));
            Assert.That(Vector3.Distance(bone.localPosition, held[0].position), Is.LessThan(.00001f));
            Assert.That(Vector3.Distance(bone.localScale, held[0].scale), Is.LessThan(.00001f));
        }
        [Test] public void HeldExpressionSurvivesFreshAnimationWritesAndClipSwitch()
        {
            Apply(held);
            for (int frame = 0; frame < 120; frame++)
            {
                face.BeforeAnimation();
                Quaternion animated = Quaternion.Euler(frame < 60 ? 0 : 12, Mathf.Sin(frame) * 3, 0);
                bone.localPosition = Vector3.zero; bone.localRotation = animated; bone.localScale = Vector3.one;
                Apply(held); AssertHeld(animated);
            }
        }
        [Test] public void UnanimatedBonesDoNotAccumulateAcrossFramesOrRepeatedApplications()
        {
            for (int frame = 0; frame < 120; frame++)
            {
                face.BeforeAnimation(); Apply(held); Apply(held); AssertHeld(Quaternion.identity);
            }
        }
        [Test] public void NeutralResetPreservesTheCurrentAnimationPose()
        {
            Apply(held); face.BeforeAnimation();
            var animated = Quaternion.Euler(17, 9, 3); bone.localRotation = animated;
            Apply(held); Apply(library.neutral);
            Assert.That(Quaternion.Angle(bone.localRotation, animated), Is.LessThan(.01f));
            Assert.That(bone.localPosition.sqrMagnitude, Is.LessThan(.0000001f));
            Assert.That(Vector3.Distance(bone.localScale, Vector3.one), Is.LessThan(.00001f));
        }
    }
}
