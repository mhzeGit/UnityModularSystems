// Applies procedural camera effects on jump and landing. Jump tilts the camera pitch down via an animation curve. Landing applies both a pitch bump and a vertical position dip based on separate curves. Effects are additive to the camera's local transform and can be snapped off instantly.

using UnityEngine;

namespace MHZE.FirstPersonController
{
    public class FPCCameraEffects
    {
        private readonly Transform cameraTransform;
        private readonly FPCSettings settings;

        private float jumpTimer;
        private float landingTimer;
        private float landingMultiplier = 1f;
        private Vector3 lastAppliedPositionOffset;
        private Quaternion lastAppliedRotationOffset = Quaternion.identity;

        public FPCCameraEffects(Transform cameraTransform, FPCSettings settings)
        {
            this.cameraTransform = cameraTransform;
            this.settings = settings;
        }

        public void TriggerJump()
        {
            jumpTimer = settings.jumpEffectDuration;
        }

        public void TriggerLanding(float verticalVelocity)
        {
            landingTimer = settings.landingEffectDuration;
            landingMultiplier = Mathf.Clamp(
                Mathf.Abs(verticalVelocity) * settings.landingVelocityMultiplier,
                0f,
                settings.landingVelocityMaxMultiplier);
        }

        public void Apply(float deltaTime)
        {
            if (!settings.enableJumpLandEffects) return;

            cameraTransform.localPosition -= lastAppliedPositionOffset;
            cameraTransform.localRotation *= Quaternion.Inverse(lastAppliedRotationOffset);

            Vector3 posOffset = Vector3.zero;
            Vector3 rotOffset = Vector3.zero;

            if (jumpTimer > 0f)
            {
                float t = Mathf.Clamp01(1f - (jumpTimer / settings.jumpEffectDuration));
                rotOffset.x = settings.jumpPitchCurve.Evaluate(t);
                jumpTimer -= deltaTime;
                if (jumpTimer <= 0f)
                    jumpTimer = 0f;
            }

            if (landingTimer > 0f)
            {
                float t = Mathf.Clamp01(1f - (landingTimer / settings.landingEffectDuration));
                rotOffset.x += settings.landingPitchCurve.Evaluate(t) * landingMultiplier;
                posOffset.y = settings.landingPositionCurve.Evaluate(t) * landingMultiplier;
                landingTimer -= deltaTime;
                if (landingTimer <= 0f)
                    landingTimer = 0f;
            }

            cameraTransform.localPosition += posOffset;
            cameraTransform.localRotation *= Quaternion.Euler(rotOffset);

            lastAppliedPositionOffset = posOffset;
            lastAppliedRotationOffset = Quaternion.Euler(rotOffset);
        }

        public void Snap()
        {
            cameraTransform.localPosition -= lastAppliedPositionOffset;
            cameraTransform.localRotation *= Quaternion.Inverse(lastAppliedRotationOffset);
            lastAppliedPositionOffset = Vector3.zero;
            lastAppliedRotationOffset = Quaternion.identity;
            jumpTimer = 0f;
            landingTimer = 0f;
        }
    }
}
