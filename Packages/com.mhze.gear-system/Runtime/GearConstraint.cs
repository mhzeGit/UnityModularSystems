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

        private float m_AppliedB;
        private float m_OffsetOrbit;
        private float m_LastDirAngle;

        public float arcLength
        {
            get
            {
                if (gearA == null || gearB == null) return 0f;
                if (radiusA <= 0f) return 0f;
                Vector3 dir = (gearB.position - gearA.position).normalized;
                return GetContactAngle(gearA, axisA, dir) * Mathf.Deg2Rad * radiusA;
            }
        }

        private void Start()
        {
            if (gearA == null || gearB == null) return;

            Vector3 dir = (gearB.position - gearA.position).normalized;
            float startContact = GetContactAngle(gearA, axisA, dir);
            m_AppliedB = startContact * (radiusA / radiusB);
            m_LastDirAngle = GetDirectionAngle(dir, GetWorldAxis(gearA, axisA));
            m_OffsetOrbit = 0f;

            if (debugLog)
                Debug.Log($"[GearConstraint] init: contactA={startContact:F2}° appliedB={m_AppliedB:F2}°");
        }

        private void Update()
        {
            if (gearA == null || gearB == null) return;
            if (radiusA <= 0f || radiusB <= 0f) return;

            Vector3 dir = (gearB.position - gearA.position).normalized;
            Vector3 worldAxis = GetWorldAxis(gearA, axisA);

            float contactA = GetContactAngle(gearA, axisA, dir);
            float dirAngle = GetDirectionAngle(dir, worldAxis);
            m_OffsetOrbit += Mathf.DeltaAngle(m_LastDirAngle, dirAngle);

            float targetB = contactA * (radiusA / radiusB) + m_OffsetOrbit;
            if (reverseB) targetB = -targetB;

            float correction = targetB - m_AppliedB;

            if (Mathf.Abs(correction) > 0.01f)
            {
                gearB.Rotate(GetWorldAxis(gearB, axisB), correction, Space.World);
                m_AppliedB = targetB;
                m_LastDirAngle = dirAngle;
            }

            if (debugLog)
            {
                float s = contactA * Mathf.Deg2Rad * radiusA;
                Debug.Log($"[GearConstraint] s={s:F6} cA={contactA:F2}° " +
                          $"orbit={m_OffsetOrbit:F2}° " +
                          $"tB={targetB:F2}° corr={correction:F4}°");
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

        private static float GetDirectionAngle(Vector3 worldDirection, Vector3 worldAxis)
        {
            Vector3 projDir = Vector3.ProjectOnPlane(worldDirection, worldAxis);
            if (projDir.sqrMagnitude < 1e-8f) return 0f;
            Vector3 projRef = Vector3.ProjectOnPlane(Vector3.right, worldAxis);
            if (projRef.sqrMagnitude < 1e-8f)
                projRef = Vector3.ProjectOnPlane(Vector3.forward, worldAxis);
            return Vector3.SignedAngle(projRef.normalized, projDir.normalized, worldAxis);
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
