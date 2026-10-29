// Handles all camera rotation logic. Accumulates mouse input into target yaw/pitch, applies sensitivity and vertical clamping, then smooths toward the target using exponential smoothing. Supports force-look that rotates the camera toward a world point, optionally for a duration then returns. Also provides methods for adding relative yaw/pitch, syncing from the transform, and calculating crouch camera offset.

using UnityEngine;

namespace MHZE.FirstPersonController
{
    public class FPCLook
    {
        private readonly IFPCMovementDriver driver;     // root (yaw rotation only)
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

        // Runtime look overrides (options menu). Default to the settings asset values.
        private float sensitivityMultiplier = 1f;
        private bool? invertYOverride;

        private object softLockOwner;
        private Vector2 softLockCenter, softLockLimits;
        private float softLockSmoothTime;

        public bool IsForced => isForced;
        public bool IsReturning => returningFromForce;
        public float Yaw => currentYaw;
        public float Pitch => currentPitch;
        public bool IsSoftLookLocked => softLockOwner != null;

        public FPCLook(IFPCMovementDriver driver, Transform camera, FPCSettings settings)
        {
            this.driver = driver;
            this.cameraTransform = camera;
            this.settings = settings;

            // Initialize from current transform
            Vector3 camAngles = camera.localEulerAngles;
            currentPitch = camAngles.x > 180f ? camAngles.x - 360f : camAngles.x;
            currentYaw = driver.Transform.eulerAngles.y;
            targetYaw = currentYaw;
            targetPitch = currentPitch;
        }

        public void Update(FPCInput input, float deltaTime)
        {
            if (IsSoftLookLocked && deltaTime <= 0f) return;
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

            float mouseX = raw.x * settings.sensitivity.x * sensitivityMultiplier * settings.inputMultiplier;
            float mouseY = raw.y * settings.sensitivity.y * sensitivityMultiplier * settings.inputMultiplier;

            if (invertYOverride ?? settings.invertY) mouseY = -mouseY;

            if (IsSoftLookLocked)
            {
                targetYaw = softLockCenter.x + SoftLimitDelta(Mathf.DeltaAngle(softLockCenter.x, targetYaw), mouseX, softLockLimits.x);
                targetPitch = softLockCenter.y + SoftLimitDelta(targetPitch - softLockCenter.y, -mouseY, softLockLimits.y);
            }
            else
            {
                targetYaw += mouseX;
                targetPitch -= mouseY;
            }
            targetPitch = Mathf.Clamp(targetPitch, settings.verticalRange.x, settings.verticalRange.y);

            // --- Smooth toward target ----------------------------
            // Exponential smoothing: fast when far, slow when close
            float smoothTime = IsSoftLookLocked ? Mathf.Max(settings.smoothTime, softLockSmoothTime) : settings.smoothTime;
            float smoothFactor = smoothTime > 0.001f
                ? 1f - Mathf.Exp(-(1f / smoothTime) * deltaTime)
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

        /// <summary>Limit input around the current view, with increasing resistance near each angular limit.</summary>
        public bool TryBeginSoftLookLock(object owner, Vector2 angleLimits, float smoothTime = .12f)
        {
            if (owner == null) throw new System.ArgumentNullException(nameof(owner));
            if (!(angleLimits.x >= 0 && angleLimits.x < 180 && angleLimits.y >= 0 && angleLimits.y < 180))
                throw new System.ArgumentOutOfRangeException(nameof(angleLimits));
            if (!(smoothTime >= 0 && smoothTime < float.PositiveInfinity))
                throw new System.ArgumentOutOfRangeException(nameof(smoothTime));
            if (IsSoftLookLocked) return ReferenceEquals(softLockOwner, owner);
            if (isForced || returningFromForce) return false;
            softLockOwner = owner;
            softLockCenter = new Vector2(currentYaw, currentPitch);
            softLockLimits = angleLimits;
            softLockSmoothTime = smoothTime;
            targetYaw = currentYaw;
            targetPitch = currentPitch;
            return true;
        }

        /// <summary>Release only this owner's limit, keeping the displayed view and discarding pending input.</summary>
        public void EndSoftLookLock(object owner)
        {
            if (IsSoftLookLocked && ReferenceEquals(softLockOwner, owner)) ClearSoftLookLock();
        }

        internal void ClearSoftLookLock()
        {
            softLockOwner = null;
            targetYaw = currentYaw;
            targetPitch = currentPitch;
        }

        private static float SoftLimitDelta(float offset, float delta, float limit)
        {
            if (limit <= 0f) return 0f;
            // Turning back is immediate; discarded outward input never builds up behind the limit.
            if (offset * delta < 0f)
            {
                float inward = Mathf.Sign(delta) * Mathf.Min(Mathf.Abs(delta), Mathf.Abs(offset));
                offset += inward;
                delta -= inward;
            }
            if (delta == 0f) return offset;
            return Mathf.Sign(delta) * (limit - (limit - Mathf.Abs(offset)) * Mathf.Exp(-Mathf.Abs(delta) / limit));
        }

        /// <summary>
        /// Multiply the settings asset's look sensitivity at runtime (options menu).
        /// The settings asset itself is never modified. 1 = unchanged.
        /// </summary>
        public void SetSensitivityMultiplier(float multiplier)
        {
            sensitivityMultiplier = Mathf.Max(0.0001f, multiplier);
        }

        /// <summary>Override the settings asset's invert-Y flag at runtime (options menu).</summary>
        public void SetInvertY(bool invert)
        {
            invertYOverride = invert;
        }

        /// <summary>Drop the runtime invert-Y override and use the settings asset value.</summary>
        public void ClearInvertYOverride()
        {
            invertYOverride = null;
        }

        public void ForceLookAt(Vector3 worldPoint, float? duration = null, float? speed = null)
        {
            Vector3 dirToTarget = worldPoint - driver.Transform.position;
            if (dirToTarget.sqrMagnitude < 0.001f) return;

            ClearSoftLookLock();

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
            softLockOwner = null;
            Vector3 camAngles = cameraTransform.localEulerAngles;
            currentPitch = camAngles.x > 180f ? camAngles.x - 360f : camAngles.x;
            currentYaw = driver.Transform.eulerAngles.y;
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
            driver.SetRotation(Quaternion.Euler(0f, currentYaw, 0f));
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
