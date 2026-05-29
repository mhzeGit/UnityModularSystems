// Core movement logic for the first person controller. Handles horizontal movement with acceleration/deceleration, gravity, jumping with coyote time and jump buffer, crouch with smooth height transition, slope compensation, and force-move towards a target position. Detects ground state transitions (grounded, jumping, falling, landing) and fires corresponding events. Works with any IFPCMovementDriver.

using System;
using UnityEngine;

namespace ModularSystems.FirstPersonController
{
    public class FPCMovement
    {
        private readonly IFPCMovementDriver driver;
        private readonly FPCSettings settings;

        // Core velocities
        private Vector3 moveVelocity;
        private float verticalVelocity;
        private bool wasGrounded;

        // Crouch state
        private bool isCrouching;
        private float currentHeight;
        private float targetHeight;

        // Jump state
        private float coyoteTimer;
        private float jumpBufferTimer;

        // Force move state
        private Vector3? forceMoveTarget;
        private float forceMoveSpeed;
        private Action forceMoveCallback;
        private bool isForceMoving;

        // Posture tracking
        private FPCPosture currentPosture;

        // --- Public state ----------------------------------------

        public bool IsGrounded { get; private set; }
        public FPCGroundState GroundState { get; private set; }
        public bool IsCrouching => isCrouching;
        public Vector3 Velocity => moveVelocity + Vector3.up * verticalVelocity;
        public bool IsMoving => moveVelocity.sqrMagnitude > 0.01f;

        // --- Events ----------------------------------------------

        public event Action OnGrounded;
        public event Action OnAirborne;
        public event Action OnJumped;
        public event Action OnCrouchStarted;
        public event Action OnCrouchEnded;

        public FPCMovement(IFPCMovementDriver movementDriver, FPCSettings settings)
        {
            this.driver = movementDriver;
            this.settings = settings;

            currentHeight = settings.standHeight;
            targetHeight = settings.standHeight;
            currentPosture = FPCPosture.Standing;

            driver.ColliderHeight = settings.standHeight;
            driver.ColliderCenter = new Vector3(0f, settings.standHeight * 0.5f, 0f);

            GroundState = FPCGroundState.Grounded;
        }

        public void Update(FPCInput input, float deltaTime)
        {
            wasGrounded = IsGrounded;
            IsGrounded = driver.IsGrounded;

            // --- Coyote time ----------------------------------------
            if (IsGrounded)
                coyoteTimer = settings.coyoteTime;
            else
                coyoteTimer -= deltaTime;

            // --- Jump buffer ---------------------------------------
            if (input.JumpPressed)
                jumpBufferTimer = settings.jumpBufferTime;
            else
                jumpBufferTimer -= deltaTime;

            // --- Ground-state detection -----------------------------
            UpdateGroundState();

            // --- Crouch ---------------------------------------------
            UpdateCrouch(input);

            // --- Movement direction ---------------------------------
            Vector3 inputDir = new Vector3(input.MoveInput.x, 0f, input.MoveInput.y);
            if (inputDir.sqrMagnitude > 1f) inputDir.Normalize();
            Vector3 worldMove = driver.Transform.TransformDirection(inputDir);

            // --- Target speed --------------------------------------
            float maxSpeed = isCrouching ? settings.crouchSpeed
                          : (input.SprintHeld ? settings.runSpeed : settings.walkSpeed);

            Vector3 targetVelocity = worldMove * maxSpeed;

            // --- Acceleration / deceleration -----------------------
            float effectiveAccel = IsGrounded
                ? settings.acceleration : settings.acceleration * settings.airControl;
            float effectiveDecel = IsGrounded
                ? settings.deceleration : settings.deceleration * settings.airControl;

            // --- Force-move override -------------------------------
            if (isForceMoving && forceMoveTarget.HasValue)
            {
                Vector3 toTarget = forceMoveTarget.Value - driver.Transform.position;
                toTarget.y = 0f;
                float dist = toTarget.magnitude;

                if (dist <= settings.arriveDistance)
                {
                    isForceMoving = false;
                    forceMoveTarget = null;
                    var cb = forceMoveCallback;
                    forceMoveCallback = null;
                    cb?.Invoke();
                    targetVelocity = Vector3.zero;
                }
                else
                {
                    float speed = Mathf.Min(forceMoveSpeed,
                        dist / Mathf.Max(forceMoveSpeed * 0.25f, 0.01f) * forceMoveSpeed);
                    targetVelocity = toTarget.normalized * speed;
                    effectiveAccel = settings.forceAcceleration;
                    effectiveDecel = settings.forceAcceleration;
                }
            }

            // --- Horizontal velocity -------------------------------
            Vector3 hVelocity = new Vector3(moveVelocity.x, 0f, moveVelocity.z);

            if (targetVelocity.sqrMagnitude > 0.001f)
            {
                hVelocity = Vector3.MoveTowards(hVelocity, targetVelocity, effectiveAccel * deltaTime);
            }
            else
            {
                hVelocity = Vector3.MoveTowards(hVelocity, Vector3.zero, effectiveDecel * deltaTime);
            }

            // --- Slope compensation --------------------------------
            if (IsGrounded && worldMove.sqrMagnitude > 0.01f)
            {
                if (Physics.Raycast(driver.Transform.position + Vector3.up * 0.1f,
                    Vector3.down, out RaycastHit slopeHit, 1.5f))
                {
                    float angle = Vector3.Angle(slopeHit.normal, Vector3.up);
                    if (angle > 0f && angle <= settings.maxSlopeAngle)
                    {
                        Vector3 slopeDir = Vector3.ProjectOnPlane(worldMove, slopeHit.normal).normalized;
                        hVelocity = slopeDir * maxSpeed;
                    }
                }
            }

            // --- Gravity & Jump -----------------------------------
            if (IsGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f;

            if (jumpBufferTimer > 0f && coyoteTimer > 0f && !isCrouching)
            {
                verticalVelocity = Mathf.Sqrt(settings.jumpForce * -2f * settings.gravity);
                jumpBufferTimer = -1f;
                coyoteTimer = -1f;
                OnJumped?.Invoke();
            }

            verticalVelocity += settings.gravity * deltaTime;
            float terminal = Mathf.Sqrt(settings.gravity * -2f * 50f);
            verticalVelocity = Mathf.Max(verticalVelocity, -terminal);

            // --- Smooth crouch height ----------------------------
            currentHeight = Mathf.Lerp(currentHeight, targetHeight,
                settings.crouchTransitionSpeed * deltaTime);
            if (Mathf.Abs(currentHeight - targetHeight) < 0.005f)
                currentHeight = targetHeight;

            float prevHeight = driver.ColliderHeight;
            driver.ColliderHeight = currentHeight;
            float hDelta = driver.ColliderHeight - prevHeight;
            driver.ColliderCenter += new Vector3(0f, hDelta * 0.5f, 0f);

            // Posture
            if (isCrouching)
                currentPosture = Mathf.Abs(currentHeight - settings.crouchHeight) < 0.01f
                    ? FPCPosture.Crouching : FPCPosture.TransitioningToCrouch;
            else
                currentPosture = Mathf.Abs(currentHeight - settings.standHeight) < 0.01f
                    ? FPCPosture.Standing : FPCPosture.TransitioningToStand;

            // --- Store horizontal ---------------------------------
            moveVelocity = new Vector3(hVelocity.x, 0f, hVelocity.z);

            // --- Apply final movement -----------------------------
            Vector3 finalVelocity = moveVelocity + Vector3.up * verticalVelocity;
            driver.ApplyMotion(finalVelocity * deltaTime);

            // Ceiling check (CharacterController: built-in; PhysicsDriver: ignored)
            if (driver.HitCeiling && verticalVelocity > 0f)
                verticalVelocity = 0f;
        }

        // --- Crouch ----------------------------------------------

        private void UpdateCrouch(FPCInput input)
        {
            bool prevCrouching = isCrouching;

            if (settings.toggleCrouch)
            {
                if (input.CrouchPressed)
                {
                    if (isCrouching)
                    {
                        if (HasRoomToStand())
                        {
                            isCrouching = false;
                            targetHeight = settings.standHeight;
                        }
                    }
                    else
                    {
                        isCrouching = true;
                        targetHeight = settings.crouchHeight;
                    }
                }
            }
            else
            {
                if (input.CrouchHeld)
                {
                    if (!isCrouching)
                    {
                        isCrouching = true;
                        targetHeight = settings.crouchHeight;
                    }
                }
                else if (isCrouching)
                {
                    if (HasRoomToStand())
                    {
                        isCrouching = false;
                        targetHeight = settings.standHeight;
                    }
                }
            }

            if (isCrouching != prevCrouching)
            {
                if (isCrouching) OnCrouchStarted?.Invoke();
                else OnCrouchEnded?.Invoke();
            }
        }

        // --- Ground-state transitions ----------------------------

        private void UpdateGroundState()
        {
            bool landed = IsGrounded && !wasGrounded;
            bool leftGround = !IsGrounded && wasGrounded;

            if (landed)
            {
                GroundState = verticalVelocity < 0f
                    ? FPCGroundState.Landing : FPCGroundState.Grounded;
                OnGrounded?.Invoke();
            }
            else if (leftGround)
            {
                GroundState = verticalVelocity > 0f
                    ? FPCGroundState.Jumping : FPCGroundState.Falling;
                if (GroundState == FPCGroundState.Falling)
                    OnAirborne?.Invoke();
            }
            else if (IsGrounded && GroundState == FPCGroundState.Landing)
            {
                GroundState = FPCGroundState.Grounded;
            }
        }

        // --- Public API ------------------------------------------

        public void Jump() => jumpBufferTimer = settings.jumpBufferTime;

        public void SetCrouch(bool crouch)
        {
            if (crouch == isCrouching) return;
            if (crouch)
            {
                isCrouching = true;
                targetHeight = settings.crouchHeight;
                OnCrouchStarted?.Invoke();
            }
            else if (HasRoomToStand())
            {
                isCrouching = false;
                targetHeight = settings.standHeight;
                OnCrouchEnded?.Invoke();
            }
        }

        public void ForceMoveTowards(Vector3 target, float speed, Action onArrived = null)
        {
            forceMoveTarget = target;
            forceMoveSpeed = speed > 0f ? speed : settings.moveSpeed;
            forceMoveCallback = onArrived;
            isForceMoving = true;
        }

        public void StopForceMove()
        {
            isForceMoving = false;
            forceMoveTarget = null;
            forceMoveCallback = null;
        }

        public bool HasRoomToStand()
        {
            Vector3 origin = driver.Transform.position;
            float checkDist = settings.standHeight - driver.ColliderRadius * 0.5f;
            return !Physics.SphereCast(origin, driver.ColliderRadius * 0.9f,
                Vector3.up, out _, checkDist);
        }

        public FPCState GetState()
        {
            return new FPCState
            {
                GroundState = GroundState,
                Posture = currentPosture,
                IsMoving = IsMoving,
                IsRunning = IsMoving && !isCrouching
                            && moveVelocity.magnitude > settings.walkSpeed * 0.9f,
                InForceLook = false,
                InForceMove = isForceMoving
            };
        }

        /// <summary>0 = standing, 1 = fully crouched.</summary>
        public float GetCrouchNormalized()
        {
            float range = settings.standHeight - settings.crouchHeight;
            if (range <= 0.001f) return 0f;
            return (settings.standHeight - currentHeight) / range;
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            driver.Teleport(position, rotation);
            moveVelocity = Vector3.zero;
            verticalVelocity = 0f;
        }
    }
}