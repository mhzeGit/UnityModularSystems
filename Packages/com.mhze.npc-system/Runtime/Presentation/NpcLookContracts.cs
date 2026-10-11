using UnityEngine;

namespace ModularNPC
{
    /// <summary>
    /// Turns one bone (head, eye, turret, aim transform) toward a target transform. The target is
    /// whatever the caller passes - the contract has no notion of a player.
    /// </summary>
    public interface INpcLook
    {
        /// <summary>The bone being turned, or null when none could be found.</summary>
        Transform LookTransform { get; }

        /// <summary>The transform currently being looked at, or null when not looking.</summary>
        Transform Target { get; }

        /// <summary>True from <see cref="LookAt"/> until <see cref="StopLooking"/>.</summary>
        bool IsLooking { get; }

        /// <summary>Degrees left between the bone and its goal (the target, or rest while easing back).</summary>
        float RemainingAngle { get; }

        /// <summary>
        /// Starts, or retargets, a continuous look at <paramref name="target"/>. Returns false when the
        /// target is invalid or the look bone is missing.
        /// </summary>
        bool LookAt(Transform target);

        /// <summary>Ends the look and eases the bone back to its rest pose.</summary>
        void StopLooking();
    }
}
