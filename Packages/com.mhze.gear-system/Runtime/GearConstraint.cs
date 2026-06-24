using UnityEngine;

namespace MHZE.GearSystem
{
    public enum GearAxis { X, Y, Z }

    [AddComponentMenu("Mechanical/Gear Constraint")]
    public class GearConstraint : MonoBehaviour
    {
        private static bool s_HasStepped;
        private Quaternion m_BaseRotation;
        [SerializeField] private GearAxis m_Axis = GearAxis.Y;
        [SerializeField] private float m_Radius = 0.5f;
        [SerializeField] private float m_ToothDensity = 5f;
        [SerializeField] private float m_ToothHeight = 0.1f;
        [SerializeField] private bool m_DebugDraw;
        [SerializeField] private bool m_IsDriver;
        [SerializeField] private float m_AngularVelocity;
        [SerializeField] private float m_CurrentAngle;
        [SerializeField] private GearConstraint[] m_Dependencies;

        public GearAxis axis
        {
            get => m_Axis;
            set => m_Axis = value;
        }

        public float radius
        {
            get => m_Radius;
            set => m_Radius = Mathf.Max(0.001f, value);
        }

        public float toothDensity
        {
            get => m_ToothDensity;
            set => m_ToothDensity = Mathf.Max(0.1f, value);
        }

        public int toothCount => Mathf.Max(3, Mathf.RoundToInt(2f * Mathf.PI * m_Radius * m_ToothDensity));

        public float toothHeight
        {
            get => m_ToothHeight;
            set => m_ToothHeight = Mathf.Max(0f, value);
        }

        public bool debugDraw
        {
            get => m_DebugDraw;
            set => m_DebugDraw = value;
        }

        public bool isDriver
        {
            get => m_IsDriver;
            set => m_IsDriver = value;
        }

        public float angularVelocity
        {
            get => m_AngularVelocity;
            set => m_AngularVelocity = value;
        }

        public float currentAngle
        {
            get => m_CurrentAngle;
            set => m_CurrentAngle = value;
        }

        public GearConstraint[] dependencies
        {
            get => m_Dependencies;
            set => m_Dependencies = value;
        }

        private void Awake()
        {
            m_BaseRotation = transform.localRotation;
        }

        private void FixedUpdate()
        {
            if (!s_HasStepped)
            {
                s_HasStepped = true;
                GearPhysicsSolver.Step(Time.fixedDeltaTime);
            }

            var axisVec = m_Axis switch
            {
                GearAxis.X => Vector3.right,
                GearAxis.Z => Vector3.forward,
                _ => Vector3.up
            };
            var gearRotation = Quaternion.AngleAxis(m_CurrentAngle * Mathf.Rad2Deg, axisVec);
            transform.localRotation = m_BaseRotation * gearRotation;
        }

        private void Update()
        {
            s_HasStepped = false;
        }

        private void OnDrawGizmos()
        {
            if (!m_DebugDraw) return;
            GearConstraintDebugger.Draw(this);
        }
    }
}
