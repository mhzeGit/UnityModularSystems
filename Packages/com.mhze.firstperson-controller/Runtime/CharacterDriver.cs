using UnityEngine;

namespace MHZE.FirstPersonController
{
    /// <summary>
    /// IFPCMovementDriver implementation using Unity's CharacterController (kinematic movement).
    /// This provides step-offset, slope-limit, and built-in grounded detection.
    /// </summary>
    public class CharacterDriver : IFPCMovementDriver
    {
        private readonly CharacterController characterController;

        public Transform Transform => characterController.transform;
        public float ColliderRadius => characterController.radius;
        public float ColliderHeight
        {
            get => characterController.height;
            set => characterController.height = value;
        }
        public Vector3 ColliderCenter
        {
            get => characterController.center;
            set => characterController.center = value;
        }
        public bool HitCeiling =>
            (characterController.collisionFlags & CollisionFlags.Above) != 0;

        public bool IsGrounded => characterController.isGrounded;

        public CharacterDriver(CharacterController cc)
        {
            characterController = cc;
        }

        public void ApplyMotion(Vector3 motion)
        {
            characterController.Move(motion);
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            characterController.enabled = false;
            characterController.transform.SetPositionAndRotation(position, rotation);
            characterController.enabled = true;
        }
    }
}