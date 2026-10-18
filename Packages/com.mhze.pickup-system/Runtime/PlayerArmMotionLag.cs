using UnityEngine;

namespace MHZE.PickupSystem
{
    /// <summary>Smooths a first-person arm rig's rotation behind its camera to give held items a softer follow-through.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class PlayerArmMotionLag : MonoBehaviour
    {
        [SerializeField] private Transform cameraTransform;
        [SerializeField, Min(0.01f)] private float rotationResponse = 0.12f;

        private Quaternion cameraToArmRotation;
        private Quaternion smoothedCameraRotation;
        private bool initialized;

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

            float blend = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, rotationResponse));
            smoothedCameraRotation = Quaternion.Slerp(smoothedCameraRotation, cameraTransform.rotation, blend);
            transform.rotation = smoothedCameraRotation * cameraToArmRotation;
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
