using UnityEngine;

namespace MHZE.AirParticlePhysics
{
    public class AirParticleEmitter : MonoBehaviour
    {
        public bool debugDraw;

        public ParticleSystem targetParticleSystem;

        public float projectionDistance = 2f;

        public float maxSpeed = 10f;

        public float maxEmissionRate = 50f;

        public Vector3 VelocityDirection { get; private set; }

        public float Speed { get; private set; }

        public Vector3 ProjectionPosition { get; private set; }

        private Vector3 _lastPosition;

        private ParticleSystem.EmissionModule _emissionModule;
        private bool _emissionCached;

        private void Awake()
        {
            _lastPosition = transform.position;
        }

        private void Update()
        {
            Vector3 velocity = (transform.position - _lastPosition) / Time.deltaTime;
            _lastPosition = transform.position;

            Speed = velocity.magnitude;

            if (velocity.sqrMagnitude > 0.0001f)
                VelocityDirection = velocity.normalized;

            ProjectionPosition = transform.position + VelocityDirection * projectionDistance;

            if (targetParticleSystem != null && VelocityDirection.sqrMagnitude > 0.001f)
            {
                targetParticleSystem.transform.SetPositionAndRotation(
                    ProjectionPosition,
                    Quaternion.LookRotation(VelocityDirection));

                if (!_emissionCached)
                {
                    _emissionModule = targetParticleSystem.emission;
                    _emissionCached = true;
                }

                float t = maxSpeed > 0.001f ? Mathf.Clamp01(Speed / maxSpeed) : 0f;
                _emissionModule.rateOverTime = Mathf.Lerp(0f, maxEmissionRate, t);
            }
        }
    }
}
