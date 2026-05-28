using UnityEngine;

namespace MHZE.FirstPersonController
{
    public class FPCHeadbob
    {
        private readonly Transform cameraTransform;
        private readonly FPCHeadbobSettings settings;
        private readonly Vector3 defaultLocalPosition;
        private readonly Quaternion defaultLocalRotation;

        private float positionPhase;
        private float rotationPhase;
        private float bobIntensity;
        private int currentPresetIndex;

        private float currentPositionFrequency;
        private float currentRotationFrequency;
        private Vector3 currentPositionAmplitude;
        private Vector3 currentRotationAmplitude;

        public FPCHeadbob(Transform cameraTransform, FPCHeadbobSettings settings)
        {
            this.cameraTransform = cameraTransform;
            this.settings = settings;
            this.defaultLocalPosition = cameraTransform.localPosition;
            this.defaultLocalRotation = cameraTransform.localRotation;

            if (settings != null && settings.presets.Count > 0)
            {
                ApplyPresetImmediate(settings.presets[0]);
                currentPresetIndex = 0;
            }
        }

        public void Update(float speed, float deltaTime)
        {
            if (settings == null)
                return;

            if (!settings.enabled)
            {
                if (bobIntensity > 0f)
                    SnapToDefault();
                return;
            }

            FPCHeadbobPreset targetPreset = SelectPreset(speed);
            InterpolateTowards(targetPreset, deltaTime);

            float targetIntensity = targetPreset.minSpeed > 0.001f
                ? Mathf.Clamp01(speed / targetPreset.minSpeed)
                : 1f;

            bobIntensity = Mathf.Lerp(bobIntensity, targetIntensity, settings.smoothing * deltaTime);

            if (bobIntensity > 0.005f)
            {
                positionPhase += currentPositionFrequency * deltaTime;
                rotationPhase += currentRotationFrequency * deltaTime;

                cameraTransform.localPosition = defaultLocalPosition + CalculatePositionOffset() * bobIntensity;
                cameraTransform.localRotation = defaultLocalRotation * Quaternion.Euler(CalculateRotationOffset() * bobIntensity);
            }
            else if (HasOffset())
            {
                ReturnToDefault(deltaTime);
            }
            else
            {
                positionPhase = 0f;
                rotationPhase = 0f;
            }

            if (settings.debugLogging)
            {
                Debug.Log(
                    $"[FPC Headbob] Preset[{currentPresetIndex}] Spd={speed:F2} Int={bobIntensity:F3} " +
                    $"PosAmp={currentPositionAmplitude} RotAmp={currentRotationAmplitude}");
            }
        }

        public void Snap()
        {
            SnapToDefault();
        }

        private Vector3 CalculatePositionOffset()
        {
            return new Vector3(
                Mathf.Cos(positionPhase) * currentPositionAmplitude.x,
                Mathf.Sin(positionPhase * 2f) * currentPositionAmplitude.y,
                Mathf.Sin(positionPhase) * currentPositionAmplitude.z
            );
        }

        private Vector3 CalculateRotationOffset()
        {
            return new Vector3(
                Mathf.Sin(rotationPhase * 2f) * currentRotationAmplitude.x,
                Mathf.Sin(rotationPhase) * currentRotationAmplitude.y,
                Mathf.Cos(rotationPhase) * currentRotationAmplitude.z
            );
        }

        private void SnapToDefault()
        {
            positionPhase = 0f;
            rotationPhase = 0f;
            bobIntensity = 0f;
            cameraTransform.localPosition = defaultLocalPosition;
            cameraTransform.localRotation = defaultLocalRotation;

            if (settings != null && settings.presets.Count > 0)
            {
                ApplyPresetImmediate(settings.presets[0]);
                currentPresetIndex = 0;
            }
        }

        private bool HasOffset()
        {
            return Vector3.Distance(cameraTransform.localPosition, defaultLocalPosition) > 0.0001f
                || Quaternion.Angle(cameraTransform.localRotation, defaultLocalRotation) > 0.01f;
        }

        private void ReturnToDefault(float deltaTime)
        {
            float t = Mathf.Clamp01(settings.smoothing * deltaTime);
            cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, defaultLocalPosition, t);
            cameraTransform.localRotation = Quaternion.Slerp(cameraTransform.localRotation, defaultLocalRotation, t);
        }

        private void InterpolateTowards(FPCHeadbobPreset target, float deltaTime)
        {
            float t = Mathf.Clamp01(settings.smoothing * deltaTime);
            currentPositionFrequency = Mathf.Lerp(currentPositionFrequency, target.positionFrequency, t);
            currentRotationFrequency = Mathf.Lerp(currentRotationFrequency, target.rotationFrequency, t);
            currentPositionAmplitude = Vector3.Lerp(currentPositionAmplitude, target.positionAmplitude, t);
            currentRotationAmplitude = Vector3.Lerp(currentRotationAmplitude, target.rotationAmplitude, t);
        }

        private void ApplyPresetImmediate(FPCHeadbobPreset preset)
        {
            currentPositionFrequency = preset.positionFrequency;
            currentRotationFrequency = preset.rotationFrequency;
            currentPositionAmplitude = preset.positionAmplitude;
            currentRotationAmplitude = preset.rotationAmplitude;
        }

        private FPCHeadbobPreset SelectPreset(float speed)
        {
            FPCHeadbobPreset best = settings.presets[0];
            currentPresetIndex = 0;

            if (speed >= settings.minSpeed)
            {
                for (int i = 0; i < settings.presets.Count; i++)
                {
                    if (speed >= settings.presets[i].minSpeed)
                    {
                        best = settings.presets[i];
                        currentPresetIndex = i;
                    }
                }
            }

            return best;
        }
    }
}
