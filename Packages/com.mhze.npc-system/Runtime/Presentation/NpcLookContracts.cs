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

    /// <summary>
    /// Optional companion to <see cref="INpcLook"/>: how strongly the head and the eyes turn toward the
    /// target (0..1 each).
    /// </summary>
    public interface INpcLookWeight
    {
        /// <summary>1 turns the head fully toward the target, 0.5 half way from its animated pose, 0 not at all.</summary>
        float HeadWeight { get; set; }

        /// <summary>1 aims the eyes fully at the target (within their limits), 0.5 half way from rest, 0 not at all.</summary>
        float EyeWeight { get; set; }
    }
}
