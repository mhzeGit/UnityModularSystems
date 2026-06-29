using UnityEngine;

namespace MHZE.GearSystem
{
    public static class GearConstraintDebugger
    {
        public static void Draw(GearConstraint gear)
        {
            if (gear.gearA != null && gear.gearB != null)
            {
                DrawContactPoint(gear);
            }
        }

        private static void DrawContactPoint(GearConstraint gear)
        {
            if (gear.gearA == null || gear.gearB == null) return;

            Vector3 posA = gear.gearA.position;
            Vector3 posB = gear.gearB.position;
            Vector3 dir = (posB - posA).normalized;
            float distance = Vector3.Distance(posA, posB);

            float rA = gear.radiusA;
            float rB = gear.radiusB;
            float totalRadius = rA + rB;

            Vector3 contactA = posA + dir * rA;
            Vector3 contactB = posB - dir * rB;

            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
            Gizmos.DrawLine(posA, contactA);
            Gizmos.DrawLine(posB, contactB);

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.9f);
            Gizmos.DrawLine(contactA, contactB);

            float ratio = totalRadius > 0f ? rA / totalRadius : 0.5f;
            Vector3 contactPoint = posA + dir * (distance * ratio);

            float sphereRadius = Mathf.Min(rA, rB) * 0.2f;
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.8f);
            Gizmos.DrawSphere(contactPoint, sphereRadius);
        }
    }
}
