using UnityEngine;

namespace MHZE.GearSystem.Physics
{
    [RequireComponent(typeof(PhysicsGear))]
    [AddComponentMenu("Mechanical/Physics Gear Motor")]
    public class GearMotor : MonoBehaviour
    {
        [SerializeField, Range(-500f, 500f)]
        private float m_SpeedRPM = 60f;

        [SerializeField]
        private bool m_MaxTorqueEnabled = true;

        [SerializeField]
        private float m_MaxTorque = 50f;

        [SerializeField]
        private AnimationCurve m_SpeedCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        [Header("Debug")]
        [SerializeField]
        private bool m_DrawGizmos = true;

        private PhysicsGear m_Gear;
        private Rigidbody m_Rigidbody;
        private float m_CurrentRPM;
        private float m_Elapsed;

        public float speedRPM
        {
            get => m_SpeedRPM;
            set => m_SpeedRPM = value;
        }

        public float currentRPM => m_CurrentRPM;

        public AnimationCurve speedCurve
        {
            get => m_SpeedCurve;
            set => m_SpeedCurve = value;
        }

        private void Awake()
        {
            m_Gear = GetComponent<PhysicsGear>();
            m_Rigidbody = m_Gear.attachedRigidbody;
        }

        private void FixedUpdate()
        {
            if (m_Gear == null || m_Rigidbody == null) return;

            m_Elapsed += Time.fixedDeltaTime;

            Vector3 axis = m_Gear.GetWorldAxis();
            float targetRPM = m_SpeedRPM * m_SpeedCurve.Evaluate(m_Elapsed);
            float targetRadPerSec = targetRPM * Mathf.Deg2Rad * 6f;

            Vector3 currentAngVel = m_Rigidbody.angularVelocity;
            float currentSpeed = Vector3.Dot(currentAngVel, axis);

            float speedError = targetRadPerSec - currentSpeed;

            if (m_MaxTorqueEnabled)
            {
                float torqueMagnitude = Mathf.Clamp(speedError * m_Rigidbody.mass, -m_MaxTorque, m_MaxTorque);
                m_Rigidbody.AddTorque(axis * torqueMagnitude, ForceMode.Force);
            }
            else
            {
                m_Rigidbody.AddTorque(axis * speedError, ForceMode.VelocityChange);
            }

            m_CurrentRPM = currentSpeed * 60f / (2f * Mathf.PI);
        }

        public void SetSpeedImmediate(float rpm)
        {
            if (m_Rigidbody == null) return;

            Vector3 axis = m_Gear.GetWorldAxis();
            float radPerSec = rpm * Mathf.Deg2Rad * 6f;
            Vector3 currentAngVel = m_Rigidbody.angularVelocity;
            Vector3 perpendicular = currentAngVel - Vector3.Dot(currentAngVel, axis) * axis;
            m_Rigidbody.angularVelocity = axis * radPerSec + perpendicular;
            m_CurrentRPM = rpm;
        }

        private void OnDrawGizmos()
        {
            if (!m_DrawGizmos) return;
            if (m_Gear == null || m_Rigidbody == null) return;

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
            Vector3 center = m_Rigidbody.worldCenterOfMass;
            Vector3 axis = m_Gear.GetWorldAxis();

            float arrowLength = m_Gear.radius * 0.6f;
            float angularPos = m_CurrentRPM * Mathf.Deg2Rad * Time.time;

            Vector3 b1;
            if (Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.99f)
                b1 = Vector3.right;
            else
                b1 = Vector3.Cross(axis, Vector3.up).normalized;

            Vector3 arrowDir = Quaternion.AngleAxis(angularPos, axis) * b1;
            Vector3 arrowEnd = center + arrowDir * arrowLength;
            Gizmos.DrawLine(center, arrowEnd);
            Gizmos.DrawWireSphere(arrowEnd, 0.05f);

            float speed = Mathf.Abs(m_CurrentRPM);
            float normalizedArc = Mathf.Repeat(Time.time * speed * 0.05f, 360f);
            Vector3 b2 = Vector3.Cross(axis, b1).normalized;

            Vector3 prev = center + b1 * (m_Gear.radius * 0.3f);
            for (int i = 1; i <= 16; i++)
            {
                float angle = normalizedArc * i / 16f;
                Vector3 dir = b1 * Mathf.Cos(angle * Mathf.Deg2Rad) + b2 * Mathf.Sin(angle * Mathf.Deg2Rad);
                Vector3 curr = center + dir * (m_Gear.radius * 0.3f);
                Gizmos.DrawLine(prev, curr);
                prev = curr;
            }
        }
    }
}
