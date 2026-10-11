using UnityEngine;

namespace ModularNPC
{
    /// <summary>Which of the NPC's arms is, or should be, extended.</summary>
    public enum NpcPointArm
    {
        /// <summary>
        /// As a state: no arm is raised. As a request to <see cref="INpcPointAt.PointAt(Transform, Vector3, NpcPointArm)"/>:
        /// let the feature pick the best arm for the target.
        /// </summary>
        None = 0,
        Left = 1,
        Right = 2
    }

    /// <summary>
    /// Extends one of an NPC's arms in a straight line toward a world position, the way a person
    /// points out a room or a door. The target is whatever the caller passes - the contract has no
    /// notion of a player, a dialog or a level.
    /// </summary>
    public interface INpcPointAt
    {
        /// <summary>True from a successful <c>PointAt</c> until <see cref="StopPointing"/>.</summary>
        bool IsPointing { get; }

        /// <summary>The transform being pointed at, or null when pointing at a fixed point or idle.</summary>
        Transform Target { get; }

        /// <summary>The world position the arm is aimed at, or the current aim when not pointing.</summary>
        Vector3 TargetPosition { get; }

        /// <summary>The arm currently raised or being raised, <see cref="NpcPointArm.None"/> when idle.</summary>
        NpcPointArm ActiveArm { get; }

        /// <summary>
        /// 0 with every arm at its animated pose, 1 with the active arm fully extended. Eased, so it
        /// is safe to drive other presentation (a sound, a grunt) from it.
        /// </summary>
        float Weight { get; }

        /// <summary>
        /// Starts, or retargets, a continuous point at <paramref name="target"/> (plus a world-space
        /// <paramref name="aimOffset"/>, for example "a little above the door pivot"). Pass
        /// <see cref="NpcPointArm.None"/> to let the NPC choose the arm. Returns false when the target is
        /// null, the NPC itself, or no arm bones could be found.
        /// </summary>
        bool PointAt(Transform target, Vector3 aimOffset = default, NpcPointArm arm = NpcPointArm.None);

        /// <summary>Same as above for a fixed world position.</summary>
        bool PointAt(Vector3 worldPosition, NpcPointArm arm = NpcPointArm.None);

        /// <summary>Lowers the arm back to the animated pose.</summary>
        void StopPointing();
    }
}
