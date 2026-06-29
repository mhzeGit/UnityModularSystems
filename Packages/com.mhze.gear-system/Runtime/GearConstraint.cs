using UnityEngine;

namespace MHZE.GearSystem
{
    public enum GearAxis { X, Y, Z }

    [AddComponentMenu("Mechanical/Gear Constraint")]
    public class GearConstraint : MonoBehaviour
    {
        [Header("Gear A")]
        public Transform gearA;
        public float radiusA = 0.5f;
        public GearAxis axisA = GearAxis.Y;

        [Header("Gear B")]
        public Transform gearB;
        public float radiusB = 0.5f;
        public GearAxis axisB = GearAxis.Y;

        [Header("Visual")]
        public float toothDensity = 5f;
        public float toothHeight = 0.1f;
        public bool debugDraw;

        [Header("Limit")]
        [Tooltip("Maximum constraint torque (Nm). 0 = unlimited.")]
        public float maxTorque = 0f;

        [Tooltip("Log debug values to console when enabled.")]
        public bool debugLog;

        private float m_ArcLength;
        private float m_PrevContactAngleA;
        private float m_AppliedAngleB;
        private bool m_HasInitialized;

        public float arcLength => m_ArcLength;

        private void Start()
        {
            if (gearA == null || gearB == null) return;

            Vector3 dir = (gearB.position - gearA.position).normalized;
            m_PrevContactAngleA = GetContactAngle(gearA, axisA, dir);
            m_HasInitialized = true;
        }

        private void Update()
        {
            if (!m_HasInitialized || gearA == null || gearB == null)
                return;
            if (radiusA <= 0f || radiusB <= 0f)
                return;

            Vector3 centerA = gearA.position;
            Vector3 centerB = gearB.position;
            Vector3 contactDir = (centerB - centerA).normalized;

            Vector3 contactPoint = centerA + contactDir * radiusA;

            float contactAngleA = GetContactAngle(gearA, axisA, contactDir);
            float deltaContactA = Mathf.DeltaAngle(m_PrevContactAngleA, contactAngleA);

            float deltaS = -deltaContactA * Mathf.Deg2Rad * radiusA;
            m_ArcLength += deltaS;

            float targetAngleB = -(m_ArcLength / radiusB) * Mathf.Rad2Deg;
            float deltaB = targetAngleB - m_AppliedAngleB;

            if (Mathf.Abs(deltaB) > 1e-6f)
            {
                Vector3 worldAxisB = GetWorldAxis(gearB, axisB);
                gearB.Rotate(worldAxisB, deltaB, Space.World);
                m_AppliedAngleB = targetAngleB;
            }

            if (debugLog)
            {
                Debug.Log($"[GearConstraint] cp={contactPoint:F3} " +
                          $"s={m_ArcLength:F6} " +
                          $"cAngA={contactAngleA:F2}°(Δ{deltaContactA:F4}) " +
                          $"θB_applied={m_AppliedAngleB:F2}° ΔB={deltaB:F4} " +
                          $"ratio={radiusA / radiusB:F4}");
            }

            m_PrevContactAngleA = contactAngleA;
        }

        private static float GetContactAngle(Transform gear, GearAxis axis, Vector3 worldDirection)
        {
            Vector3 worldAxis = GetWorldAxis(gear, axis);
            Vector3 projDir = Vector3.ProjectOnPlane(worldDirection, worldAxis);
            Vector3 projRight = Vector3.ProjectOnPlane(gear.right, worldAxis);

            if (projDir.sqrMagnitude < 1e-8f || projRight.sqrMagnitude < 1e-8f)
                return 0f;

            return Vector3.SignedAngle(projRight.normalized, projDir.normalized, worldAxis);
        }

        private static Vector3 GetWorldAxis(Transform t, GearAxis axis)
        {
            return axis switch
            {
                GearAxis.X => t.right,
                GearAxis.Z => t.forward,
                _ => t.up
            };
        }

        private void OnDrawGizmos()
        {
            if (debugDraw)
            {
                GearConstraintDebugger.Draw(this);
            }
        }
    }
}
