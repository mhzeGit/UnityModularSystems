using UnityEditor;
using UnityEngine;

namespace MHZE.GearSystem.Editor
{
    [InitializeOnLoad]
    internal static class GearViewportVisualDebugger
    {
        static GearViewportVisualDebugger()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView view)
        {
            var constraints = Object.FindObjectsByType<GearConstraint>(FindObjectsSortMode.None);
            foreach (var constraint in constraints)
            {
                if (!constraint.debugDraw) continue;

                Transform mA = constraint.meshA != null ? constraint.meshA : constraint.gearA;
                Transform mB = constraint.meshB != null ? constraint.meshB : constraint.gearB;

                OverlapInfo[] overlaps = constraint.debugShowOverlaps ? constraint.GetOverlaps() : null;
                OverlapInfo activeOv = constraint.HasActiveOverlap ? constraint.ActiveOverlap : default;

                float sphereOffsetA = constraint.sphereRadiusOffsetA * constraint.toothHeight;
                float sphereOffsetB = constraint.sphereRadiusOffsetB * constraint.toothHeight;

                DrawGear(mA, constraint.radiusA, constraint.axisA,
                    constraint.toothCountA, constraint.toothHeight, constraint.toothWidth,
                    constraint.debugColorA, constraint.overlapSphereRadius, true, sphereOffsetA,
                    overlaps, constraint.HasActiveOverlap, activeOv);

                DrawGear(mB, constraint.radiusB, constraint.axisB,
                    constraint.toothCountB, constraint.toothHeight, constraint.toothWidth,
                    constraint.debugColorB, constraint.overlapSphereRadius, false, sphereOffsetB,
                    overlaps, constraint.HasActiveOverlap, activeOv);

                if (overlaps != null && overlaps.Length > 0)
                {
                    foreach (var ov in overlaps)
                    {
                        bool isActive = constraint.HasActiveOverlap &&
                            ov.IsSamePair(constraint.ActiveOverlap);
                        Handles.color = isActive ? Color.yellow : new Color(1f, 0.7f, 0f);
                        Handles.DrawLine(ov.pointA, ov.pointB);
                    }
                }
            }
        }

        private static void DrawGear(Transform gearTransform, float radius, GearAxis axis,
            float toothCount, float toothHeight, float toothWidth, Color color, float sphereRadius, bool sphereOnTeeth,
            float sphereRadiusOffset, OverlapInfo[] overlaps, bool hasActiveOverlap, OverlapInfo activeOverlap)
        {
            if (gearTransform == null || radius <= 0f || toothCount <= 0f) return;

            Vector3 center = gearTransform.position;
            Vector3 normal = AxisToVector(axis, gearTransform);
            Vector3 tangent = Vector3.ProjectOnPlane(gearTransform.right, normal).normalized;
            if (tangent.sqrMagnitude < 0.001f)
                tangent = Vector3.ProjectOnPlane(gearTransform.forward, normal).normalized;

            float outerRadius = radius + toothHeight;
            float angleStep = 360f / toothCount;
            float halfWidth = toothWidth * 0.5f;
            float offsetAngle = (toothWidth / radius) * Mathf.Rad2Deg;
            int toothCountInt = Mathf.Max(1, Mathf.RoundToInt(toothCount));

            Color fillColor = color;
            fillColor.a = 0.15f;

            // Filled gear body
            Handles.color = fillColor;
            Handles.DrawSolidDisc(center, normal, radius);

            // Tooth fills and outlines
            Handles.color = color;

            for (int i = 0; i < toothCountInt; i++)
            {
                Vector3 dir = Quaternion.AngleAxis(i * angleStep + offsetAngle, normal) * tangent;
                Vector3 tan = Vector3.Cross(normal, dir).normalized;

                Vector3 innerC = center + dir * radius;
                Vector3 outerC = center + dir * (radius + toothHeight);

                Vector3 innerL = innerC - tan * halfWidth;
                Vector3 innerR = innerC + tan * halfWidth;
                Vector3 outerL = outerC - tan * halfWidth;
                Vector3 outerR = outerC + tan * halfWidth;

                // Filled tooth
                Handles.color = fillColor;
                Handles.DrawAAConvexPolygon(innerL, innerR, outerR, outerL);

                // Tooth outline
                Handles.color = color;
                Handles.DrawLine(innerL, innerR);
                Handles.DrawLine(innerR, outerR);
                Handles.DrawLine(outerR, outerL);
                Handles.DrawLine(outerL, innerL);
            }

            // Radius circle outline
            Handles.color = color;
            Handles.DrawWireArc(center, normal, tangent, 360f, radius);

            // Spheres (wireframe, sphere-collider style)
            float sphereOffset = sphereOnTeeth ? 0f : angleStep * 0.5f;

            for (int i = 0; i < toothCountInt; i++)
            {
                Vector3 dir = Quaternion.AngleAxis(i * angleStep + offsetAngle + sphereOffset, normal) * tangent;
                Vector3 pos = center + dir * (radius + sphereRadiusOffset);

                bool overlapping = false;
                bool isActive = false;
                if (overlaps != null)
                {
                    foreach (var ov in overlaps)
                    {
                        if (sphereOnTeeth ? ov.toothIndexA == i : ov.toothIndexB == i)
                        {
                            overlapping = true;
                            if (hasActiveOverlap && ov.IsSamePair(activeOverlap))
                                isActive = true;
                            break;
                        }
                    }
                }

                if (isActive)
                    Handles.color = Color.yellow;
                else if (overlapping)
                    Handles.color = new Color(1f, 0.7f, 0f);
                else
                    Handles.color = color;
                DrawWireSphere(pos, sphereRadius);
            }
        }

        private static void DrawWireSphere(Vector3 center, float radius)
        {
            Handles.DrawWireDisc(center, Vector3.up, radius);
            Handles.DrawWireDisc(center, Vector3.right, radius);
            Handles.DrawWireDisc(center, Vector3.forward, radius);
        }

        private static Vector3 AxisToVector(GearAxis axis, Transform transform)
        {
            switch (axis)
            {
                case GearAxis.X: return transform.right;
                case GearAxis.Y: return transform.up;
                case GearAxis.Z: return transform.forward;
                default: return transform.up;
            }
        }
    }
}
