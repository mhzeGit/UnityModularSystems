using UnityEngine;

namespace MHZE.GearSystem
{
    public static class GearConstraintDebugger
    {
        public static void Draw(GearConstraint gear)
        {
            DrawGear(gear);
            DrawDependencies(gear);
        }

        private static void DrawGear(GearConstraint gear)
        {
            if (gear.radius <= 0f) return;

            GetAxes(gear.axis, out var axis, out var b1, out var b2);

            float outerRadius = gear.radius + gear.toothHeight;
            float halfDepth = 0.05f;
            int segments = Mathf.Clamp(gear.toothCount * 2, 16, 128);

            Vector3 topCenter = axis * halfDepth;
            Vector3 botCenter = -axis * halfDepth;

            Color bodyColor = new Color(0.3f, 0.6f, 1f, 0.8f);
            Color toothColor = new Color(0.2f, 0.5f, 0.9f, 0.8f);

            var prevMatrix = Gizmos.matrix;
            Gizmos.matrix = gear.transform.localToWorldMatrix;

            // Draw pitch circle (gear body)
            Gizmos.color = bodyColor;
            DrawCircle(topCenter, b1, b2, gear.radius, segments);
            DrawCircle(botCenter, b1, b2, gear.radius, segments);

            // Vertical lines along the body
            for (int i = 0; i < segments; i += 4)
            {
                float angle = 2f * Mathf.PI * i / segments;
                Vector3 dir = b1 * Mathf.Cos(angle) + b2 * Mathf.Sin(angle);
                Gizmos.DrawLine(topCenter + dir * gear.radius, botCenter + dir * gear.radius);
            }

            // Draw individual teeth with gaps
            Gizmos.color = toothColor;
            float toothHalfWidth = Mathf.PI / gear.toothCount * 0.35f;

            for (int i = 0; i < gear.toothCount; i++)
            {
                float angle = 2f * Mathf.PI * i / gear.toothCount;

                Vector3 dirLeft = b1 * Mathf.Cos(angle - toothHalfWidth) + b2 * Mathf.Sin(angle - toothHalfWidth);
                Vector3 dirRight = b1 * Mathf.Cos(angle + toothHalfWidth) + b2 * Mathf.Sin(angle + toothHalfWidth);

                Vector3 tlInner = topCenter + dirLeft * gear.radius;
                Vector3 tlOuter = topCenter + dirLeft * outerRadius;
                Vector3 trInner = topCenter + dirRight * gear.radius;
                Vector3 trOuter = topCenter + dirRight * outerRadius;

                Vector3 blInner = botCenter + dirLeft * gear.radius;
                Vector3 blOuter = botCenter + dirLeft * outerRadius;
                Vector3 brInner = botCenter + dirRight * gear.radius;
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

            // Axis indicator
            Gizmos.color = Color.red;
            Gizmos.DrawLine(-axis * halfDepth * 2, axis * halfDepth * 2);

            // Hub circle
            Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            float hubRadius = gear.radius * 0.25f;
            DrawCircle(topCenter, b1, b2, hubRadius, segments);
            DrawCircle(botCenter, b1, b2, hubRadius, segments);

            Gizmos.matrix = prevMatrix;
        }

        private static void DrawDependencies(GearConstraint gear)
        {
            var dependencies = gear.dependencies;
            if (dependencies == null) return;

            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
            foreach (var dep in dependencies)
            {
                if (dep == null) continue;
                Gizmos.DrawLine(gear.transform.position, dep.transform.position);
            }
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
