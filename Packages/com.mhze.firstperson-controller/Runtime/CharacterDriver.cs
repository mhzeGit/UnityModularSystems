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
                float radius = characterController.radius;
                Vector3 origin = Transform.position + Vector3.up * (radius + 0.05f);
                float castDist = radius + settings.groundCheckDepth;
                return Physics.SphereCast(origin, radius * settings.groundCheckRadiusScale,
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
