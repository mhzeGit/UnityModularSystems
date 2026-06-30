using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MHZE.GearSystem.Editor
{
    [InitializeOnLoad]
    public static class GearShapeGizmoDrawer
    {
        static GearShapeGizmoDrawer()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            GearConstraintBase[] gears = Object.FindObjectsByType<GearConstraintBase>(FindObjectsSortMode.None);

            Handles.zTest = CompareFunction.Always;

            foreach (GearConstraintBase gear in gears)
            {
                if (!gear.debugDraw || !gear.enabled) continue;

                if (gear.gearA != null)
                    DrawGearShape(gear.gearA.position,
                        GearConstraintBase.GetWorldAxis(gear.gearA, gear.axisA),
                        gear.radiusA, gear.toothHeight, gear.toothDensity, gear.toothWidth);

                if (gear.gearB != null)
                    DrawGearShape(gear.gearB.position,
                        GearConstraintBase.GetWorldAxis(gear.gearB, gear.axisB),
                        gear.radiusB, gear.toothHeight, gear.toothDensity, gear.toothWidth);
            }
        }

        private static void GetGearPlaneBasis(Vector3 axis, out Vector3 b1, out Vector3 b2)
        {
            if (Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.99f)
            { b1 = Vector3.right; b2 = Vector3.forward; }
            else
            { b1 = Vector3.Cross(axis, Vector3.up).normalized; b2 = Vector3.Cross(axis, b1).normalized; }
        }

        private static void DrawGearShape(Vector3 center, Vector3 axis, float radius, float toothHeight, float toothDensity, float toothWidth)
        {
            if (radius <= 0f || toothDensity <= 0f) return;

            GetGearPlaneBasis(axis, out Vector3 b1, out Vector3 b2);

            int numTeeth = Mathf.Max(4, Mathf.RoundToInt(toothDensity));
            float angleStep = 360f / numTeeth;

            Color color = new Color(0.3f, 0.6f, 1f, 0.7f);

            // center indicator
            Handles.color = color;
            float cs = radius * 0.06f;
            Handles.DrawLine(center - b1 * cs, center + b1 * cs);
            Handles.DrawLine(center - b2 * cs, center + b2 * cs);

            // gear body circle
            Handles.color = new Color(0.3f, 0.6f, 1f, 0.4f);
            int circleSegs = Mathf.Max(24, numTeeth * 2);
            Vector3 prev = center + b1 * radius;
            for (int i = 1; i <= circleSegs; i++)
            {
                float a = (float)i / circleSegs * Mathf.PI * 2f;
                Vector3 p = center + (b1 * Mathf.Cos(a) + b2 * Mathf.Sin(a)) * radius;
                Handles.DrawLine(prev, p);
                prev = p;
            }

            // teeth
            float halfToothAngle = toothWidth * 0.5f * Mathf.Deg2Rad;
            float outerRadius = radius + toothHeight;
            Handles.color = new Color(0.3f, 0.6f, 1f, 0.75f);
            for (int i = 0; i < numTeeth; i++)
            {
                float centerAngle = i * angleStep * Mathf.Deg2Rad;
                float lAngle = centerAngle - halfToothAngle;
                float rAngle = centerAngle + halfToothAngle;

                float cl = Mathf.Cos(lAngle), sl = Mathf.Sin(lAngle);
                float cr = Mathf.Cos(rAngle), sr = Mathf.Sin(rAngle);

                Vector3 lInner = center + (b1 * cl + b2 * sl) * radius;
                Vector3 rInner = center + (b1 * cr + b2 * sr) * radius;
                Vector3 lOuter = center + (b1 * cl + b2 * sl) * outerRadius;
                Vector3 rOuter = center + (b1 * cr + b2 * sr) * outerRadius;

                Handles.DrawLine(lInner, lOuter);
                Handles.DrawLine(rInner, rOuter);
                Handles.DrawLine(lOuter, rOuter);
            }
        }
    }
}
