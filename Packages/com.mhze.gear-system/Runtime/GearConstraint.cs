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
            m_PositionError = 0f;
            m_SleepCounter = 0;
            m_FrameCount = 0;
            m_HasPrevRotA = false;
            m_HasPrevRotB = false;
        }

        private void Start()
        {
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

            m_FrameCount++;
            m_LastSlept = false;

            // Work in each rigidbody's LOCAL frame to avoid coordinate-mismatch
            // between world-space axes and per-body inertia tensors / freeze constraints.
            Vector3 localAxis = GetAxisVector();

            // Angular velocity about the constraint axis in the body's LOCAL frame.
            float omegaA = GetBodyAngularVelocity(m_GearA, localAxis, ref m_PrevRotA, ref m_HasPrevRotA, dt);
            float omegaB = GetBodyAngularVelocity(m_GearB, localAxis, ref m_PrevRotB, ref m_HasPrevRotB, dt);

            m_LastOmegaA = omegaA;
            m_LastOmegaB = omegaB;

            float rA = m_RadiusA;
            float rB = m_RadiusB;

            float velError = omegaA * rA + omegaB * rB;
            m_LastVelError = velError;

            float invIA = GetInverseInertiaAboutBodyAxis(m_GearA, localAxis);
            float invIB = GetInverseInertiaAboutBodyAxis(m_GearB, localAxis);

            m_LastInvIA = invIA;
            m_LastInvIB = invIB;

            float invMass = invIA * rA * rA + invIB * rB * rB;
            if (invMass < 1e-12f)
            {
                if (m_DebugLog && m_FrameCount <= 3)
                    Debug.Log($"[GearConstraint] Frame {m_FrameCount}: invMass={invMass:e} is zero. Both gears kinematic? invIA={invIA} invIB={invIB}");
                return;
            }

            m_PositionError += velError * dt;

            // --- Sleep heuristic ---
            // Only sleep when the constraint is ALREADY satisfied (velError is tiny).
            // Never sleep while there is an active velocity mismatch.
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

            // Apply torques in each rigidbody's LOCAL space so that
            // FreezeRotation constraints (e.g. freeze X, Z; leave Y free)
            // do not block the torque. AddTorque with a world-space axis
            // can develop cross-axis components from quaternion rounding,
            // which get clamped by frozen rotation axes.
            // AddRelativeTorque applies a clean single-axis torque.
            m_GearA.AddRelativeTorque(localAxis * tauA, ForceMode.Force);
            m_GearB.AddRelativeTorque(localAxis * tauB, ForceMode.Force);

            // Decay position error
            m_PositionError *= Mathf.Clamp01(1f - beta * 0.1f);
            m_PositionError = Mathf.Clamp(m_PositionError, -10f, 10f);

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
        // Helpers
        // --------------------------------------------------------------------------------

        /// <summary>
        /// Returns the scalar angular velocity (rad/s) of <paramref name="rb"/> about its
        /// LOCAL <paramref name="bodyAxis"/> (a unit vector in the body's local frame).
        /// Uses Rigidbody.angularVelocity when available; falls back to Transform-derived
        /// rotation when the rigidbody reports near-zero while the Transform is rotating.
        /// </summary>
        private float GetBodyAngularVelocity(
            Rigidbody rb, Vector3 bodyAxis,
            ref Quaternion prevRot, ref bool hasPrev, float dt)
        {
            // Convert world angular-velocity to body-local and project
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

                    // rotAxis is in world space. Convert to body-local,
                    // project onto the constraint axis.
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

        /// <summary>
        /// Returns the inverse rotational inertia about the body-local <paramref name="bodyAxis"/>
        /// (e.g. (0,1,0) for the local Y axis). Accounts for inertiaTensorRotation.
        /// Returns 0 if the body is kinematic or has zero inertia about that axis.
        /// </summary>
        private static float GetInverseInertiaAboutBodyAxis(Rigidbody rb, Vector3 bodyAxis)
        {
            if (rb == null || rb.isKinematic) return 0f;

            // Convert body-local axis to inertia-principal space.
            // bodyAxis is in the rigidbody's local frame.
            // inertiaTensorRotation rotates from body-local to principal.
            // So principal-axis = R_i^T * bodyAxis = inverse(R_i) * bodyAxis
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
        // Gizmos
        // --------------------------------------------------------------------------------

        private void OnDrawGizmos()
        {
            if (!m_DebugDraw) return;
            GearConstraintDebugger.Draw(this);
        }
    }
}
