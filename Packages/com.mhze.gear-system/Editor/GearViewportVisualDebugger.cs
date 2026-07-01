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

                DrawGear(mA, constraint.radiusA, constraint.axisA,
                    constraint.toothCountA, constraint.toothHeight, constraint.toothWidth, Color.cyan);

                DrawGear(mB, constraint.radiusB, constraint.axisB,
                    constraint.toothCountB, constraint.toothHeight, constraint.toothWidth, Color.magenta);
            }
        }

        private static void DrawGear(Transform gearTransform, float radius, GearAxis axis,
            float toothCount, float toothHeight, float toothWidth, Color color)
        {
            if (gearTransform == null || radius <= 0f || toothCount <= 0f) return;

            Vector3 center = gearTransform.position;
            Vector3 normal = AxisToVector(axis, gearTransform);
            Vector3 tangent = Vector3.ProjectOnPlane(gearTransform.right, normal).normalized;
            if (tangent.sqrMagnitude < 0.001f)
                tangent = Vector3.ProjectOnPlane(gearTransform.forward, normal).normalized;

            float outerRadius = radius + toothHeight;

            // Radius circle
            Handles.color = color;
            Handles.DrawWireArc(center, normal, tangent, 360f, radius);

            // Tooth profiles
            Handles.color = color;
            float angleStep = 360f / toothCount;
            float halfWidth = toothWidth * 0.5f;
            float offsetAngle = (toothWidth / radius) * Mathf.Rad2Deg;
            int toothCountInt = Mathf.Max(1, Mathf.RoundToInt(toothCount));

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

                Handles.DrawLine(innerL, innerR);
                Handles.DrawLine(innerR, outerR);
                Handles.DrawLine(outerR, outerL);
                Handles.DrawLine(outerL, innerL);
            }
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
