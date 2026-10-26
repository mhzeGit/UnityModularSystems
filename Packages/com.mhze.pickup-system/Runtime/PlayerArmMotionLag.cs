using UnityEngine;

namespace MHZE.PickupSystem
{
    /// <summary>
    /// Smooths a first-person arm rig's rotation behind its camera so held items follow through softly.
    /// The drag gets progressively stiffer as the lag grows but never clamps to a fixed angle, so a fast
    /// camera turn still leaves the arm a little give instead of stopping it dead at the limit.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class PlayerArmMotionLag : MonoBehaviour
    {
        [SerializeField] private Transform cameraTransform;

        [Tooltip("How quickly the arm catches up to the camera, in seconds. Larger values give more visible follow-through.")]
        [SerializeField, Min(0.01f)] private float rotationResponse = 0.12f;

        [Tooltip("How gently the arm settles back after a turn, in seconds. Larger values make the release softer.")]
        [SerializeField, Min(0.01f)] private float settleResponse = 0.3f;

        [Tooltip("The lag angle at which the drag starts to stiffen hard, in degrees. The arm can pass it slightly; it is a soft knee, not a cap.")]
        [SerializeField, Min(1f)] private float maxLagAngle = 24f;

        [Tooltip("How strongly the drag stiffens at the knee. Higher values keep the arm closer to the knee during fast turns.")]
        [SerializeField, Min(0f)] private float limitStiffness = 5f;

        private Quaternion cameraToArmRotation;
        private Quaternion smoothedCameraRotation;
        private Quaternion previousCameraRotation;
        private float previousLagAngle;
        private bool initialized;

        /// <summary>The lag angle, in degrees, where the drag starts to stiffen hard.</summary>
        public float MaxLagAngle
        {
            get => maxLagAngle;
            set => maxLagAngle = Mathf.Max(1f, value);
        }

        /// <summary>How strongly the drag stiffens at <see cref="MaxLagAngle"/>.</summary>
        public float LimitStiffness
        {
            get => limitStiffness;
            set => limitStiffness = Mathf.Max(0f, value);
        }

        /// <summary>How slowly the arm settles back to rest when the camera stops turning, in seconds.</summary>
        public float SettleResponse
        {
            get => settleResponse;
            set => settleResponse = Mathf.Max(0.01f, value);
        }

        private void OnEnable()
        {
            if (cameraTransform == null && transform.parent != null)
                cameraTransform = transform.parent;

            Initialize();
        }

        private void LateUpdate()
        {
            if (cameraTransform == null)
                return;

            if (!initialized)
                Initialize();

            Quaternion cameraRotation = cameraTransform.rotation;
            float limit = Mathf.Max(1f, maxLagAngle);
            float lagAngle = Quaternion.Angle(previousCameraRotation, smoothedCameraRotation);
            float cameraDelta = Quaternion.Angle(previousCameraRotation, cameraRotation);

            // The return gets its own softer timing; the stiffening is only for the follow that
            // caused the lag, otherwise the arm would hurry back exactly when it should drift.
            bool releasing = cameraDelta < 0.02f || lagAngle < previousLagAngle - 0.001f;
            float response = releasing
                ? Mathf.Max(settleResponse, rotationResponse)
                : rotationResponse;

            // Split the frame so the arm follows the camera in small steps. That keeps the response
            // independent of frame rate, which matters because the stiffness is progressive.
            float dt = Mathf.Max(0f, Time.deltaTime);
            if (dt > 0f)
            {
                int steps = Mathf.Clamp(Mathf.CeilToInt(dt / 0.0025f), 1, 16);
                float h = dt / steps;
                for (int i = 1; i <= steps; i++)
                {
                    Quaternion target = Quaternion.Slerp(previousCameraRotation, cameraRotation, (float)i / steps);
                    float stepLag = Quaternion.Angle(target, smoothedCameraRotation);
                    float tension = Mathf.Clamp01(stepLag / limit);
                    float stiffened = response / (1f + limitStiffness * Mathf.Pow(tension, 4f));
                    float blend = 1f - Mathf.Exp(-h / Mathf.Max(0.005f, stiffened));
                    smoothedCameraRotation = Quaternion.Slerp(smoothedCameraRotation, target, blend);
                }
            }

            transform.rotation = smoothedCameraRotation * cameraToArmRotation;
            previousCameraRotation = cameraRotation;
            previousLagAngle = Quaternion.Angle(cameraRotation, smoothedCameraRotation);
        }

        private void Initialize()
        {
            if (cameraTransform == null)
                return;

            Quaternion cameraRotation = cameraTransform.rotation;
            cameraToArmRotation = Quaternion.Inverse(cameraRotation) * transform.rotation;
            smoothedCameraRotation = cameraRotation;
            previousCameraRotation = cameraRotation;
            previousLagAngle = 0f;
            initialized = true;
        }
    }
}
