using UnityEngine;

namespace MHZE.GearSystem
{
    public static class GearConstraintDebugger
    {
        public static void Draw(GearConstraint gear)
        {
            Transform xfA = GetGearTransform(gear.gearA, gear.meshA);
            Transform xfB = GetGearTransform(gear.gearB, gear.meshB);

            if (xfA != null)
                GearConstraint.DrawGearGizmo(xfA, gear.meshA, gear.radiusA, gear.axisA, 0f, gear.toothDensity, gear.toothHeight);
            if (xfB != null)
                GearConstraint.DrawGearGizmo(xfB, gear.meshB, gear.radiusB, gear.axisB, 0f, gear.toothDensity, gear.toothHeight);

            if (gear.gearA != null && gear.gearB != null)
            {
                DrawConnectionLine(gear);
            }
        }

        private static void DrawConnectionLine(GearConstraint gear)
        {
            Transform xfA = GetGearTransform(gear.gearA, gear.meshA);
            Transform xfB = GetGearTransform(gear.gearB, gear.meshB);
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

        private static Transform GetGearTransform(Transform gearTransform, Transform meshTransform)
        {
            if (gearTransform == null) return null;

            if (meshTransform != null)
                return meshTransform;

            MeshFilter meshFilter = gearTransform.GetComponentInChildren<MeshFilter>();
            return meshFilter != null ? meshFilter.transform : gearTransform;
        }
    }
}
