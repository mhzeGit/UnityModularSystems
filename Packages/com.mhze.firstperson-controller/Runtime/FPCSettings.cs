using UnityEngine;

namespace MHZE.FirstPersonController
{
    [CreateAssetMenu(fileName = "FPCSettings", menuName = "MHZE/First Person Controller/Settings", order = 1)]
    public class FPCSettings : ScriptableObject
    {
        [Header("Movement")]
        public float walkSpeed = 4.5f;
        public float runSpeed = 7.5f;
        public float acceleration = 40f;
        public float deceleration = 30f;
        [Range(0f, 1f)] public float airControl = 0.522f;
        [Range(0f, 90f)] public float maxSlopeAngle = 45f;
        public float stepOffset = 0.2f;

        [Header("Crouch")]
        public float crouchHeight = 0.8f;
        public float standHeight = 1.8f;
        public float crouchSpeed = 2.5f;
        public float crouchTransitionSpeed = 8f;
        public bool toggleCrouch = false;

        [Header("Jump")]
        public float jumpForce = 1f;
        public float gravity = -15f;
        public float groundedThreshold = 0.1f;
        public float coyoteTime = 0.15f;
        public float jumpBufferTime = 0.1f;

        [Header("Look")]
        public Vector2 sensitivity = new Vector2(1f, 1f);
        public float smoothTime = 0.04f;
        public Vector2 verticalRange = new Vector2(-90f, 90f);
        public bool invertY = false;
        public float inputMultiplier = 0.1f;

        [Header("Force Look")]
        public float lookAtSpeed = 180f;
        public float returnSpeed = 120f;
        public float tolerance = 1f;

        [Header("Force Move")]
        public float moveSpeed = 5f;
        public float arriveDistance = 0.05f;
        public float stoppingDistance = 0.01f;
        public float forceAcceleration = 15f;

        [Header("State")]
        public bool debugLogging = false;
    }
}