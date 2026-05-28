using UnityEngine;

namespace MHZE.FirstPersonController
{
    public class FPCHeadbob
    {
        private readonly Transform cameraTransform;
        private readonly FPCHeadbobSettings settings;
        private readonly Vector3 defaultLocalPosition;
        private readonly Quaternion defaultLocalRotation;

        private float positionBobTimer;
        private float rotationBobTimer;
        private float bobIntensity;
        private Vector3 currentPositionOffset;
        private Vector3 currentRotationOffset;

        public FPCHeadbob(Transform cameraTransform, FPCHeadbobSettings settings)
        {
            this.cameraTransform = cameraTransform;
            this.settings = settings;
            this.defaultLocalPosition = cameraTransform.localPosition;
            this.defaultLocalRotation = cameraTransform.localRotation;
        }

        public void Update(float speed, bool isGrounded, bool isMoving, bool isCrouching, bool isSprinting, float deltaTime)
        {
            if (settings == null || !settings.enabled)
            {
                if (HasOffset())
                    Reset();
                return;
            }

            FPCHeadbobPreset preset = SelectPreset(isMoving, isGrounded, isCrouching, isSprinting);
            if (preset == null)
            {
                if (HasOffset())
                    Reset();
                return;
            }

            bool isIdle = isGrounded && !isMoving && !isCrouching;

            float targetIntensity;
            if (isIdle)
            {
                targetIntensity = 1f;
            }
            else
            {
                bool shouldBob = isGrounded && isMoving && speed >= settings.minSpeed;
                targetIntensity = shouldBob ? Mathf.Clamp01(speed / 4.5f) : 0f;
            }

            bobIntensity = Mathf.Lerp(bobIntensity, targetIntensity,
                settings.smoothing * deltaTime);

            if (bobIntensity > 0.005f)
            {
                float dtAdvance = isIdle ? deltaTime : speed * deltaTime;

                positionBobTimer += dtAdvance;
                rotationBobTimer += dtAdvance;

                float posX = Mathf.Cos(positionBobTimer * preset.positionFrequency) * preset.positionAmplitude.x;
                float posY = Mathf.Sin(positionBobTimer * preset.positionFrequency * 2f) * preset.positionAmplitude.y;
                currentPositionOffset = new Vector3(posX, posY, 0f) * bobIntensity;

                float rotPitch = Mathf.Sin(rotationBobTimer * preset.rotationFrequency * 2f) * preset.rotationAmplitude.x;
                float rotRoll = Mathf.Cos(rotationBobTimer * preset.rotationFrequency) * preset.rotationAmplitude.y;
                currentRotationOffset = new Vector3(rotPitch, 0f, rotRoll) * bobIntensity;
            }
            else
            {
                positionBobTimer = 0f;
                rotationBobTimer = 0f;
                currentPositionOffset = Vector3.Lerp(currentPositionOffset, Vector3.zero,
                    settings.smoothing * deltaTime);
                currentRotationOffset = Vector3.Lerp(currentRotationOffset, Vector3.zero,
                    settings.smoothing * deltaTime);

                if (currentPositionOffset.sqrMagnitude < 0.0001f)
                    currentPositionOffset = Vector3.zero;
                if (currentRotationOffset.sqrMagnitude < 0.0001f)
                    currentRotationOffset = Vector3.zero;
            }

            cameraTransform.localPosition = defaultLocalPosition + currentPositionOffset;
            cameraTransform.localRotation = defaultLocalRotation * Quaternion.Euler(currentRotationOffset);
        }

        public void Reset()
        {
            positionBobTimer = 0f;
            rotationBobTimer = 0f;
            bobIntensity = 0f;
            currentPositionOffset = Vector3.zero;
            currentRotationOffset = Vector3.zero;
            cameraTransform.localPosition = defaultLocalPosition;
            cameraTransform.localRotation = defaultLocalRotation;
        }

        private bool HasOffset()
        {
            return currentPositionOffset.sqrMagnitude > 0f
                || currentRotationOffset.sqrMagnitude > 0f;
        }

        private FPCHeadbobPreset SelectPreset(bool isMoving, bool isGrounded, bool isCrouching, bool isSprinting)
        {
            if (!isGrounded)
                return settings.airborne;
            if (isCrouching)
                return settings.crouching;
            if (!isMoving)
                return settings.idle;
            if (isSprinting)
                return settings.running;
            return settings.walking;
        }
    }
}
