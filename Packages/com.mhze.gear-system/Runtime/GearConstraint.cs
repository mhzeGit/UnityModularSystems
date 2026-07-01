using UnityEngine;

namespace MHZE.GearSystem
{
    public enum GearAxis { X, Y, Z }

    [AddComponentMenu("Mechanical/Gear Constraint")]
    public class GearConstraint : MonoBehaviour
    {
        public Transform gearA;
        public Transform meshA;
        public float radiusA = 0.5f;
        public GearAxis axisA = GearAxis.Y;
        public float toothCountA = 5f;

        public Transform gearB;
        public Transform meshB;
        public float radiusB = 0.5f;
        public GearAxis axisB = GearAxis.Y;
        public float toothCountB = 5f;

        public float toothHeight = 0.1f;
        [Tooltip("Angular width of one tooth (degrees). Used for mesh offset alignment.")]
        public float toothWidth = 36f;
        [Tooltip("Angular offset for gear mesh alignment (degrees).")]
        public float meshOffset;
        public bool debugDraw;
        [Tooltip("Log debug values to console when enabled.")]
        public bool debugLog;
    }
}
