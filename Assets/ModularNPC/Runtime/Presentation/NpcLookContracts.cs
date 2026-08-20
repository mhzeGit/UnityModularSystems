using UnityEngine;

namespace ModularNPC
{
    /// <summary>Per-command overrides. Default values use the feature's configured settings.</summary>
    public readonly struct NpcLookOptions
    {
        public NpcLookOptions(
            float degreesPerSecond,
            float angularTolerance = 0.5f,
            bool keepTracking = false)
        {
            DegreesPerSecond = Mathf.Max(0f, degreesPerSecond);
            AngularTolerance = Mathf.Max(0f, angularTolerance);
            KeepTracking = keepTracking;
            OverrideSpeed = true;
            OverrideTolerance = true;
        }

        public float DegreesPerSecond { get; }

        public float AngularTolerance { get; }

        public bool KeepTracking { get; }

        public bool OverrideSpeed { get; }

        public bool OverrideTolerance { get; }

        public static NpcLookOptions Default => default;

        public NpcLookOptions WithTracking(bool keepTracking)
        {
            return new NpcLookOptions(
                DegreesPerSecond,
                AngularTolerance,
                keepTracking,
                OverrideSpeed,
                OverrideTolerance);
        }

        private NpcLookOptions(
            float degreesPerSecond,
            float angularTolerance,
            bool keepTracking,
            bool overrideSpeed,
            bool overrideTolerance)
        {
            DegreesPerSecond = degreesPerSecond;
            AngularTolerance = angularTolerance;
            KeepTracking = keepTracking;
            OverrideSpeed = overrideSpeed;
            OverrideTolerance = overrideTolerance;
        }
    }

    public interface INpcLook
    {
        Transform LookTransform { get; }

        bool IsLooking { get; }

        float RemainingAngle { get; }

        NpcCommandStartResult LookAtPosition(
            Vector3 worldPosition,
            NpcLookOptions options,
            NpcCommandRequest commandRequest);

        NpcCommandStartResult LookInDirection(
            Vector3 worldDirection,
            NpcLookOptions options,
            NpcCommandRequest commandRequest);

        NpcCommandStartResult LookAtTarget(
            Transform target,
            NpcLookOptions options,
            NpcCommandRequest commandRequest);

        NpcCommandStartResult ReturnToRest(
            NpcLookOptions options,
            NpcCommandRequest commandRequest);
    }
}
