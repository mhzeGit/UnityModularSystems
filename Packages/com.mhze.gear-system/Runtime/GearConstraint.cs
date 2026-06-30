using UnityEngine;

namespace MHZE.GearSystem
{
    [AddComponentMenu("Mechanical/Gear Constraint")]
    public class GearConstraint : GearConstraintBase
    {
        private float m_AppliedB;
        private float m_OffsetOrbit;
        private float m_LastDirAngle;

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

            float halfTooth = toothWidth * 0.5f;
            if (meshA != null)
                meshA.localRotation = Quaternion.AngleAxis(halfTooth, GetLocalAxis(axisA));
            if (meshB != null)
                meshB.localRotation = Quaternion.AngleAxis(-halfTooth, GetLocalAxis(axisB));
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
    }
}
