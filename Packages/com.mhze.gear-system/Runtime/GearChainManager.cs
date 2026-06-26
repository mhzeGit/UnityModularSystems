using System.Collections.Generic;
using UnityEngine;

namespace MHZE.GearSystem
{
    [DefaultExecutionOrder(-50)]
    public class GearChainManager : MonoBehaviour
    {
        private static GearChainManager s_Instance;

        private readonly List<GearConstraint> m_Constraints = new();
        private readonly List<GearChain> m_Chains = new();
        private bool m_Dirty;

        // --------------------------------------------------------------------
        // Auto-spawn / singleton
        // --------------------------------------------------------------------

        private static void EnsureInstance()
        {
            if (s_Instance != null) return;
            var go = new GameObject("[GearChainManager]");
            s_Instance = go.AddComponent<GearChainManager>();
            if (Application.isPlaying)
                DontDestroyOnLoad(go);
        }

        public static bool exists => s_Instance != null;

        // --------------------------------------------------------------------
        // Registration
        // --------------------------------------------------------------------

        public static void Register(GearConstraint constraint)
        {
            EnsureInstance();
            s_Instance.m_Constraints.Add(constraint);
            s_Instance.m_Dirty = true;
        }

        public static void Unregister(GearConstraint constraint)
        {
            if (s_Instance == null) return;
            s_Instance.m_Constraints.Remove(constraint);
            s_Instance.m_Dirty = true;

            if (s_Instance.m_Constraints.Count == 0 && Application.isPlaying)
            {
                Destroy(s_Instance.gameObject);
            }
        }

        public static void NotifyTopologyChanged()
        {
            if (s_Instance != null)
                s_Instance.m_Dirty = true;
        }

        // --------------------------------------------------------------------
        // Unity messages
        // --------------------------------------------------------------------

        private void Awake()
        {
            if (s_Instance == null)
                s_Instance = this;
        }

        private void OnDestroy()
        {
            if (s_Instance == this)
                s_Instance = null;
        }

        private void FixedUpdate()
        {
            if (m_Dirty)
            {
                RebuildChains();
                if (m_Chains.Count == 0) return;
            }

            float dt = Time.fixedDeltaTime;
            if (dt <= 0f) return;

            for (int i = 0; i < m_Chains.Count; i++)
            {
                if (!m_Chains[i].IsValid())
                {
                    m_Dirty = true;
                    return;
                }
                m_Chains[i].Solve(dt);
            }
        }

        // --------------------------------------------------------------------
        // Chain discovery (connected-component BFS on constraint graph)
        // --------------------------------------------------------------------

        private void RebuildChains()
        {
            m_Dirty = false;
            m_Chains.Clear();

            int count = m_Constraints.Count;
            if (count == 0) return;

            for (int i = count - 1; i >= 0; i--)
            {
                if (m_Constraints[i] == null)
                {
                    m_Constraints.RemoveAt(i);
                    count--;
                }
            }
            if (count == 0) return;

            var rbToConstraints = new Dictionary<Rigidbody, List<GearConstraint>>();
            var validConstraints = new List<GearConstraint>(count);

            for (int i = 0; i < count; i++)
            {
                var c = m_Constraints[i];
                if (c == null || c.gearA == null || c.gearB == null) continue;
                validConstraints.Add(c);

                if (!rbToConstraints.TryGetValue(c.gearA, out var listA))
                {
                    listA = new List<GearConstraint>();
                    rbToConstraints[c.gearA] = listA;
                }
                listA.Add(c);

                if (!rbToConstraints.TryGetValue(c.gearB, out var listB))
                {
                    listB = new List<GearConstraint>();
                    rbToConstraints[c.gearB] = listB;
                }
            }

            var visited = new HashSet<GearConstraint>();

            for (int i = 0; i < validConstraints.Count; i++)
            {
                var seed = validConstraints[i];
                if (!visited.Add(seed)) continue;

                var chain = new GearChain();
                var queue = new Queue<GearConstraint>();
                queue.Enqueue(seed);

                while (queue.Count > 0)
                {
                    var c = queue.Dequeue();
                    chain.constraints.Add(c);

                    Rigidbody[] rbs = { c.gearA, c.gearB };
                    foreach (var rb in rbs)
                    {
                        if (rb == null || !rbToConstraints.TryGetValue(rb, out var neighbors))
                            continue;
                        foreach (var n in neighbors)
                        {
                            if (visited.Add(n))
                                queue.Enqueue(n);
                        }
                    }
                }

                chain.Initialize();
                m_Chains.Add(chain);
            }
        }

        // ================================================================
        //  GearChain — one connected component
        // ================================================================

        private class GearChain
        {
            public readonly List<GearConstraint> constraints = new();
            private readonly List<Rigidbody> rigidbodies = new();

            // Per-constraint cached data
            private Vector3[] m_LocalAxisA;
            private Vector3[] m_LocalAxisB;
            private float[] rA, rB;
            private float[] invIA, invIB;
            private float[] damping;
            private float[] efficiency;
            private float[] maxTorque;
            private float[] positionCorrection;
            private float[] sleepThreshold;
            private bool[] detectTransformRotation;

            // Precomputed rigidbody index for each constraint's gearA/B
            private int[] m_GearAIndex;
            private int[] m_GearBIndex;

            // Per-rigidbody data
            private int[] m_RbStart;
            private GearAxis[] m_RigidbodyAxis;

            // Runtime state — per constraint (persisted frame-to-frame)
            private float[] m_PositionError;
            private int[] m_SleepCounter;
            private bool[] m_HasInitialBlend;

            // Runtime state — per rigidbody (persisted frame-to-frame)
            private Quaternion[] m_PrevRot;
            private bool[] m_HasPrevRot;

            // Per-frame buffers (zeroed at start of each Solve)
            private float[] m_Omega;        // cached local angular velocity
            private float[] m_TorqueBuffer; // accumulated torque per rigidbody
            private float[] m_Lambda;       // accumulated impulse per constraint

            // Per-rigidbody flag: was initial blend applied this frame? (applied as direct impulse)
            private float[] m_InitialBlendDelta;

            private int m_ChainSleepCounter;

            private const int MAX_ITERATIONS = 20;
            private const float EPS = 1e-12f;
            private const float CONVERGENCE_THRESHOLD = 0.001f;

            // --------------------------------------------------------------------
            // Initialization — called once when chain is discovered
            // --------------------------------------------------------------------

            public void Initialize()
            {
                int n = constraints.Count;

                m_LocalAxisA = new Vector3[n];
                m_LocalAxisB = new Vector3[n];
                rA = new float[n];
                rB = new float[n];
                invIA = new float[n];
                invIB = new float[n];
                damping = new float[n];
                efficiency = new float[n];
                maxTorque = new float[n];
                positionCorrection = new float[n];
                sleepThreshold = new float[n];
                detectTransformRotation = new bool[n];
                m_GearAIndex = new int[n];
                m_GearBIndex = new int[n];

                m_PositionError = new float[n];
                m_SleepCounter = new int[n];
                m_HasInitialBlend = new bool[n];
                m_Lambda = new float[n];

                var rbSet = new HashSet<Rigidbody>();
                for (int i = 0; i < n; i++)
                {
                    var c = constraints[i];
                    if (c.gearA != null) rbSet.Add(c.gearA);
                    if (c.gearB != null) rbSet.Add(c.gearB);
                }
                rigidbodies.Clear();
                rigidbodies.AddRange(rbSet);
                int rbCount = rigidbodies.Count;

                m_RbStart = new int[rbCount];
                m_RigidbodyAxis = new GearAxis[rbCount];
                m_PrevRot = new Quaternion[rbCount];
                m_HasPrevRot = new bool[rbCount];
                m_Omega = new float[rbCount];
                m_TorqueBuffer = new float[rbCount];
                m_InitialBlendDelta = new float[rbCount];

                var rbIndex = new Dictionary<Rigidbody, int>(rbCount);
                for (int i = 0; i < rbCount; i++)
                {
                    rbIndex[rigidbodies[i]] = i;
                    m_RbStart[i] = -1;
                }

                for (int i = 0; i < n; i++)
                {
                    var c = constraints[i];

                    m_LocalAxisA[i] = GearConstraint.GetAxisVector(c.axisA);
                    m_LocalAxisB[i] = GearConstraint.GetAxisVector(c.axisB);
                    rA[i] = c.radiusA;
                    rB[i] = c.radiusB;
                    damping[i] = c.damping;
                    efficiency[i] = c.efficiency;
                    maxTorque[i] = c.maxTorque;
                    positionCorrection[i] = c.positionCorrection;
                    sleepThreshold[i] = c.sleepThreshold;
                    detectTransformRotation[i] = c.detectTransformRotation;

                    m_GearAIndex[i] = rbIndex.TryGetValue(c.gearA, out int idxA) ? idxA : -1;
                    m_GearBIndex[i] = rbIndex.TryGetValue(c.gearB, out int idxB) ? idxB : -1;

                    if (m_GearAIndex[i] >= 0 && m_RbStart[m_GearAIndex[i]] == -1)
                    {
                        m_RbStart[m_GearAIndex[i]] = i;
                        m_RigidbodyAxis[m_GearAIndex[i]] = c.axisA;
                    }
                    if (m_GearBIndex[i] >= 0 && m_RbStart[m_GearBIndex[i]] == -1)
                    {
                        m_RbStart[m_GearBIndex[i]] = i;
                        m_RigidbodyAxis[m_GearBIndex[i]] = c.axisB;
                    }
                }
            }

            // --------------------------------------------------------------------
            // Validation
            // --------------------------------------------------------------------

            public bool IsValid()
            {
                for (int i = 0; i < rigidbodies.Count; i++)
                    if (rigidbodies[i] == null) return false;
                for (int i = 0; i < constraints.Count; i++)
                    if (constraints[i] == null) return false;
                return true;
            }

            // --------------------------------------------------------------------
            // Main solver — runs once per FixedUpdate
            // --------------------------------------------------------------------

            public void Solve(float dt)
            {
                int n = constraints.Count;
                int rbCount = rigidbodies.Count;

                // --- reset per-frame buffers ---
                System.Array.Clear(m_Lambda, 0, n);
                System.Array.Clear(m_TorqueBuffer, 0, rbCount);
                System.Array.Clear(m_InitialBlendDelta, 0, rbCount);

                // ================================================================
                // Phase 1 — snapshot current angular velocities, compute inertias
                // ================================================================

                for (int i = 0; i < rbCount; i++)
                {
                    var rb = rigidbodies[i];
                    GearAxis axis = m_RigidbodyAxis[i];
                    int ci = m_RbStart[i];
                    bool detectTrans = ci >= 0 && detectTransformRotation[ci];

                    m_Omega[i] = GetBodyAngularVelocity(
                        rb, axis, ref m_PrevRot[i], ref m_HasPrevRot[i], dt, detectTrans);
                }

                for (int i = 0; i < n; i++)
                {
                    invIA[i] = GetInverseInertiaAboutBodyAxis(constraints[i].gearA, m_LocalAxisA[i]);
                    invIB[i] = GetInverseInertiaAboutBodyAxis(constraints[i].gearB, m_LocalAxisB[i]);
                }

                // ================================================================
                // Phase 2 — initial velocity blend (one-shot at creation)
                //            Applied as a direct impulse to the rigidbody AND
                //            reflected in cached m_Omega for subsequent phases.
                // ================================================================

                for (int i = 0; i < n; i++)
                {
                    if (m_HasInitialBlend[i]) continue;
                    ApplyInitialBlend(i);
                    m_HasInitialBlend[i] = true;
                }

                // ================================================================
                // Phase 3 — chain-wide sleep check
                // ================================================================

                if (TrySleep(rbCount, n))
                    return;

                // ================================================================
                // Phase 4 — Gauss-Seidel iteration over all constraints
                //          Each iteration propagates constraint impulses through
                //          the chain via cached m_Omega.
                //          Impulses are accumulated in m_Lambda per constraint.
                // ================================================================

                for (int iter = 0; iter < MAX_ITERATIONS; iter++)
                {
                    float maxError = 0f;

                    for (int i = 0; i < n; i++)
                    {
                        int idxA = m_GearAIndex[i];
                        int idxB = m_GearBIndex[i];

                        float omegaA = m_Omega[idxA];
                        float omegaB = m_Omega[idxB];
                        float riA = rA[i];
                        float riB = rB[i];
                        float iA = invIA[i];
                        float iB = invIB[i];

                        float velError = omegaA * riA + omegaB * riB;
                        maxError = Mathf.Max(maxError, Mathf.Abs(velError));

                        float invMass = iA * riA * riA + iB * riB * riB;
                        if (invMass < EPS)
                            continue;

                        m_PositionError[i] = m_PositionError[i] * 0.9f + velError * dt;

                        float beta = positionCorrection[i];
                        float lambda = -(velError + beta * m_PositionError[i] / dt) / invMass;

                        float deltaA = lambda * riA * iA;
                        float deltaB = lambda * riB * iB;

                        // Update cached velocities immediately so subsequent
                        // constraints in this iteration see the correction.
                        m_Omega[idxA] += deltaA;
                        m_Omega[idxB] += deltaB;

                        // Accumulate impulse for final torque conversion.
                        m_Lambda[i] += lambda;
                    }

                    if (maxError < CONVERGENCE_THRESHOLD)
                        break;
                }

                // ================================================================
                // Phase 5 — convert accumulated impulses to torques,
                //           apply efficiency / damping / torque limiting.
                // ================================================================

                // The cached m_Omega is now the fully corrected velocity
                // (post all Gauss-Seidel iterations).  Compute the pre-Gauss-Seidel
                // (but post-initial-blend) velocity for power-flow and damping.
                for (int i = 0; i < n; i++)
                {
                    int idxA = m_GearAIndex[i];
                    int idxB = m_GearBIndex[i];

                    float initOmegaA = m_Omega[idxA] - GetDeltaFromLambda(i, idxA);
                    float initOmegaB = m_Omega[idxB] - GetDeltaFromLambda(i, idxB);

                    float riA = rA[i];
                    float riB = rB[i];
                    float dtInv = 1f / dt;

                    float tauA = m_Lambda[i] * riA * dtInv;
                    float tauB = m_Lambda[i] * riB * dtInv;

                    // Efficiency (power-flow direction based on pre-correction velocity)
                    if (efficiency[i] < 1f)
                    {
                        float powA = initOmegaA * tauA;
                        if (Mathf.Abs(powA) > 1e-6f)
                        {
                            if (powA < 0f) tauB *= efficiency[i];
                            else           tauA *= efficiency[i];
                        }
                    }

                    // Damping
                    if (damping[i] > 0f)
                    {
                        float IA = 1f / Mathf.Max(invIA[i], 1e-8f);
                        tauA -= damping[i] * initOmegaA * IA;
                        float IB = 1f / Mathf.Max(invIB[i], 1e-8f);
                        tauB -= damping[i] * initOmegaB * IB;
                    }

                    // Torque limiting
                    if (maxTorque[i] > 0f)
                    {
                        tauA = Mathf.Clamp(tauA, -maxTorque[i], maxTorque[i]);
                        tauB = Mathf.Clamp(tauB, -maxTorque[i], maxTorque[i]);
                    }

                    m_TorqueBuffer[idxA] += tauA;
                    m_TorqueBuffer[idxB] += tauB;
                }

                // ================================================================
                // Phase 6 — apply results to real rigidbodies
                //           (initial blend as direct impulse, Gauss-Seidel as torque)
                // ================================================================

                for (int i = 0; i < rbCount; i++)
                {
                    var rb = rigidbodies[i];
                    GearAxis axis = m_RigidbodyAxis[i];
                    Vector3 localAxis = GearConstraint.GetAxisVector(axis);

                    // Apply initial blend as direct velocity impulse (matches
                    // original GearConstraint.ApplyInitialVelocityBlend behaviour).
                    float blendDelta = m_InitialBlendDelta[i];
                    if (Mathf.Abs(blendDelta) > 1e-12f)
                    {
                        Vector3 worldAxis = rb.transform.TransformDirection(localAxis);
                        rb.angularVelocity += worldAxis * blendDelta;
                    }

                    // Apply Gauss-Seidel torque.
                    float totalTau = m_TorqueBuffer[i];
                    if (Mathf.Abs(totalTau) > 1e-12f)
                    {
                        rb.AddRelativeTorque(localAxis * totalTau, ForceMode.Force);
                    }
                }

                // Clamp position errors.
                for (int i = 0; i < n; i++)
                    m_PositionError[i] = Mathf.Clamp(m_PositionError[i], -0.5f, 0.5f);
            }

            // --------------------------------------------------------------------
            // Sleep
            // --------------------------------------------------------------------

            private bool TrySleep(int rbCount, int constraintCount)
            {
                bool allSleeping = true;
                for (int i = 0; i < constraintCount; i++)
                {
                    int idxA = m_GearAIndex[i];
                    int idxB = m_GearBIndex[i];
                    float omegaA = m_Omega[idxA];
                    float omegaB = m_Omega[idxB];
                    float velError = omegaA * rA[i] + omegaB * rB[i];
                    float thr = sleepThreshold[i];

                    if (Mathf.Abs(omegaA) > thr ||
                        Mathf.Abs(omegaB) > thr ||
                        Mathf.Abs(velError) > thr * 0.1f)
                    {
                        allSleeping = false;
                        break;
                    }
                }

                if (allSleeping)
                {
                    m_ChainSleepCounter++;
                    if (m_ChainSleepCounter > 10)
                    {
                        for (int i = 0; i < rbCount; i++)
                        {
                            var rb = rigidbodies[i];
                            GearAxis axis = m_RigidbodyAxis[i];
                            Vector3 localAxis = GearConstraint.GetAxisVector(axis);
                            Vector3 worldAxis = rb.transform.TransformDirection(localAxis);
                            Vector3 proj = Vector3.Project(rb.angularVelocity, worldAxis);
                            rb.angularVelocity -= proj;
                        }
                        return true;
                    }
                }
                else
                {
                    m_ChainSleepCounter = 0;
                }
                return false;
            }

            // --------------------------------------------------------------------
            // Helpers
            // --------------------------------------------------------------------

            private float GetDeltaFromLambda(int constraintIndex, int rbIndex)
            {
                float lambda = m_Lambda[constraintIndex];
                var rb = rigidbodies[rbIndex];
                var c = constraints[constraintIndex];
                if (rb == c.gearA) return lambda * rA[constraintIndex] * invIA[constraintIndex];
                if (rb == c.gearB) return lambda * rB[constraintIndex] * invIB[constraintIndex];
                return 0f;
            }

            private void ApplyInitialBlend(int ci)
            {
                int idxA = m_GearAIndex[ci];
                int idxB = m_GearBIndex[ci];
                if (idxA < 0 || idxB < 0) return;

                float riA = rA[ci];
                float riB = rB[ci];
                float iA = invIA[ci];
                float iB = invIB[ci];

                float omegaA0 = m_Omega[idxA];
                float omegaB0 = m_Omega[idxB];
                float velError = omegaA0 * riA + omegaB0 * riB;

                if (Mathf.Abs(velError) < 1e-10f) return;
                if (iA < 1e-12f && iB < 1e-12f) return;

                float invMass = iA * riA * riA + iB * riB * riB;
                if (invMass < 1e-12f) return;

                float J = -velError / invMass;
                float deltaA = J * riA * iA;
                float deltaB = J * riB * iB;

                // Update cached omega for subsequent phases.
                m_Omega[idxA] += deltaA;
                m_Omega[idxB] += deltaB;

                // Store for direct impulse application at end of Solve().
                m_InitialBlendDelta[idxA] += deltaA;
                m_InitialBlendDelta[idxB] += deltaB;

                m_PositionError[ci] = 0f;
            }

            private static float GetBodyAngularVelocity(
                Rigidbody rb, GearAxis axis,
                ref Quaternion prevRot, ref bool hasPrev, float dt, bool detectTransform)
            {
                Vector3 bodyAxis = GearConstraint.GetAxisVector(axis);
                Vector3 localOmega = rb.transform.InverseTransformDirection(rb.angularVelocity);
                float rigidOmega = Vector3.Dot(localOmega, bodyAxis);

                if (!detectTransform)
                    return rigidOmega;

                Quaternion current = rb.transform.rotation;
                float transformOmega = 0f;

                if (hasPrev)
                {
                    float angleDiff = Quaternion.Angle(prevRot, current);
                    if (angleDiff > 0.0001f)
                    {
                        Quaternion delta = Quaternion.Inverse(prevRot) * current;
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

                if (rb.isKinematic && absTrans > absRigid) return transformOmega;
                if (absRigid < 1e-4f && absTrans > 1e-4f) return transformOmega;
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
        }
    }
}
