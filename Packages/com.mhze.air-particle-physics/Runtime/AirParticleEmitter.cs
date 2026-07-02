using UnityEngine;

namespace MHZE.AirParticlePhysics
{
    public class AirParticleEmitter : MonoBehaviour
    {
        public bool debugDraw;

        public Vector3 VelocityDirection { get; private set; }

        private Vector3 _lastPosition;

        private void Awake()
        {
            _lastPosition = transform.position;
        }

        private void Update()
        {
            Vector3 velocity = transform.position - _lastPosition;
            _lastPosition = transform.position;

            if (velocity.sqrMagnitude > 0.0001f)
                VelocityDirection = velocity.normalized;
        }
    }
}
