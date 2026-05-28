using UnityEngine;

namespace MHZE.FirstPersonController
{
    public class CharacterDriver : IFPCMovementDriver
    {
        private readonly CharacterController characterController;
        private readonly FPCSettings settings;

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

        public bool IsGrounded
        {
            get
            {
                // Spherecast from just above the capsule base downward
                Vector3 bottom = Transform.position
                    + characterController.center
                    - Vector3.up * (characterController.height * 0.5f);
                Vector3 origin = bottom + Vector3.up * (characterController.radius + settings.groundCheckRaise);
                float castDist = (characterController.height * 0.5f)
                    - characterController.radius
                    + settings.groundCheckDepth;
                return Physics.SphereCast(origin, characterController.radius * settings.groundCheckRadiusScale,
                    Vector3.down, out _, castDist);
            }
        }

        public CharacterDriver(CharacterController cc, FPCSettings settings)
        {
            characterController = cc;
            this.settings = settings;
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
