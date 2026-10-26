using UnityEngine;

namespace MHZE.PickupSystem
{
    /// <summary>
    /// Smooths a first-person arm rig's rotation behind its camera so held items follow through softly.
    /// The drag stiffens as it approaches a maximum lag angle: a fast camera turn eases into the limit
    /// instead of leaving the arm outside the view or snapping against a hard stop.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class PlayerArmMotionLag : MonoBehaviour
    {
        [SerializeField] private Transform cameraTransform;

        [Tooltip("How quickly the arm catches up to the camera, in seconds. Larger values give more visible follow-through.")]
        [SerializeField, Min(0.01f)] private float rotationResponse = 0.12f;

        [Tooltip("How far the arm may trail the camera at most, in degrees. The drag stiffens as it approaches this angle.")]
        [SerializeField, Min(1f)] private float maxLagAngle = 24f;

        [Tooltip("How strongly the drag stiffens near the limit. Higher values catch up sooner and keep the arm in view during fast turns.")]
        [SerializeField, Min(0f)] private float limitStiffness = 3f;

        private Quaternion cameraToArmRotation;
        private Quaternion smoothedCameraRotation;
        private bool initialized;

        /// <summary>Largest angle, in degrees, that the arm may trail the camera.</summary>
        public float MaxLagAngle
        {
            get => maxLagAngle;
            set => maxLagAngle = Mathf.Max(1f, value);
        }

        /// <summary>How sharply the arm stiffens as it reaches <see cref="MaxLagAngle"/>.</summary>
        public float LimitStiffness
        {
            get => limitStiffness;
            set => limitStiffness = Mathf.Max(0f, value);
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
            float lagAngle = LagAngle(cameraRotation);
            float tension = Mathf.Clamp01(lagAngle / limit);

            // In the last stretch before the limit the arm catches up faster, so a fast turn feels
            // stiff rather than loose. Ordinary turns keep the full follow-through.
            float nearLimit = Mathf.InverseLerp(0.55f, 1f, tension);
            float response = rotationResponse / (1f + limitStiffness * nearLimit * nearLimit);
            float blend = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, response));
            smoothedCameraRotation = Quaternion.Slerp(smoothedCameraRotation, cameraRotation, blend);

            // Bend the remaining lag through a soft saturating curve. It barely changes small lags
            // and approaches the limit without ever crossing it, so the drag never hits a hard stop.
            lagAngle = LagAngle(cameraRotation);
            if (lagAngle > 0.0001f)
            {
                Quaternion lag = Quaternion.Inverse(cameraRotation) * smoothedCameraRotation;
                float limited = SoftLimit(lagAngle, limit);
                if (limited < lagAngle)
                    smoothedCameraRotation = cameraRotation * Quaternion.SlerpUnclamped(Quaternion.identity, lag, limited / lagAngle);
            }

            transform.rotation = smoothedCameraRotation * cameraToArmRotation;
        }

        private float LagAngle(Quaternion cameraRotation)
        {
            return Quaternion.Angle(Quaternion.identity, Quaternion.Inverse(cameraRotation) * smoothedCameraRotation);
        }

        /// <summary>
        /// <paramref name="angle"/> compressed smoothly toward <paramref name="limit"/>. The curve is
        /// close to the identity for small angles and never reaches the limit, so the arm keeps a
        /// little give at full drag instead of stopping at a wall.
        /// </summary>
        private static float SoftLimit(float angle, float limit)
        {
            float ratio = angle / limit;
            float ratio2 = ratio * ratio;
            return angle / Mathf.Sqrt(Mathf.Sqrt(1f + ratio2 * ratio2));
        }

        private void Initialize()
        {
            if (cameraTransform == null)
                return;

            cameraToArmRotation = Quaternion.Inverse(cameraTransform.rotation) * transform.rotation;
            smoothedCameraRotation = cameraTransform.rotation;
            initialized = true;
        }
    }
}
