using UnityEngine;

namespace MHZE.GearSystem
{
    public enum GearAxis { X, Y, Z }

    [AddComponentMenu("Mechanical/Gear Constraint")]
    public class GearConstraint : MonoBehaviour
    {
        [Header("Axis")]
        [SerializeField] private GearAxis m_Axis = GearAxis.Y;

        [Header("Gears")]
        [SerializeField] private Rigidbody m_GearA;
        [SerializeField] private Rigidbody m_GearB;
        [SerializeField] private float m_RadiusA = 0.5f;
        [SerializeField] private float m_RadiusB = 0.5f;

        [Header("Visual")]
        [SerializeField] private float m_ToothDensity = 5f;
        [SerializeField] private float m_ToothHeight = 0.1f;
        [SerializeField] private bool m_DebugDraw;
        [SerializeField] private bool m_IsDriver;

        [Header("Physics")]
        [SerializeField]
        [Range(0f, 100f)]
        [Tooltip("Viscous damping applied per-gear. Dissipates energy proportional to angular velocity.")]
        private float m_Damping = 0f;

        [SerializeField]
        [Range(0.001f, 1f)]
        [Tooltip("Baumgarte stabilization factor. Higher values correct positional drift faster but may cause oscillation.")]
        private float m_PositionCorrection = 0.2f;

        [SerializeField]
        [Tooltip("Maximum constraint torque (Nm). 0 = unlimited.")]
        private float m_MaxTorque = 0f;

        [SerializeField]
        [Range(0.01f, 1f)]
        [Tooltip("Transmission efficiency. 1 = perfect transfer, 0.5 = 50% efficiency. Reduces torque to the driven gear.")]
        private float m_Efficiency = 1f;

        [SerializeField]
        [Tooltip("Auto-disable collision between gear colliders.")]
        private bool m_IgnoreCollisionBetweenGears = true;

        [SerializeField]
        [Tooltip("Angular velocity below which the constraint enters sleep after cooldown frames.")]
        private float m_SleepThreshold = 0.01f;

        private float m_PositionError;
        private int m_SleepCounter;
        private bool m_Initialized;

        // --------------------------------------------------------------------------------
        // Public Properties
        // --------------------------------------------------------------------------------

        public GearAxis axis
        {
            get => m_Axis;
            set => m_Axis = value;
        }

        public float radiusA
        {
            get => m_RadiusA;
            set => m_RadiusA = Mathf.Max(0.001f, value);
        }

        public float radiusB
        {
            get => m_RadiusB;
            set => m_RadiusB = Mathf.Max(0.001f, value);
        }

        public float toothDensity
        {
            get => m_ToothDensity;
            set => m_ToothDensity = Mathf.Max(0.1f, value);
        }

        public int GetToothCount(float radius) =>
            Mathf.Max(3, Mathf.RoundToInt(2f * Mathf.PI * radius * m_ToothDensity));

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

        public Rigidbody gearA
        {
            get => m_GearA;
            set => m_GearA = value;
        }

        public Rigidbody gearB
        {
            get => m_GearB;
            set => m_GearB = value;
        }

        public float damping
        {
            get => m_Damping;
            set => m_Damping = Mathf.Max(0f, value);
        }

        public float positionCorrection
        {
            get => m_PositionCorrection;
            set => m_PositionCorrection = Mathf.Clamp01(value);
        }

        public float efficiency
        {
            get => m_Efficiency;
            set => m_Efficiency = Mathf.Clamp01(value);
        }

        public float sleepThreshold
        {
            get => m_SleepThreshold;
            set => m_SleepThreshold = Mathf.Max(0f, value);
        }

        public Vector3 GetAxisVector()
        {
            return m_Axis switch
            {
                GearAxis.X => Vector3.right,
                GearAxis.Z => Vector3.forward,
                _ => Vector3.up
            };
        }

        public float gearRatio => m_RadiusB / m_RadiusA;

        // --------------------------------------------------------------------------------
        // Unity Messages
        // --------------------------------------------------------------------------------

        private void OnEnable()
        {
            m_Initialized = false;
            m_PositionError = 0f;
            m_SleepCounter = 0;
        }

        private void Start()
        {
            // Offset Gear B by half a tooth so teeth interlock instead of overlapping.
            if (m_GearB != null)
            {
                int toothCount = GetToothCount(m_RadiusB);
                float halfToothAngle = Mathf.PI / toothCount;
                Vector3 localAxis = GetAxisVector();
                Vector3 worldAxis = m_GearB.transform.rotation * localAxis;
                m_GearB.transform.rotation = Quaternion.AngleAxis(halfToothAngle * Mathf.Rad2Deg, worldAxis) * m_GearB.transform.rotation;
            }

            if (m_IgnoreCollisionBetweenGears && m_GearA != null && m_GearB != null)
            {
                Collider[] collidersA = m_GearA.GetComponentsInChildren<Collider>();
                Collider[] collidersB = m_GearB.GetComponentsInChildren<Collider>();
                foreach (Collider ca in collidersA)
                {
                    if (ca == null) continue;
                    foreach (Collider cb in collidersB)
                    {
                        if (cb == null) continue;
                        Physics.IgnoreCollision(ca, cb, true);
                    }
                }
            }
        }

        private void FixedUpdate()
        {
            if (m_GearA == null || m_GearB == null) return;

            float dt = Time.fixedDeltaTime;
            if (dt <= 0f) return;

            if (!m_Initialized)
            {
                m_PositionError = 0f;
                m_Initialized = true;
            }

            // --- Gather per-gear state ---

            Vector3 localAxis = GetAxisVector();
            Vector3 axisA = m_GearA.transform.rotation * localAxis;
            Vector3 axisB = m_GearB.transform.rotation * localAxis;

            float omegaA = Vector3.Dot(m_GearA.angularVelocity, axisA);
            float omegaB = Vector3.Dot(m_GearB.angularVelocity, axisB);

            float rA = m_RadiusA;
            float rB = m_RadiusB;

            // Constraint velocity error (should be zero for an ideal rigid connection)
            // C(theta_A, theta_B) = r_A * theta_A + r_B * theta_B  = 0
            // dC/dt               = r_A * omega_A  + r_B * omega_B  = 0
            float velError = omegaA * rA + omegaB * rB;

            // Inverse rotational inertia about the constraint axis
            float invIA = GetInverseInertiaAboutAxis(m_GearA, axisA);
            float invIB = GetInverseInertiaAboutAxis(m_GearB, axisB);

            // Effective inverse mass of the constraint (seconds-squared per kg-m^2)
            float invMass = invIA * rA * rA + invIB * rB * rB;

            if (invMass < 1e-12f) return; // both gears are effectively massless / kinematic

            // Accumulate position-level constraint violation (integral of velocity error)
            m_PositionError += velError * dt;

            // --- Sleep heuristic ---
            float speedA = Mathf.Abs(omegaA);
            float speedB = Mathf.Abs(omegaB);

            if (m_SleepThreshold > 0f && speedA < m_SleepThreshold && speedB < m_SleepThreshold)
            {
                m_SleepCounter++;
                if (m_SleepCounter > 10)
                {
                    // Sleep: gently bring angular velocity to zero
                    m_GearA.angularVelocity = Vector3.MoveTowards(
                        m_GearA.angularVelocity, Vector3.zero, m_SleepThreshold * 0.5f);
                    m_GearB.angularVelocity = Vector3.MoveTowards(
                        m_GearB.angularVelocity, Vector3.zero, m_SleepThreshold * 0.5f);
                    return;
                }
            }
            else
            {
                m_SleepCounter = 0;
            }

            // --- Compute constraint force (Lagrange multiplier) ---
            //
            // Derivation (ForceMode.Force):
            //   tau_A = r_A * lambda       (torque on A from contact force lambda)
            //   delta_omega_A = tau_A * dt / I_A = r_A * lambda * dt / I_A
            //
            // Velocity error after applying constraint:
            //   e_new = e + r_A * delta_omega_A + r_B * delta_omega_B
            //         = e + lambda * dt * invMass
            //
            // Target: e_new = -beta * posError / dt   (Baumgarte stabilization)
            //
            //   lambda = -(e + beta * posError / dt) / (dt * invMass)

            float beta = m_PositionCorrection;
            float contactForce = -(velError + beta * m_PositionError / dt) / (invMass * dt);

            float tauA = contactForce * rA;
            float tauB = contactForce * rB;

            // --- Efficiency: load-dependent losses ---
            //
            // Determine power-flow direction.
            //   P_A = omega_A * tau_A:
            //     P_A > 0  =>  A is receiving power (being driven)
            //     P_A < 0  =>  A is outputting power  (driving)
            //
            // For a lossy gear mesh, the driven gear receives eta * ideal_torque.
            // The constraint compensates in subsequent frames, causing the driver
            // to feel proportionally more resistance.
            if (m_Efficiency < 1f)
            {
                float powA = omegaA * tauA;

                if (Mathf.Abs(powA) > 1e-6f)
                {
                    if (powA < 0f)      tauB *= m_Efficiency;   // A drives, B receives less
                    else                 tauA *= m_Efficiency;   // B drives, A receives less
                }
            }

            // --- Damping: viscous friction (bearing losses, independent of load) ---
            if (m_Damping > 0f)
            {
                float iA = 1f / Mathf.Max(invIA, 1e-8f);
                float iB = 1f / Mathf.Max(invIB, 1e-8f);
                tauA -= m_Damping * omegaA * iA;
                tauB -= m_Damping * omegaB * iB;
            }

            // --- Torque limiting (optional safety clamp) ---
            if (m_MaxTorque > 0f)
            {
                tauA = Mathf.Clamp(tauA, -m_MaxTorque, m_MaxTorque);
                tauB = Mathf.Clamp(tauB, -m_MaxTorque, m_MaxTorque);
            }

            // --- Apply ---
            m_GearA.AddTorque(axisA * tauA, ForceMode.Force);
            m_GearB.AddTorque(axisB * tauB, ForceMode.Force);

            // --- Decay position error to prevent integral windup ---
            m_PositionError *= Mathf.Clamp01(1f - beta * 0.1f);
            m_PositionError = Mathf.Clamp(m_PositionError, -10f, 10f);
        }

        // --------------------------------------------------------------------------------
        // Helpers
        // --------------------------------------------------------------------------------

        /// <summary>
        /// Returns the inverse rotational inertia of <paramref name="rb"/> about
        /// <paramref name="worldAxis"/>, or 0 if the body is kinematic / has zero inertia.
        /// </summary>
        private static float GetInverseInertiaAboutAxis(Rigidbody rb, Vector3 worldAxis)
        {
            if (rb == null || rb.isKinematic) return 0f;

            // Transform world axis into the inertia-tensor's local-diagonal space.
            Vector3 localAxis = Quaternion.Inverse(rb.inertiaTensorRotation) * worldAxis;
            localAxis.Normalize();

            Vector3 i = rb.inertiaTensor;

            // I_axis = diag(ix,iy,iz) · (localAxis²)   (since the tensor is diagonal in
            // its principal-axis frame)
            float I = localAxis.x * localAxis.x * i.x
                    + localAxis.y * localAxis.y * i.y
                    + localAxis.z * localAxis.z * i.z;

            if (I < 1e-8f) return 0f;
            return 1f / I;
        }

        // --------------------------------------------------------------------------------
        // Gizmos
        // --------------------------------------------------------------------------------

        private void OnDrawGizmos()
        {
            if (!m_DebugDraw) return;
            GearConstraintDebugger.Draw(this);
        }
    }
}
