using UnityEngine;

namespace MHZE.GearSystem
{
    public enum GearAxis { X, Y, Z }

    [AddComponentMenu("Mechanical/Gear Constraint")]
    public class GearConstraint : MonoBehaviour
    {
        [SerializeField] private GearAxis m_Axis = GearAxis.Y;
        [SerializeField] private GearConstraint[] m_Dependencies;

        public GearAxis axis
        {
            get => m_Axis;
            set => m_Axis = value;
        }

        public GearConstraint[] dependencies
        {
            get => m_Dependencies;
            set => m_Dependencies = value;
        }
    }
}
