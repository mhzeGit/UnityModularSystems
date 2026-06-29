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
                DrawArcLength(gear);
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

            Vector3 contactPoint = posA + dir * rA;

            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
            Gizmos.DrawLine(posA, contactPoint);
            Gizmos.DrawLine(posB, posB - dir * rB);

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
            Gizmos.DrawLine(contactPoint, posB - dir * rB);

            float sphereRadius = Mathf.Min(rA, rB) * 0.15f;
            Gizmos.color = new Color(1f, 0.3f, 0f, 0.9f);
            Gizmos.DrawSphere(contactPoint, sphereRadius * 2f);

            Gizmos.color = new Color(0f, 0.8f, 1f, 0.5f);
            Gizmos.DrawWireSphere(contactPoint, sphereRadius * 2f);
        }

        private static void DrawArcLength(GearConstraint gear)
        {
            if (gear.gearA == null || gear.radiusA <= 0f) return;

            float arcLen = gear.arcLength;
            if (Mathf.Abs(arcLen) < 0.001f) return;

            Vector3 center = gear.gearA.position;
            Vector3 axis = gear.axisA switch
            {
                GearAxis.X => gear.gearA.right,
                GearAxis.Z => gear.gearA.forward,
                _ => gear.gearA.up
            };
            float radius = gear.radiusA;

            float angleDeg = (arcLen / radius) * Mathf.Rad2Deg % 360f;

            Vector3 b1, b2;
            if (Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.99f)
            { b1 = Vector3.right; b2 = Vector3.forward; }
            else
            { b1 = Vector3.Cross(axis, Vector3.up).normalized; b2 = Vector3.Cross(axis, b1).normalized; }

            int segments = Mathf.Max(4, Mathf.RoundToInt(Mathf.Abs(angleDeg) / 10f));
            float sign = Mathf.Sign(angleDeg);
            Vector3 arcStart = center + b1 * radius;

            Gizmos.color = new Color(0f, 1f, 0.4f, 0.7f);
            for (int i = 1; i <= segments; i++)
            {
                float t = (float)i / segments;
                float a = sign * t * Mathf.Abs(angleDeg) * Mathf.Deg2Rad;
                Vector3 dir = b1 * Mathf.Cos(a) + b2 * Mathf.Sin(a);
                Vector3 p = center + dir * radius;
                Gizmos.DrawLine(arcStart, p);
                arcStart = p;
            }
        }
    }
}
