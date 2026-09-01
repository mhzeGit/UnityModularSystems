using UnityEngine;

namespace ModularNPC
{
    public enum NpcRotationAxes
    {
        AllAxes,
        YawOnly
    }

    /// <summary>Per-command overrides. Default values use the feature's configured settings.</summary>
    public readonly struct NpcRotationOptions
    {
        public NpcRotationOptions(
            float degreesPerSecond,
            float angularTolerance = 0.5f,
            bool keepTracking = false)
        {
            DegreesPerSecond = Mathf.Max(0f, degreesPerSecond);
            AngularTolerance = Mathf.Max(0f, angularTolerance);
            OverrideSpeed = true;
            OverrideTolerance = true;
            KeepTracking = keepTracking;
        }

        public float DegreesPerSecond { get; }

        public float AngularTolerance { get; }

        public bool OverrideSpeed { get; }

        public bool OverrideTolerance { get; }

        public bool KeepTracking { get; }

        public static NpcRotationOptions Default => default;

        public NpcRotationOptions WithTracking(bool keepTracking)
        {
            return new NpcRotationOptions(
                DegreesPerSecond,
                AngularTolerance,
                OverrideSpeed,
                OverrideTolerance,
                keepTracking);
        }

        private NpcRotationOptions(
            float degreesPerSecond,
            float angularTolerance,
            bool overrideSpeed,
            bool overrideTolerance,
            bool keepTracking)
        {
            DegreesPerSecond = degreesPerSecond;
            AngularTolerance = angularTolerance;
            OverrideSpeed = overrideSpeed;
            OverrideTolerance = overrideTolerance;
            KeepTracking = keepTracking;
        }
    }

    public interface INpcRotation
    {
        bool IsRotating { get; }

        float RemainingAngle { get; }

        NpcCommandStartResult FacePosition(
            Vector3 worldPosition,
            NpcRotationOptions options,
            NpcCommandRequest commandRequest);

        NpcCommandStartResult FaceDirection(
            Vector3 worldDirection,
            NpcRotationOptions options,
            NpcCommandRequest commandRequest);

        NpcCommandStartResult FaceTarget(
            Transform target,
            NpcRotationOptions options,
            NpcCommandRequest commandRequest);

        NpcCommandStartResult RotateTo(
            Quaternion worldRotation,
            NpcRotationOptions options,
            NpcCommandRequest commandRequest);
    }
}
