using System;
using MHZE.FirstPersonController;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Checks bounded camera input, release continuity and temporary lock ownership.</summary>
public class SoftLookLockTests
{
    private GameObject root, pivot;
    private FPCSettings settings;
    private FPCLook look;
    private FPCInput input;
    private readonly object owner = new object();

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Look test");
        pivot = new GameObject("Pivot");
        pivot.transform.SetParent(root.transform);
        settings = ScriptableObject.CreateInstance<FPCSettings>();
        settings.inputMultiplier = 1;
        settings.sensitivity = Vector2.one;
        settings.smoothTime = 0;
        look = new FPCLook(new CharacterDriver(root.AddComponent<CharacterController>(), settings), pivot.transform, settings);
        input = new FPCInput();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(settings);
    }

    private void Move(float yaw, float pitch = 0, float dt = 1f / 60f)
    {
        typeof(FPCInput).GetProperty(nameof(FPCInput.LookInput)).SetValue(input, new Vector2(yaw, -pitch));
        look.Update(input, dt);
    }

    [Test]
    public void RepeatedInputStaysBoundedAndReversesImmediately()
    {
        look.TryBeginSoftLookLock(owner, new Vector2(6, 4), 0);
        for (int i = 0; i < 500; i++) Move(50, -50);
        Assert.That(Mathf.DeltaAngle(0, look.Yaw), Is.EqualTo(6).Within(.001f));
        Assert.That(look.Pitch, Is.EqualTo(-4).Within(.001f));
        Move(-1, 1);
        Assert.That(Mathf.DeltaAngle(0, look.Yaw), Is.EqualTo(5).Within(.001f));
        Assert.That(look.Pitch, Is.EqualTo(-3).Within(.001f));
        for (int i = 0; i < 500; i++) Move(-50, 50);
        Assert.That(Mathf.DeltaAngle(0, look.Yaw), Is.EqualTo(-6).Within(.001f));
        Assert.That(look.Pitch, Is.EqualTo(4).Within(.001f));
    }

    [Test]
    public void ResistanceIncreasesAndSplitInputGivesSameResult()
    {
        look.TryBeginSoftLookLock(owner, new Vector2(6, 4), 0);
        Move(1);
        float first = look.Yaw;
        Move(1);
        float twoSteps = look.Yaw;
        Assert.That(twoSteps - first, Is.LessThan(first));
        root.transform.rotation = Quaternion.identity;
        look.SyncWithTransform();
        look.TryBeginSoftLookLock(owner, new Vector2(6, 4), 0);
        Move(2);
        Assert.That(look.Yaw, Is.EqualTo(twoSteps).Within(.001f));
    }

    [Test]
    public void SmoothedFlickAndReleaseDoNotSnapOrQueueHiddenMovement()
    {
        look.TryBeginSoftLookLock(owner, new Vector2(6, 4), .12f);
        Move(10000, 10000);
        float yaw = look.Yaw, pitch = look.Pitch;
        Assert.That(yaw, Is.InRange(.01f, 2f));
        Assert.That(pitch, Is.InRange(.01f, 2f));
        look.EndSoftLookLock(owner);
        Move(0);
        Assert.That(look.Yaw, Is.EqualTo(yaw).Within(.001f));
        Assert.That(look.Pitch, Is.EqualTo(pitch).Within(.001f));
        Move(30);
        Assert.That(Mathf.DeltaAngle(yaw, look.Yaw), Is.EqualTo(30).Within(.001f));
    }

    [Test]
    public void WraparoundAndExistingPitchRangeAreRespected()
    {
        root.transform.rotation = Quaternion.Euler(0, 359, 0);
        pivot.transform.localRotation = Quaternion.Euler(88, 0, 0);
        look.SyncWithTransform();
        look.TryBeginSoftLookLock(owner, new Vector2(6, 4), 0);
        for (int i = 0; i < 100; i++) Move(100, 100);
        Assert.That(Mathf.DeltaAngle(359, look.Yaw), Is.EqualTo(6).Within(.001f));
        Assert.That(look.Pitch, Is.EqualTo(90).Within(.001f));
    }

    [Test]
    public void OwnershipAndScriptedPlacementCannotLeaveAStaleLock()
    {
        var other = new object();
        Assert.That(look.TryBeginSoftLookLock(owner, new Vector2(6, 4), 0), Is.True);
        Assert.That(look.TryBeginSoftLookLock(other, Vector2.one), Is.False);
        Move(3);
        Assert.That(look.TryBeginSoftLookLock(owner, Vector2.one), Is.True);
        look.EndSoftLookLock(other);
        Assert.That(look.IsSoftLookLocked, Is.True);
        Move(1000);
        Assert.That(look.Yaw, Is.EqualTo(6).Within(.001f));
        look.SyncWithTransform();
        Assert.That(look.IsSoftLookLocked, Is.False);
        look.TryBeginSoftLookLock(owner, Vector2.one);
        look.ForceLookAt(Vector3.forward * 10);
        Assert.That(look.IsSoftLookLocked, Is.False);
        Assert.That(look.TryBeginSoftLookLock(owner, Vector2.one), Is.False);
    }

    [Test]
    public void ZeroAxisPauseSensitivityAndInvertYWork()
    {
        look.TryBeginSoftLookLock(owner, new Vector2(0, 4), 0);
        Move(50, 50, 0);
        Move(0);
        Assert.That(look.Pitch, Is.Zero);
        look.SetInvertY(true);
        look.SetSensitivityMultiplier(2);
        Move(50, 1);
        Assert.That(look.Yaw, Is.Zero);
        Assert.That(look.Pitch, Is.EqualTo(-4 * (1 - Mathf.Exp(-.5f))).Within(.001f));
    }

    [Test]
    public void InvalidLimitsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => look.TryBeginSoftLookLock(null, Vector2.one));
        foreach (float invalid in new[] { -1f, 180f, float.NaN, float.PositiveInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => look.TryBeginSoftLookLock(owner, new Vector2(invalid, 4)));
        Assert.Throws<ArgumentOutOfRangeException>(() => look.TryBeginSoftLookLock(owner, Vector2.one, float.NaN));
    }
}
