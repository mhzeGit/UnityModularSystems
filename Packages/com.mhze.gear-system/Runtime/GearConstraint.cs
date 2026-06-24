using UnityEngine;

namespace MHZE.GearSystem
{
    public enum GearAxis { X, Y, Z }

    [AddComponentMenu("Mechanical/Gear Constraint")]
    public class GearConstraint : MonoBehaviour
    {
        [SerializeField] private GearAxis m_Axis = GearAxis.Y;
        [SerializeField] private float m_Radius = 0.5f;
        [SerializeField] [Range(3, 256)] private int m_ToothCount = 16;
        [SerializeField] private float m_ToothHeight = 0.1f;
        [SerializeField] private bool m_DebugDraw;
        [SerializeField] private GearConstraint[] m_Dependencies;

        public GearAxis axis
        {
            get => m_Axis;
            set => m_Axis = value;
        }

        public float radius
        {
            get => m_Radius;
            set => m_Radius = Mathf.Max(0.001f, value);
        }

        public int toothCount
        {
            get => m_ToothCount;
            set => m_ToothCount = Mathf.Clamp(value, 3, 256);
        }

        public float toothHeight
        {
            get => m_ToothHeight;
            set => m_ToothHeight = Mathf.Max(0f, value);
        }

        public bool debugDraw
        {
            get => m_DebugDraw;
            set => m_DebugDraw = value;
        }

        public GearConstraint[] dependencies
        {
            get => m_Dependencies;
            set => m_Dependencies = value;
        }

        private void OnDrawGizmos()
        {
            if (!m_DebugDraw) return;
            GearConstraintDebugger.Draw(this);
        }
    }
}
