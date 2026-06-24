using UnityEngine;

namespace MHZE.GearSystem
{
    public enum GearAxis { X, Y, Z }

    [AddComponentMenu("Mechanical/Gear Constraint")]
    public class GearConstraint : MonoBehaviour
    {
        [SerializeField] private GearAxis m_Axis = GearAxis.Y;
        [SerializeField] private float m_RadiusA = 0.5f;
        [SerializeField] private float m_RadiusB = 0.5f;
        [SerializeField] private float m_ToothDensity = 5f;
        [SerializeField] private float m_ToothHeight = 0.1f;
        [SerializeField] private bool m_DebugDraw;
        [SerializeField] private bool m_IsDriver;
        [SerializeField] private Rigidbody m_GearA;
        [SerializeField] private Rigidbody m_GearB;

        public GearAxis axis
        {
            get => m_Axis;
            set => m_Axis = value;
        }

        public float radiusA
        {
            get => m_RadiusA;
            set => m_RadiusA = Mathf.Max(0.001f, value);
        }

        public float radiusB
        {
            get => m_RadiusB;
            set => m_RadiusB = Mathf.Max(0.001f, value);
        }

        public float toothDensity
        {
            get => m_ToothDensity;
            set => m_ToothDensity = Mathf.Max(0.1f, value);
        }

        public int GetToothCount(float radius) => Mathf.Max(3, Mathf.RoundToInt(2f * Mathf.PI * radius * m_ToothDensity));

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

        public bool isDriver
        {
            get => m_IsDriver;
            set => m_IsDriver = value;
        }

        public Rigidbody gearA
        {
            get => m_GearA;
            set => m_GearA = value;
        }

        public Rigidbody gearB
        {
            get => m_GearB;
            set => m_GearB = value;
        }

        public Vector3 GetAxisVector()
        {
            return m_Axis switch
            {
                GearAxis.X => Vector3.right,
                GearAxis.Z => Vector3.forward,
                _ => Vector3.up
            };
        }

        private void OnDrawGizmos()
        {
            if (!m_DebugDraw) return;
            GearConstraintDebugger.Draw(this);
        }
    }
}
