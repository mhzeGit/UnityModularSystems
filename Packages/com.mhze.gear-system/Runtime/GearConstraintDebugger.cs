using UnityEngine;

namespace MHZE.GearSystem
{
    public static class GearConstraintDebugger
    {
        public static void Draw(GearConstraint gear)
        {
            if (gear.gearA != null)
                GearConstraint.DrawGearGizmo(gear.gearA, gear.radiusA, gear.axisA, 0f, gear.toothDensity, gear.toothHeight);
            if (gear.gearB != null)
                GearConstraint.DrawGearGizmo(gear.gearB, gear.radiusB, gear.axisB, 0f, gear.toothDensity, gear.toothHeight);

            if (gear.gearA != null && gear.gearB != null)
            {
                DrawConnectionLine(gear);
            }
        }

        private static void DrawConnectionLine(GearConstraint gear)
        {
            if (gear.gearA == null || gear.gearB == null) return;

            Vector3 posA = gear.gearA.position;
            Vector3 posB = gear.gearB.position;
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


    }
}
