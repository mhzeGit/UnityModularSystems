using UnityEngine;

namespace MHZE.GearSystem
{
    public static class GearConstraintDebugger
    {
        private static readonly Color ToothColor = new Color(0.2f, 0.9f, 0.3f, 0.7f);
        private static readonly Color GapColor = new Color(0.9f, 0.2f, 0.2f, 0.7f);

        private const float SphereRadius = 0.05f;
        private static readonly float OverlapThreshold = SphereRadius * 2f;

        public static void Draw(GearConstraintBase gear)
        {
            if (gear.gearA != null && gear.gearB != null)
            {
                DrawContactPoint(gear);
                DrawArcLength(gear);
                var teethA = GetToothPositions(gear.gearA, gear.meshA, gear.axisA, gear.radiusA, gear.toothHeight, gear.toothCountA, false);
                var teethB = GetToothPositions(gear.gearB, gear.meshB, gear.axisB, gear.radiusB, gear.toothHeight, gear.toothCountB, true);
                DrawMarkersWithOverlapDetection(teethA, teethB, ToothColor, GapColor);
            }
        }

        private static void DrawContactPoint(GearConstraintBase gear)
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

        private static void DrawArcLength(GearConstraintBase gear)
        {
            if (gear.gearA == null || gear.radiusA <= 0f) return;

            float arcLen = gear.arcLength;
            if (Mathf.Abs(arcLen) < 0.001f) return;

            Vector3 center = gear.gearA.position;
            Vector3 axis = GearConstraintBase.GetWorldAxis(gear.gearA, gear.axisA);
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

        private static System.Collections.Generic.List<Vector3> GetToothPositions(Transform gear, Transform mesh, GearAxis axis, float radius, float toothHeight, float toothCount, bool atGaps)
        {
            var positions = new System.Collections.Generic.List<Vector3>();
            if (gear == null || radius <= 0f) return positions;

            Transform t = mesh != null ? mesh : gear;
            Vector3 worldAxis = GearConstraintBase.GetWorldAxis(t, axis);
            Vector3 refDir = Vector3.ProjectOnPlane(t.right, worldAxis);
            if (refDir.sqrMagnitude < 0.001f)
                refDir = Vector3.ProjectOnPlane(t.forward, worldAxis);

            if (refDir.sqrMagnitude < 0.001f) return positions;

            refDir = refDir.normalized * (radius + toothHeight * 0.5f);

            int numTeeth = Mathf.Max(1, Mathf.RoundToInt(toothCount));
            float stepDeg = 360f / numTeeth;
            float offset = atGaps ? 0.5f : 0f;

            for (int i = 0; i < numTeeth; i++)
            {
                float angle = (i + offset) * stepDeg;
                Quaternion rot = Quaternion.AngleAxis(angle, worldAxis);
                positions.Add(t.position + rot * refDir);
            }

            return positions;
        }

        private static void DrawMarkersWithOverlapDetection(System.Collections.Generic.List<Vector3> teethA, System.Collections.Generic.List<Vector3> teethB, Color colorA, Color colorB)
        {
            foreach (var pos in teethA)
            {
                Gizmos.color = OverlapsAny(pos, teethB) ? Color.yellow : colorA;
                Gizmos.DrawWireSphere(pos, SphereRadius);
            }

            foreach (var pos in teethB)
            {
                Gizmos.color = OverlapsAny(pos, teethA) ? Color.yellow : colorB;
                Gizmos.DrawWireSphere(pos, SphereRadius);
            }
        }

        private static bool OverlapsAny(Vector3 pos, System.Collections.Generic.List<Vector3> others)
        {
            foreach (var other in others)
            {
                if (Vector3.Distance(pos, other) < OverlapThreshold)
                    return true;
            }
            return false;
        }
    }
}
