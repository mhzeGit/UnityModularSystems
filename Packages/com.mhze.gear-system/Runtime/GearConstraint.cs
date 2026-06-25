using UnityEngine;

namespace MHZE.GearSystem
{
    public enum GearAxis { X, Y, Z }

    [AddComponentMenu("Mechanical/Gear Constraint")]
    public class GearConstraint : MonoBehaviour
    {
        [Header("Gears")]
        [SerializeField] private Rigidbody m_GearA;
        [SerializeField] private Rigidbody m_GearB;
        [SerializeField] private float m_RadiusA = 0.5f;
        [SerializeField] private float m_RadiusB = 0.5f;
        [SerializeField] private GearAxis m_AxisA = GearAxis.Y;
        [SerializeField] private GearAxis m_AxisB = GearAxis.Y;

        [Header("Visual")]
        [SerializeField] private float m_ToothDensity = 5f;
        [SerializeField] private float m_ToothHeight = 0.1f;
        [SerializeField] private bool m_DebugDraw;

        [Header("Physics")]
        [SerializeField]
        [Range(0f, 100f)]
        [Tooltip("Viscous damping applied per-gear.")]
        private float m_Damping = 0f;

        [SerializeField]
        [Range(0.001f, 1f)]
        [Tooltip("Baumgarte stabilization factor.")]
        private float m_PositionCorrection = 0.2f;

        [SerializeField]
        [Tooltip("Maximum constraint torque (Nm). 0 = unlimited.")]
        private float m_MaxTorque = 0f;

        [SerializeField]
        [Range(0.01f, 1f)]
        [Tooltip("Transmission efficiency.")]
        private float m_Efficiency = 1f;

        [SerializeField]
        [Tooltip("Auto-disable collision between gear colliders.")]
        private bool m_IgnoreCollisionBetweenGears = true;

        [SerializeField]
        [Tooltip("Angular velocity below which the constraint can sleep.")]
        private float m_SleepThreshold = 0.01f;

        [SerializeField]
        [Tooltip("Detect rotation applied via Transform (e.g. Animator).")]
        private bool m_DetectTransformRotation = true;

        [SerializeField]
        [Tooltip("Log debug values to console when enabled.")]
        private bool m_DebugLog;

        // --- Runtime state ---
        private float m_PositionError;
        private int m_SleepCounter;
        private int m_FrameCount;
        private bool m_HasAppliedInitialBlend;

        // Transform-rotation tracking
        private Quaternion m_PrevRotA = Quaternion.identity;
        private Quaternion m_PrevRotB = Quaternion.identity;
        private bool m_HasPrevRotA;
        private bool m_HasPrevRotB;

        // Cached debug values for last frame
        private float m_LastOmegaA;
        private float m_LastOmegaB;
        private float m_LastVelError;
        private float m_LastContactForce;
        private float m_LastTauA;
        private float m_LastTauB;
        private float m_LastInvIA;
        private float m_LastInvIB;
        private bool m_LastSlept;

        // --------------------------------------------------------------------------------
        // Public Properties
        // --------------------------------------------------------------------------------

        public GearAxis axisA
        {
            get => m_AxisA;
            set => m_AxisA = value;
        }

        public GearAxis axisB
        {
            get => m_AxisB;
            set => m_AxisB = value;
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

        public int GetToothCount(float radius)
        {
            int count = Mathf.RoundToInt(2f * Mathf.PI * radius * m_ToothDensity);
            if (count % 2 != 0)
                count++;
            return Mathf.Max(4, count);
        }

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

        public Rigidbody gearA
        {
            get => m_GearA;
            set
            {
                m_GearA = value;
                AlignTransformToGears();
            }
        }

        public Rigidbody gearB
        {
            get => m_GearB;
            set
            {
                m_GearB = value;
                AlignTransformToGears();
            }
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

        public bool detectTransformRotation
        {
            get => m_DetectTransformRotation;
            set => m_DetectTransformRotation = value;
        }

        public bool debugLog
        {
            get => m_DebugLog;
            set => m_DebugLog = value;
        }

        public static Vector3 GetAxisVector(GearAxis axis)
        {
            return axis switch
            {
                GearAxis.X => Vector3.right,
                GearAxis.Z => Vector3.forward,
                _ => Vector3.up
            };
        }

        private static void GetReferenceAxes(GearAxis axis, out Vector3 axisDir, out Vector3 b1, out Vector3 b2)
        {
            switch (axis)
            {
                case GearAxis.X:
                    axisDir = Vector3.right; b1 = Vector3.up; b2 = Vector3.forward;
                    break;
                case GearAxis.Z:
                    axisDir = Vector3.forward; b1 = Vector3.right; b2 = Vector3.up;
                    break;
                default:
                    axisDir = Vector3.up; b1 = Vector3.right; b2 = Vector3.forward;
                    break;
            }
        }

        public float gearRatio => m_RadiusB / m_RadiusA;

        // --------------------------------------------------------------------------------
        // Tooth alignment
        // --------------------------------------------------------------------------------

        public void AlignTeeth()
        {
            // Alignment is disabled — the debug draw always matches the gear transform directly.
        }

        // --------------------------------------------------------------------------------
        // Unity Messages
        // --------------------------------------------------------------------------------

        private void OnEnable()
        {
            m_PositionError = 0f;
            m_SleepCounter = 0;
            m_FrameCount = 0;
            m_HasAppliedInitialBlend = false;
            m_HasPrevRotA = false;
            m_HasPrevRotB = false;
        }

        private void OnValidate()
        {
            AlignTransformToGears();
        }

        private void Start()
        {
            AlignTransformToGears();

            if (m_IgnoreCollisionBetweenGears && m_GearA != null && m_GearB != null)
            {
                Collider[] collidersA = m_GearA.GetComponentsInChildren<Collider>();
                Collider[] collidersB = m_GearB.GetComponentsInChildren<Collider>();
                foreach (Collider ca in collidersA)
                {
                    if (ca == null || ca.isTrigger) continue;
                    foreach (Collider cb in collidersB)
                    {
                        if (cb == null || cb.isTrigger) continue;
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

            m_FrameCount++;
            m_LastSlept = false;

            // Apply one-time velocity blend on the first frame so the constraint
            // starts from an equilibrium state rather than applying a sudden snap.
            if (!m_HasAppliedInitialBlend)
            {
                ApplyInitialVelocityBlend();
                m_HasAppliedInitialBlend = true;
            }

            // Work in each rigidbody's LOCAL frame.
            Vector3 localAxisA = GetAxisVector(m_AxisA);
            Vector3 localAxisB = GetAxisVector(m_AxisB);

            float omegaA = GetBodyAngularVelocity(m_GearA, localAxisA, ref m_PrevRotA, ref m_HasPrevRotA, dt);
            float omegaB = GetBodyAngularVelocity(m_GearB, localAxisB, ref m_PrevRotB, ref m_HasPrevRotB, dt);

            m_LastOmegaA = omegaA;
            m_LastOmegaB = omegaB;

            float rA = m_RadiusA;
            float rB = m_RadiusB;

            float velError = omegaA * rA + omegaB * rB;
            m_LastVelError = velError;

            float invIA = GetInverseInertiaAboutBodyAxis(m_GearA, localAxisA);
            float invIB = GetInverseInertiaAboutBodyAxis(m_GearB, localAxisB);

            m_LastInvIA = invIA;
            m_LastInvIB = invIB;

            float invMass = invIA * rA * rA + invIB * rB * rB;
            if (invMass < 1e-12f)
            {
                if (m_DebugLog && m_FrameCount <= 3)
                    Debug.Log($"[GearConstraint] Frame {m_FrameCount}: invMass={invMass:e} is zero. Both gears kinematic? invIA={invIA} invIB={invIB}");
                return;
            }

            m_PositionError = m_PositionError * 0.9f + velError * dt;

            // --- Sleep heuristic ---
            float absVelError = Mathf.Abs(velError);
            float speedB = Mathf.Abs(omegaB);
            bool gearBIsSlow = speedB < m_SleepThreshold;
            bool constraintSatisfied = absVelError < m_SleepThreshold * 0.1f;

            if (gearBIsSlow && constraintSatisfied && Mathf.Abs(omegaA) < m_SleepThreshold)
            {
                m_SleepCounter++;
                if (m_SleepCounter > 10)
                {
                    m_LastSlept = true;
                    m_GearB.angularVelocity = Vector3.MoveTowards(m_GearB.angularVelocity, Vector3.zero, m_SleepThreshold * 0.1f);
                    m_GearA.angularVelocity = Vector3.MoveTowards(m_GearA.angularVelocity, Vector3.zero, m_SleepThreshold * 0.1f);
                    return;
                }
            }
            else
            {
                m_SleepCounter = 0;
            }

            // --- Compute constraint force ---
            float beta = m_PositionCorrection;
            float contactForce = -(velError + beta * m_PositionError / dt) / (invMass * dt);

            m_LastContactForce = contactForce;

            float tauA = contactForce * rA;
            float tauB = contactForce * rB;

            // Efficiency
            if (m_Efficiency < 1f)
            {
                float powA = omegaA * tauA;
                if (Mathf.Abs(powA) > 1e-6f)
                {
                    if (powA < 0f) tauB *= m_Efficiency;
                    else           tauA *= m_Efficiency;
                }
            }

            // Damping
            if (m_Damping > 0f)
            {
                float iA = 1f / Mathf.Max(invIA, 1e-8f);
                tauA -= m_Damping * omegaA * iA;
                float iB = 1f / Mathf.Max(invIB, 1e-8f);
                tauB -= m_Damping * omegaB * iB;
            }

            // Torque limiting
            if (m_MaxTorque > 0f)
            {
                tauA = Mathf.Clamp(tauA, -m_MaxTorque, m_MaxTorque);
                tauB = Mathf.Clamp(tauB, -m_MaxTorque, m_MaxTorque);
            }

            m_LastTauA = tauA;
            m_LastTauB = tauB;

            // Apply torques in each rigidbody's LOCAL space.
            m_GearA.AddRelativeTorque(localAxisA * tauA, ForceMode.Force);
            m_GearB.AddRelativeTorque(localAxisB * tauB, ForceMode.Force);

            m_PositionError = Mathf.Clamp(m_PositionError, -0.5f, 0.5f);

            // Debug log every 60 frames
            if (m_DebugLog && m_FrameCount % 60 == 0)
            {
                Debug.Log(
                    $"[GearConstraint] F{m_FrameCount} " +
                    $"omegaA={omegaA:F4} omegaB={omegaB:F4} " +
                    $"velErr={velError:F4} force={contactForce:F2} " +
                    $"tauA={tauA:F2} tauB={tauB:F2} " +
                    $"invIA={invIA:F4} invIB={invIB:F4} " +
                    $"posErr={m_PositionError:F4} slept={m_LastSlept}");
            }
        }

        // --------------------------------------------------------------------------------
        // Initial velocity blend (one-shot at constraint creation)
        // --------------------------------------------------------------------------------

        private void ApplyInitialVelocityBlend()
        {
            Vector3 localAxisA = GetAxisVector(m_AxisA);
            Vector3 localAxisB = GetAxisVector(m_AxisB);

            float omegaA0 = Vector3.Dot(
                m_GearA.transform.InverseTransformDirection(m_GearA.angularVelocity), localAxisA);
            float omegaB0 = Vector3.Dot(
                m_GearB.transform.InverseTransformDirection(m_GearB.angularVelocity), localAxisB);

            float rA = m_RadiusA;
            float rB = m_RadiusB;

            float velError = omegaA0 * rA + omegaB0 * rB;

            if (Mathf.Abs(velError) < 1e-10f)
                return;

            float invIA = GetInverseInertiaAboutBodyAxis(m_GearA, localAxisA);
            float invIB = GetInverseInertiaAboutBodyAxis(m_GearB, localAxisB);

            if (invIA < 1e-12f && invIB < 1e-12f)
                return;

            float invMass = invIA * rA * rA + invIB * rB * rB;
            if (invMass < 1e-12f)
                return;

            // Impulse that satisfies the constraint while conserving angular momentum
            // J = -velError / (rA²/IA + rB²/IB)
            float J = -velError / invMass;

            float deltaOmegaA = J * rA * invIA;
            float deltaOmegaB = J * rB * invIB;

            m_GearA.angularVelocity += m_GearA.transform.TransformDirection(localAxisA * deltaOmegaA);
            m_GearB.angularVelocity += m_GearB.transform.TransformDirection(localAxisB * deltaOmegaB);

            m_PositionError = 0f;
        }

        // --------------------------------------------------------------------------------
        // Helpers
        // --------------------------------------------------------------------------------

        private float GetBodyAngularVelocity(
            Rigidbody rb, Vector3 bodyAxis,
            ref Quaternion prevRot, ref bool hasPrev, float dt)
        {
            Vector3 localOmega = rb.transform.InverseTransformDirection(rb.angularVelocity);
            float rigidOmega = Vector3.Dot(localOmega, bodyAxis);

            if (!m_DetectTransformRotation)
                return rigidOmega;

            Quaternion current = rb.transform.rotation;
            float transformOmega = 0f;

            if (hasPrev)
            {
                Quaternion delta = Quaternion.Inverse(prevRot) * current;
                float angleDiff = Quaternion.Angle(prevRot, current);

                if (angleDiff > 0.0001f)
                {
                    delta.ToAngleAxis(out float angleDeg, out Vector3 rotAxis);

                    Vector3 localRotAxis = rb.transform.InverseTransformDirection(rotAxis);
                    float projectedDeg = angleDeg * Vector3.Dot(localRotAxis, bodyAxis);
                    transformOmega = projectedDeg * Mathf.Deg2Rad / dt;
                }
            }

            prevRot = current;
            hasPrev = true;

            float absRigid = Mathf.Abs(rigidOmega);
            float absTrans = Mathf.Abs(transformOmega);

            if (rb.isKinematic && absTrans > absRigid)
                return transformOmega;

            if (absRigid < 1e-4f && absTrans > 1e-4f)
                return transformOmega;

            return rigidOmega;
        }

        private static float GetInverseInertiaAboutBodyAxis(Rigidbody rb, Vector3 bodyAxis)
        {
            if (rb == null || rb.isKinematic) return 0f;

            Vector3 princAxis = Quaternion.Inverse(rb.inertiaTensorRotation) * bodyAxis;
            princAxis.Normalize();

            Vector3 i = rb.inertiaTensor;
            float I = princAxis.x * princAxis.x * i.x
                    + princAxis.y * princAxis.y * i.y
                    + princAxis.z * princAxis.z * i.z;

            if (I < 1e-8f) return 0f;
            return 1f / I;
        }

        // --------------------------------------------------------------------------------
        // Transform alignment
        // --------------------------------------------------------------------------------

        private void AlignTransformToGears()
        {
            if (m_GearA == null || m_GearB == null) return;

            Vector3 posA = m_GearA.position;
            Vector3 posB = m_GearB.position;
            Vector3 direction = posB - posA;

            transform.position = (posA + posB) * 0.5f;

            if (direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(direction);
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
