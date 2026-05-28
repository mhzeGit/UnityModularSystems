using UnityEngine;

namespace MHZE.FirstPersonController
{
    public class PhysicsDriver : IFPCMovementDriver
    {
        private readonly Rigidbody rb;
        private readonly CapsuleCollider capsule;
        private readonly FPCSettings settings;
        private readonly PhysicsDriverBridge bridge;

        private Vector3 pendingVelocity;
        private bool grounded;
        private Vector3 cachedPosition;
        private bool pendingTeleport;
        private Vector3 teleportPosition;
        private Quaternion teleportRotation;

        public Transform Transform => rb.transform;
        public float ColliderRadius => capsule.radius;
        public float ColliderHeight
        {
            get => capsule.height;
            set => capsule.height = value;
        }
        public Vector3 ColliderCenter
        {
            get => capsule.center;
            set => capsule.center = value;
        }
        public bool HitCeiling => false;

        public bool IsGrounded
        {
            get
            {
                // Spherecast from just above the capsule base downward
                Vector3 bottom = rb.position
                    + capsule.center
                    - Vector3.up * (capsule.height * 0.5f);
                Vector3 origin = bottom + Vector3.up * (capsule.radius + settings.groundCheckRaise);
                float castDist = (capsule.height * 0.5f)
                    - capsule.radius
                    + settings.groundCheckDepth;
                return Physics.SphereCast(origin, capsule.radius * settings.groundCheckRadiusScale,
                    Vector3.down, out _, castDist);
            }
        }

        public PhysicsDriver(Rigidbody rigidbody, CapsuleCollider collider, FPCSettings settings)
        {
            rb = rigidbody;
            capsule = collider;
            this.settings = settings;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            bridge = rb.gameObject.GetComponent<PhysicsDriverBridge>();
            if (bridge == null)
                bridge = rb.gameObject.AddComponent<PhysicsDriverBridge>();
            bridge.FixedUpdateCallback = OnFixedUpdate;
        }

        public void ApplyMotion(Vector3 motion)
        {
            float dt = Time.deltaTime;
            pendingVelocity = dt > 0.001f ? motion / dt : Vector3.zero;
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            pendingTeleport = true;
            teleportPosition = position;
            teleportRotation = rotation;
            pendingVelocity = Vector3.zero;
        }

        private void OnFixedUpdate()
        {
            if (pendingTeleport)
            {
                rb.isKinematic = true;
                rb.position = teleportPosition;
                rb.rotation = teleportRotation;
                rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
                pendingTeleport = false;
                return;
            }

            rb.linearVelocity = pendingVelocity;
        }

        public void Cleanup()
        {
            if (bridge != null)
            {
                bridge.FixedUpdateCallback -= OnFixedUpdate;
            }
        }

        [DefaultExecutionOrder(-100)]
        private class PhysicsDriverBridge : MonoBehaviour
        {
            internal System.Action FixedUpdateCallback;
            private void FixedUpdate() => FixedUpdateCallback?.Invoke();
        }
    }
}
