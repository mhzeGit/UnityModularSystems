using UnityEngine;

namespace MHZE.FirstPersonController
{
    /// <summary>
    /// IFPCMovementDriver implementation using a Rigidbody + CapsuleCollider (physics-based movement).
    /// The player can be affected by external forces (explosions, knockback) and interact with physics objects.
    /// </summary>
    public class PhysicsDriver : IFPCMovementDriver
    {
        private readonly Rigidbody rb;
        private readonly CapsuleCollider capsule;
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
                // Ground check via spherecast from capsule base
                Vector3 origin = rb.position + Vector3.up * (capsule.radius + 0.05f);
                float castDist = (capsule.height * 0.5f) - capsule.radius + 0.1f;
                return Physics.SphereCast(origin, capsule.radius * 0.9f, Vector3.down,
                    out _, castDist);
            }
        }

        public PhysicsDriver(Rigidbody rigidbody, CapsuleCollider collider)
        {
            rb = rigidbody;
            capsule = collider;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // Attach a bridge MonoBehaviour to handle FixedUpdate
            bridge = rb.gameObject.GetComponent<PhysicsDriverBridge>();
            if (bridge == null)
                bridge = rb.gameObject.AddComponent<PhysicsDriverBridge>();
            bridge.FixedUpdateCallback = OnFixedUpdate;
        }

        public void ApplyMotion(Vector3 motion)
        {
            // Convert motion (world units) to a velocity
            float dt = Time.deltaTime;
            pendingVelocity = dt > 0.001f ? motion / dt : Vector3.zero;
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            // Schedule for next FixedUpdate to avoid physics interference
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

            // Apply velocity (includes gravity + horizontal movement from FPCMovement)
            rb.linearVelocity = pendingVelocity;

            // Keep rotation fixed (only yaw changes via FPCLook)
            // Pitch is handled by the camera child, not the rigidbody
        }

        // Called by the controller when the driver is no longer needed
        public void Cleanup()
        {
            if (bridge != null)
            {
                bridge.FixedUpdateCallback -= OnFixedUpdate;
            }
        }

        // --- Small bridge MonoBehaviour --------------------------
        // Lives on the same GameObject so we get FixedUpdate calls
        // without polluting the main controller.

        [DefaultExecutionOrder(-100)]
        private class PhysicsDriverBridge : MonoBehaviour
        {
            internal System.Action FixedUpdateCallback;
            private void FixedUpdate() => FixedUpdateCallback?.Invoke();
        }
    }
}