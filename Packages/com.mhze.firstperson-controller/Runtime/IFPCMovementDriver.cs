using UnityEngine;

namespace MHZE.FirstPersonController
{
    /// <summary>
    /// Abstracts the low-level movement mechanism behind a common interface.
    /// Two built-in implementations: CharacterDriver (CharacterController) and PhysicsDriver (Rigidbody).
    /// </summary>
    public interface IFPCMovementDriver
    {
        /// <summary>True when the collider base touches the ground.</summary>
        bool IsGrounded { get; }

        /// <summary>Transform of the player root.</summary>
        Transform Transform { get; }

        /// <summary>Collider radius (read-only, used for ground checks and headroom tests).</summary>
        float ColliderRadius { get; }

        /// <summary>Current collider height.</summary>
        float ColliderHeight { get; set; }

        /// <summary>Current collider center offset.</summary>
        Vector3 ColliderCenter { get; set; }

        /// <summary>True if the collider hit something above on the last Move call (used to stop upward velocity).</summary>
        bool HitCeiling { get; }

        /// <summary>
        /// Move the collider by the given world-space motion vector.
        /// For CharacterDriver: equivalent to CharacterController.Move(motion).
        /// For PhysicsDriver: sets velocity to motion / deltaTime and lets the engine resolve.
        /// </summary>
        void ApplyMotion(Vector3 motion);

        /// <summary>
        /// Instantly move the player to a position/rotation and reset velocities.
        /// </summary>
        void Teleport(Vector3 position, Quaternion rotation);
    }
}