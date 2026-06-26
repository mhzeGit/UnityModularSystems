using UnityEngine;

namespace MHZE.GearSystem
{
    public static class GearConstraintDebugger
    {
        public static void Draw(GearConstraint gear)
        {
            Transform xfA = GetGearTransform(gear.gearA);
            Transform xfB = GetGearTransform(gear.gearB);

            float offsetA = GetRotationOffset(xfA);
            float offsetB = GetRotationOffset(xfB);

            if (xfA != null) DrawGear(gear, xfA, gear.radiusA, gear.axisA, offsetA);
            if (xfB != null) DrawGear(gear, xfB, gear.radiusB, gear.axisB, offsetB);

            if (gear.gearA != null && gear.gearB != null)
            {
                DrawConnectionLine(gear);
            }
        }

        private static float GetRotationOffset(Transform xf)
        {
            if (xf == null) return 0f;
            GearItem item = xf.GetComponentInParent<GearItem>();
            return item != null ? item.rotationOffset : 0f;
        }

        private static void DrawConnectionLine(GearConstraint gear)
        {
            Transform xfA = GetGearTransform(gear.gearA);
            Transform xfB = GetGearTransform(gear.gearB);
            if (xfA == null || xfB == null) return;

            Vector3 posA = xfA.position;
            Vector3 posB = xfB.position;
            Vector3 dir = (posB - posA).normalized;
            float rA = gear.radiusA;
            float rB = gear.radiusB;

            Vector3 contactA = posA + dir * rA;
            Vector3 contactB = posB - dir * rB;

            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
            Gizmos.DrawLine(posA, contactA);
            Gizmos.DrawLine(posB, contactB);

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.9f);
            Gizmos.DrawLine(contactA, contactB);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(contactA, 0.03f);
            Gizmos.DrawWireSphere(contactB, 0.03f);
        }

        private static void DrawGear(GearConstraint gear, Transform targetTransform, float radius, GearAxis gearAxis, float rotationOffset)
        {
            if (radius <= 0f) return;

            GetAxes(gearAxis, out var axis, out var b1, out var b2);

            float outerRadius = radius + gear.toothHeight;
            float halfDepth = 0.05f;
            int segments = Mathf.Clamp(gear.GetToothCount(radius) * 2, 16, 128);

            Vector3 topCenter = axis * halfDepth;
            Vector3 botCenter = -axis * halfDepth;

            Color bodyColor = new Color(0.3f, 0.6f, 1f, 0.8f);
            Color toothColor = new Color(0.2f, 0.5f, 0.9f, 0.8f);

            var prevMatrix = Gizmos.matrix;
            Quaternion offsetRot = Quaternion.AngleAxis(rotationOffset, axis);
            Gizmos.matrix = targetTransform.localToWorldMatrix * Matrix4x4.Rotate(offsetRot);

            Gizmos.color = bodyColor;
            DrawCircle(topCenter, b1, b2, radius, segments);
            DrawCircle(botCenter, b1, b2, radius, segments);

            for (int i = 0; i < segments; i += 4)
            {
                float angle = 2f * Mathf.PI * i / segments;
                Vector3 dir = b1 * Mathf.Cos(angle) + b2 * Mathf.Sin(angle);
                Gizmos.DrawLine(topCenter + dir * radius, botCenter + dir * radius);
            }

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

                Gizmos.DrawLine(tlInner, tlOuter);
                Gizmos.DrawLine(tlOuter, trOuter);
                Gizmos.DrawLine(trOuter, trInner);

                Gizmos.DrawLine(blInner, blOuter);
                Gizmos.DrawLine(blOuter, brOuter);
                Gizmos.DrawLine(brOuter, brInner);

                Gizmos.DrawLine(tlOuter, blOuter);
                Gizmos.DrawLine(trOuter, brOuter);
                Gizmos.DrawLine(tlInner, blInner);
                Gizmos.DrawLine(trInner, brInner);
            }
            Gizmos.matrix = prevMatrix;
        }

        private static Transform GetGearTransform(Rigidbody rb)
        {
            if (rb == null) return null;

            GearItem item = rb.GetComponent<GearItem>();
            if (item != null && item.meshTransform != null)
                return item.meshTransform;

            MeshFilter meshFilter = rb.GetComponentInChildren<MeshFilter>();
            return meshFilter != null ? meshFilter.transform : rb.transform;
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
