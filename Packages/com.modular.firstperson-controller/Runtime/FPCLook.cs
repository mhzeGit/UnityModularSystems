// Handles all camera rotation logic. Accumulates mouse input into target yaw/pitch, applies sensitivity and vertical clamping, then smooths toward the target using exponential smoothing. Supports force-look that rotates the camera toward a world point, optionally for a duration then returns. Also provides methods for adding relative yaw/pitch, syncing from the transform, and calculating crouch camera offset.

using UnityEngine;

namespace ModularSystems.FirstPersonController
{
    public class FPCLook
    {
        private readonly Transform playerTransform;  // root (yaw rotation only)
        private readonly Transform cameraTransform;  // camera / pivot (pitch rotation)
        private readonly FPCSettings settings;

        // Accumulated target rotation (where the player wants to look)
        private float targetYaw;
        private float targetPitch;

        // Current smoothed rotation (what's actually displayed)
        private float currentYaw;
        private float currentPitch;

        // Force look state
        private bool isForced;
        private float targetForceYaw;
        private float targetForcePitch;
        private float preForceYaw;
        private float preForcePitch;
        private float forceLookTimer;
        private float? forceLookDuration;
        private bool returningFromForce;
        private float returnTimer;

        public bool IsForced => isForced;
        public bool IsReturning => returningFromForce;
        public float Yaw => currentYaw;
        public float Pitch => currentPitch;

        public FPCLook(Transform playerRoot, Transform camera, FPCSettings settings)
        {
            this.playerTransform = playerRoot;
            this.cameraTransform = camera;
            this.settings = settings;

            // Initialize from current transform
            Vector3 camAngles = camera.localEulerAngles;
            currentPitch = camAngles.x > 180f ? camAngles.x - 360f : camAngles.x;
            currentYaw = playerRoot.eulerAngles.y;
            targetYaw = currentYaw;
            targetPitch = currentPitch;
        }

        public void Update(FPCInput input, float deltaTime)
        {
            if (isForced)
            {
                UpdateForcedLook(deltaTime);
                return;
            }

            if (returningFromForce)
            {
                UpdateReturnFromForce(deltaTime);
                return;
            }

            // --- Normal look: accumulate target ------------------
            Vector2 raw = input.LookInput;

            float mouseX = raw.x * settings.sensitivity.x * settings.inputMultiplier;
            float mouseY = raw.y * settings.sensitivity.y * settings.inputMultiplier;

            if (settings.invertY) mouseY = -mouseY;

            targetYaw += mouseX;
            targetPitch -= mouseY;
            targetPitch = Mathf.Clamp(targetPitch, settings.verticalRange.x, settings.verticalRange.y);

            // --- Smooth toward target ----------------------------
            // Exponential smoothing: fast when far, slow when close
            float smoothFactor = settings.smoothTime > 0.001f
                ? 1f - Mathf.Exp(-(1f / settings.smoothTime) * deltaTime)
                : 1f;

            currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, smoothFactor);
            currentPitch = Mathf.Lerp(currentPitch, targetPitch, smoothFactor);

            ApplyRotation();
        }

        // --- Forced look -----------------------------------------

        private void UpdateForcedLook(float deltaTime)
        {
            currentYaw = Mathf.MoveTowardsAngle(currentYaw, targetForceYaw,
                settings.lookAtSpeed * deltaTime);
            currentPitch = Mathf.MoveTowards(currentPitch, targetForcePitch,
                settings.lookAtSpeed * deltaTime);
            currentPitch = Mathf.Clamp(currentPitch, settings.verticalRange.x, settings.verticalRange.y);

            // Keep target in sync so when forced look ends, the transition is smooth
            targetYaw = currentYaw;
            targetPitch = currentPitch;

            ApplyRotation();

            float yawDiff = Mathf.DeltaAngle(currentYaw, targetForceYaw);
            float pitchDiff = targetForcePitch - currentPitch;
            bool arrived = Mathf.Abs(yawDiff) <= settings.tolerance
                        && Mathf.Abs(pitchDiff) <= settings.tolerance;

            if (arrived)
            {
                currentYaw = targetForceYaw;
                currentPitch = targetForcePitch;
                targetYaw = currentYaw;
                targetPitch = currentPitch;
                ApplyRotation();

                if (forceLookDuration.HasValue)
                {
                    forceLookTimer += deltaTime;
                    if (forceLookTimer >= forceLookDuration.Value)
                        StartReturnFromForce();
                }
            }
        }

        private void UpdateReturnFromForce(float deltaTime)
        {
            returnTimer -= deltaTime;
            float t = Mathf.Clamp01(1f - (returnTimer / 0.3f));

            currentYaw = Mathf.LerpAngle(currentYaw, preForceYaw, t);
            currentPitch = Mathf.Lerp(currentPitch, preForcePitch, t);

            targetYaw = currentYaw;
            targetPitch = currentPitch;

            ApplyRotation();

            if (t >= 1f || returnTimer <= 0f)
            {
                returningFromForce = false;
                // Reset to the natural position
                currentYaw = targetYaw;
                currentPitch = targetPitch;
            }
        }

        // --- Public API ------------------------------------------

        public void ForceLookAt(Vector3 worldPoint, float? duration = null, float? speed = null)
        {
            Vector3 dirToTarget = worldPoint - playerTransform.position;
            if (dirToTarget.sqrMagnitude < 0.001f) return;

            dirToTarget.Normalize();

            preForceYaw = currentYaw;
            preForcePitch = currentPitch;

            targetForceYaw = Mathf.Atan2(dirToTarget.x, dirToTarget.z) * Mathf.Rad2Deg;
            targetForcePitch = -Mathf.Asin(Mathf.Clamp(dirToTarget.y, -1f, 1f)) * Mathf.Rad2Deg;

            isForced = true;
            returningFromForce = false;
            forceLookTimer = 0f;
            forceLookDuration = duration;
        }

        public void ForceLookAt(Transform target, float? duration = null, float? speed = null)
        {
            if (target == null) return;
            ForceLookAt(target.position, duration, speed);
        }

        public void StopForceLook(bool snapBack = false)
        {
            if (!isForced && !returningFromForce) return;

            if (snapBack)
            {
                currentYaw = preForceYaw;
                currentPitch = preForcePitch;
                targetYaw = currentYaw;
                targetPitch = currentPitch;
                isForced = false;
                returningFromForce = false;
                ApplyRotation();
            }
            else if (isForced)
            {
                StartReturnFromForce();
            }
            else
            {
                returningFromForce = false;
            }
        }

        public void AddYaw(float degrees)
        {
            targetYaw += degrees;
            currentYaw = targetYaw;
            ApplyRotation();
        }

        public void AddPitch(float degrees)
        {
            targetPitch -= degrees;
            targetPitch = Mathf.Clamp(targetPitch, settings.verticalRange.x, settings.verticalRange.y);
            currentPitch = targetPitch;
            ApplyRotation();
        }

        public void SyncWithTransform()
        {
            Vector3 camAngles = cameraTransform.localEulerAngles;
            currentPitch = camAngles.x > 180f ? camAngles.x - 360f : camAngles.x;
            currentYaw = playerTransform.eulerAngles.y;
            targetYaw = currentYaw;
            targetPitch = currentPitch;
        }

        /// <summary>
        /// Camera Y offset based on crouch progress (0=standing, 1=full crouch).
        /// </summary>
        public float GetCrouchCameraOffset(float crouchNormalized, float standHeight, float crouchHeight)
        {
            return -(crouchNormalized * (standHeight - crouchHeight));
        }

        // --- Internal --------------------------------------------

        private void ApplyRotation()
        {
            playerTransform.rotation = Quaternion.Euler(0f, currentYaw, 0f);
            cameraTransform.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
        }

        private void StartReturnFromForce()
        {
            if (!isForced) return;

            preForceYaw = currentYaw;
            preForcePitch = currentPitch;
            isForced = false;
            returningFromForce = true;
            returnTimer = 0.3f;
        }
    }
}