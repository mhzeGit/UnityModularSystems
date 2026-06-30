using UnityEngine;

namespace MHZE.GearSystem
{
    public enum GearAxis { X, Y, Z }

    public abstract class GearConstraintBase : MonoBehaviour
    {
        [Header("Gear A")]
        public Transform gearA;
        public Transform meshA;
        public float radiusA = 0.5f;
        public GearAxis axisA = GearAxis.Y;

        [Header("Gear B")]
        public Transform gearB;
        public Transform meshB;
        public float radiusB = 0.5f;
        public GearAxis axisB = GearAxis.Y;

        [Header("Visual")]
        public float toothDensity = 5f;
        public float toothHeight = 0.1f;
        [Tooltip("Angular width of one tooth (degrees). Used for mesh offset alignment.")]
        public float toothWidth = 36f;
        [Tooltip("Angular offset for gear mesh alignment (degrees).")]
        public float meshOffset;
        public bool debugDraw;
        [Tooltip("Log debug values to console when enabled.")]
        public bool debugLog;

        [Tooltip("Invert gear B rotation direction.")]
        public bool reverseB;

        public float arcLength
        {
            get
            {
                if (gearA == null || gearB == null) return 0f;
                if (radiusA <= 0f) return 0f;
                Vector3 dir = (gearB.position - gearA.position).normalized;
                return GetContactAngle(gearA, axisA, dir) * Mathf.Deg2Rad * radiusA;
            }
        }

        private void OnDrawGizmos()
        {
            if (debugDraw) GearConstraintDebugger.Draw(this);
        }

        protected static float GetContactAngle(Transform gear, GearAxis axis, Vector3 worldDirection)
        {
            Vector3 worldAxis = GetWorldAxis(gear, axis);
            Vector3 projDir = Vector3.ProjectOnPlane(worldDirection, worldAxis);
            Vector3 projRight = Vector3.ProjectOnPlane(gear.right, worldAxis);
            if (projDir.sqrMagnitude < 1e-8f || projRight.sqrMagnitude < 1e-8f) return 0f;
            return Vector3.SignedAngle(projRight.normalized, projDir.normalized, worldAxis);
        }

        protected static float GetDirectionAngle(Vector3 worldDirection, Vector3 worldAxis)
        {
            Vector3 projDir = Vector3.ProjectOnPlane(worldDirection, worldAxis);
            if (projDir.sqrMagnitude < 1e-8f) return 0f;
            Vector3 projRef = Vector3.ProjectOnPlane(Vector3.right, worldAxis);
            if (projRef.sqrMagnitude < 1e-8f)
                projRef = Vector3.ProjectOnPlane(Vector3.forward, worldAxis);
            return Vector3.SignedAngle(projRef.normalized, projDir.normalized, worldAxis);
        }

        public static Vector3 GetWorldAxis(Transform t, GearAxis axis)
        {
            return axis switch
            {
                GearAxis.X => t.right,
                GearAxis.Z => t.forward,
                _ => t.up
            };
        }

        public static Vector3 GetLocalAxis(GearAxis axis)
        {
            return axis switch
            {
                GearAxis.X => Vector3.right,
                GearAxis.Z => Vector3.forward,
                _ => Vector3.up
            };
        }
    }
}
