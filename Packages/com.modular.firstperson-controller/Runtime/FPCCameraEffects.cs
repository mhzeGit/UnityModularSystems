// Applies procedural camera effects on jump and landing. Jump tilts the camera pitch down via an animation curve. Landing applies both a pitch bump and a vertical position dip based on separate curves. Effects are additive to the camera's local transform and can be snapped off instantly.

using UnityEngine;

namespace ModularSystems.FirstPersonController
{
    public class FPCCameraEffects
    {
        private readonly Transform cameraTransform;
        private readonly FPCSettings settings;

        private float jumpTimer;
        private float landingTimer;

        public FPCCameraEffects(Transform cameraTransform, FPCSettings settings)
        {
            this.cameraTransform = cameraTransform;
            this.settings = settings;
        }

        public void TriggerJump()
        {
            jumpTimer = settings.jumpEffectDuration;
        }

        public void TriggerLanding()
        {
            landingTimer = settings.landingEffectDuration;
        }

        public void Apply(float deltaTime)
        {
            if (!settings.enableJumpLandEffects) return;

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
                rotOffset.x += settings.landingPitchCurve.Evaluate(t);
                posOffset.y = settings.landingPositionCurve.Evaluate(t);
                landingTimer -= deltaTime;
                if (landingTimer <= 0f)
                    landingTimer = 0f;
            }

            if (posOffset != Vector3.zero || rotOffset != Vector3.zero)
            {
                cameraTransform.localPosition += posOffset;
                cameraTransform.localRotation *= Quaternion.Euler(rotOffset);
            }
        }

        public void Snap()
        {
            jumpTimer = 0f;
            landingTimer = 0f;
        }
    }
}
