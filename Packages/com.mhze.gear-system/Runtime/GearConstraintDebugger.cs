using UnityEngine;

namespace MHZE.GearSystem
{
    public static class GearConstraintDebugger
    {
        public static void Draw(GearConstraint gear)
        {
            if (gear.gearA != null) DrawGear(gear, gear.gearA.transform, gear.radiusA);
            if (gear.gearB != null) DrawGear(gear, gear.gearB.transform, gear.radiusB);
        }

        private static void DrawGear(GearConstraint gear, Transform targetTransform, float radius)
        {
            if (radius <= 0f) return;

            GetAxes(gear.axis, out var axis, out var b1, out var b2);

            float outerRadius = radius + gear.toothHeight;
            float halfDepth = 0.05f;
            int segments = Mathf.Clamp(gear.GetToothCount(radius) * 2, 16, 128);

            Vector3 topCenter = axis * halfDepth;
            Vector3 botCenter = -axis * halfDepth;

            Color bodyColor = new Color(0.3f, 0.6f, 1f, 0.8f);
            Color toothColor = new Color(0.2f, 0.5f, 0.9f, 0.8f);

            var prevMatrix = Gizmos.matrix;
            Gizmos.matrix = targetTransform.localToWorldMatrix;

            // Draw pitch circle (gear body)
            Gizmos.color = bodyColor;
            DrawCircle(topCenter, b1, b2, radius, segments);
            DrawCircle(botCenter, b1, b2, radius, segments);

            // Vertical lines along the body
            for (int i = 0; i < segments; i += 4)
            {
                float angle = 2f * Mathf.PI * i / segments;
                Vector3 dir = b1 * Mathf.Cos(angle) + b2 * Mathf.Sin(angle);
                Gizmos.DrawLine(topCenter + dir * radius, botCenter + dir * radius);
            }

            // Draw individual teeth with gaps
            Gizmos.color = toothColor;
            int toothCount = gear.GetToothCount(radius);
            float toothHalfWidth = Mathf.PI / toothCount * 0.35f;

            for (int i = 0; i < toothCount; i++)
            {
                float angle = 2f * Mathf.PI * i / toothCount;

                Vector3 dirLeft = b1 * Mathf.Cos(angle - toothHalfWidth) + b2 * Mathf.Sin(angle - toothHalfWidth);
                Vector3 dirRight = b1 * Mathf.Cos(angle + toothHalfWidth) + b2 * Mathf.Sin(angle + toothHalfWidth);

                Vector3 tlInner = topCenter + dirLeft * radius;
                Vector3 tlOuter = topCenter + dirLeft * outerRadius;
                Vector3 trInner = topCenter + dirRight * radius;
                Vector3 trOuter = topCenter + dirRight * outerRadius;

                Vector3 blInner = botCenter + dirLeft * radius;
                Vector3 blOuter = botCenter + dirLeft * outerRadius;
                Vector3 brInner = botCenter + dirRight * radius;
                Vector3 brOuter = botCenter + dirRight * outerRadius;

                // Top face
                Gizmos.DrawLine(tlInner, tlOuter);
                Gizmos.DrawLine(tlOuter, trOuter);
                Gizmos.DrawLine(trOuter, trInner);

                // Bottom face
                Gizmos.DrawLine(blInner, blOuter);
                Gizmos.DrawLine(blOuter, brOuter);
                Gizmos.DrawLine(brOuter, brInner);

                // Side edges
                Gizmos.DrawLine(tlOuter, blOuter);
                Gizmos.DrawLine(trOuter, brOuter);
                Gizmos.DrawLine(tlInner, blInner);
                Gizmos.DrawLine(trInner, brInner);
            }
            Gizmos.matrix = prevMatrix;
        }

        private static void GetAxes(GearAxis gearAxis, out Vector3 axis, out Vector3 b1, out Vector3 b2)
        {
            switch (gearAxis)
            {
                case GearAxis.X:
                    axis = Vector3.right; b1 = Vector3.up; b2 = Vector3.forward;
                    break;
                case GearAxis.Z:
                    axis = Vector3.forward; b1 = Vector3.right; b2 = Vector3.up;
                    break;
                default:
                    axis = Vector3.up; b1 = Vector3.right; b2 = Vector3.forward;
                    break;
            }
        }

        private static void DrawCircle(Vector3 center, Vector3 b1, Vector3 b2, float radius, int segments)
        {
            Vector3 prev = center + b1 * radius;
            for (int i = 1; i <= segments; i++)
            {
                float angle = 2f * Mathf.PI * i / segments;
                Vector3 dir = b1 * Mathf.Cos(angle) + b2 * Mathf.Sin(angle);
                Vector3 curr = center + dir * radius;
                Gizmos.DrawLine(prev, curr);
                prev = curr;
            }
        }
    }
}
