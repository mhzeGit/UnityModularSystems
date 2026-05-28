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

        [Header("Ground Check")]
        [Tooltip("Radius of the ground check sphere as a fraction of the capsule radius.")]
        [Range(0.1f, 1f)]
        public float groundCheckRadiusScale = 0.9f;
        [Tooltip("Distance below the character's feet to check for ground. Larger = easier to stay grounded.")]
        public float groundCheckDepth = 0.1f;

        [Header("Jump")]
        public float jumpForce = 1f;
        public float gravity = -15f;
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

        [Header("Camera Effects")]
        [Tooltip("Widen FOV based on horizontal speed.")]
        public bool enableFovSpeedEffect = true;
        [Tooltip("Camera FOV when standing still.")]
        public float fovBaseValue = 80f;
        [Tooltip("Camera FOV at full speed.")]
        public float fovMaxValue = 90f;
        [Tooltip("Horizontal speed at which FOV effect is fully applied.")]
        public float fovSpeedThreshold = 7f;
        [Tooltip("How fast FOV widens when accelerating (higher = snappier).")]
        public float fovIncreaseSpeed = 8f;
        [Tooltip("How fast FOV narrows when decelerating (higher = snappier).")]
        public float fovDecreaseSpeed = 4f;

        [Header("Jump / Land Camera Effects")]
        [Tooltip("Enable procedural camera effects on jump and landing.")]
        public bool enableJumpLandEffects = true;

        [Header("Jump Effect")]
        [Tooltip("Duration of the jump camera effect in seconds.")]
        public float jumpEffectDuration = 0.3f;
        [Tooltip("Pitch offset over normalized time (0→1). Negative = tilts down.")]
        public AnimationCurve jumpPitchCurve = new AnimationCurve(
            new Keyframe(0f, -4f),
            new Keyframe(0.15f, -1f),
            new Keyframe(0.3f, 0f)
        );

        [Header("Landing Effect")]
        [Tooltip("Duration of the landing camera effect in seconds.")]
        public float landingEffectDuration = 0.3f;
        [Tooltip("Pitch offset over normalized time (0→1) on landing.")]
        public AnimationCurve landingPitchCurve = new AnimationCurve(
            new Keyframe(0f, 2f),
            new Keyframe(0.15f, 0.5f),
            new Keyframe(0.3f, 0f)
        );
        [Tooltip("Y position offset over normalized time (0→1) on landing.")]
        public AnimationCurve landingPositionCurve = new AnimationCurve(
            new Keyframe(0f, -0.08f),
            new Keyframe(0.15f, -0.02f),
            new Keyframe(0.3f, 0f)
        );


        [Header("Headbob")]
        [Tooltip("Headbob settings asset. Controls per-state position/rotation oscillation.")]
        public FPCHeadbobSettings headbobSettings;

        [Header("State")]
        public bool debugLogging = false;
    }
}