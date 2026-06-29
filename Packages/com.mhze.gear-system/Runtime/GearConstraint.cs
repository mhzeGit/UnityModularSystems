using UnityEngine;

namespace MHZE.GearSystem
{
    public enum GearAxis { X, Y, Z }

    [AddComponentMenu("Mechanical/Gear Constraint")]
    public class GearConstraint : MonoBehaviour
    {
        [Header("Gear A")]
        public Transform gearA;
        public float radiusA = 0.5f;
        public GearAxis axisA = GearAxis.Y;

        [Header("Gear B")]
        public Transform gearB;
        public float radiusB = 0.5f;
        public GearAxis axisB = GearAxis.Y;

        [Header("Visual")]
        public float toothDensity = 5f;
        public float toothHeight = 0.1f;
        public bool debugDraw;

        [Header("Limit")]
        [Tooltip("Maximum constraint torque (Nm). 0 = unlimited.")]
        public float maxTorque = 0f;

        [Tooltip("Log debug values to console when enabled.")]
        public bool debugLog;

        private void OnDrawGizmos()
        {
            if (debugDraw)
            {
                GearConstraintDebugger.Draw(this);
            }
        }
    }
}
