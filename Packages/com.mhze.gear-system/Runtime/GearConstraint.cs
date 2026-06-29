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

        [Tooltip("Invert gear B rotation direction.")]
        public bool reverseB;

        private float m_SavedContactA;
        private float m_OffsetA;
        private float m_AppliedB;

        public float arcLength => m_OffsetA * Mathf.Deg2Rad * radiusA;

        private void Start()
        {
            if (gearA == null || gearB == null) return;

            Vector3 dir = (gearB.position - gearA.position).normalized;
            m_SavedContactA = GetContactAngle(gearA, axisA, dir);
            m_OffsetA = 0f;
            m_AppliedB = 0f;
        }

        private void Update()
        {
            if (gearA == null || gearB == null) return;
            if (radiusA <= 0f || radiusB <= 0f) return;

            Vector3 dir = (gearB.position - gearA.position).normalized;

            float contactA = GetContactAngle(gearA, axisA, dir);

            m_OffsetA += Mathf.DeltaAngle(m_SavedContactA + m_OffsetA, contactA);

            float targetB = m_OffsetA * (radiusA / radiusB);
            if (reverseB) targetB = -targetB;

            float correction = targetB - m_AppliedB;

            if (Mathf.Abs(correction) > 0.1f)
            {
                gearB.Rotate(GetWorldAxis(gearB, axisB), correction, Space.World);
                m_AppliedB = targetB;
            }

            if (debugLog)
            {
                Debug.Log($"[GearConstraint] s={m_OffsetA * Mathf.Deg2Rad * radiusA:F6} " +
                          $"ΔA={m_OffsetA:F1}° ΔB={m_AppliedB:F1}°");
            }
        }

        private static float GetContactAngle(Transform gear, GearAxis axis, Vector3 worldDirection)
        {
            Vector3 worldAxis = GetWorldAxis(gear, axis);
            Vector3 projDir = Vector3.ProjectOnPlane(worldDirection, worldAxis);
            Vector3 projRight = Vector3.ProjectOnPlane(gear.right, worldAxis);
            if (projDir.sqrMagnitude < 1e-8f || projRight.sqrMagnitude < 1e-8f) return 0f;
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
            if (debugDraw) GearConstraintDebugger.Draw(this);
        }
    }
}
