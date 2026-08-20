using UnityEngine;

namespace ModularNPC
{
    public enum NpcNavigationState
    {
        Idle,
        CalculatingPath,
        Moving,
        Arrived,
        Unreachable
    }

    /// <summary>Per-command overrides. A default value uses the feature's configured settings.</summary>
    public readonly struct NpcMoveOptions
    {
        public NpcMoveOptions(
            float speed,
            float stoppingDistance,
            bool acceptPartialPath = false)
        {
            Speed = Mathf.Max(0f, speed);
            StoppingDistance = Mathf.Max(0f, stoppingDistance);
            OverrideSpeed = true;
            OverrideStoppingDistance = true;
            AcceptPartialPath = acceptPartialPath;
        }

        public float Speed { get; }

        public float StoppingDistance { get; }

        public bool OverrideSpeed { get; }

        public bool OverrideStoppingDistance { get; }

        public bool AcceptPartialPath { get; }

        public static NpcMoveOptions Default => default;

        public NpcMoveOptions WithSpeed(float speed)
        {
            return new NpcMoveOptions(
                Mathf.Max(0f, speed),
                StoppingDistance,
                true,
                OverrideStoppingDistance,
                AcceptPartialPath);
        }

        public NpcMoveOptions WithStoppingDistance(float stoppingDistance)
        {
            return new NpcMoveOptions(
                Speed,
                Mathf.Max(0f, stoppingDistance),
                OverrideSpeed,
                true,
                AcceptPartialPath);
        }

        public NpcMoveOptions WithPartialPaths(bool acceptPartialPath)
        {
            return new NpcMoveOptions(
                Speed,
                StoppingDistance,
                OverrideSpeed,
                OverrideStoppingDistance,
                acceptPartialPath);
        }

        private NpcMoveOptions(
            float speed,
            float stoppingDistance,
            bool overrideSpeed,
            bool overrideStoppingDistance,
            bool acceptPartialPath)
        {
            Speed = speed;
            StoppingDistance = stoppingDistance;
            OverrideSpeed = overrideSpeed;
            OverrideStoppingDistance = overrideStoppingDistance;
            AcceptPartialPath = acceptPartialPath;
        }
    }

    public interface INpcNavigation
    {
        Vector3 Destination { get; }

        Vector3 Velocity { get; }

        float RemainingDistance { get; }

        NpcNavigationState State { get; }

        bool IsMoving { get; }

        NpcCommandStartResult MoveTo(
            Vector3 destination,
            NpcMoveOptions options,
            NpcCommandRequest commandRequest);

        bool Warp(Vector3 position);
    }
}
