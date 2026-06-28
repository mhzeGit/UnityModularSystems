using System.Collections.Generic;
using UnityEngine;

namespace MHZE.GearSystem
{
    [AddComponentMenu("Mechanical/Gear Item")]
    public class GearItem : MonoBehaviour
    {
        [Header("Gear")]
        [SerializeField] private Transform m_GearTransform;
        [SerializeField] private float m_Radius = 0.5f;
        [SerializeField] private GearAxis m_Axis = GearAxis.Y;
        [SerializeField] private float m_ToothDensity = 5f;
        [SerializeField] private float m_ToothHeight = 0.1f;
        [SerializeField] private float m_MaxTorque;

        private Collider m_Collider;
        private readonly Dictionary<GearItem, GameObject> m_Constraints = new Dictionary<GearItem, GameObject>();

        public float radius { get => m_Radius; set => m_Radius = value; }
        public GearAxis axis { get => m_Axis; set => m_Axis = value; }
        public float toothDensity { get => m_ToothDensity; set => m_ToothDensity = value; }
        public float toothHeight { get => m_ToothHeight; set => m_ToothHeight = value; }
        public float maxTorque { get => m_MaxTorque; set => m_MaxTorque = value; }
        public Transform gearTransform { get => m_GearTransform; set => m_GearTransform = value; }

        private void Awake()
        {
            if (m_GearTransform == null)
                m_GearTransform = transform;
            if (!TryGetComponent(out m_Collider))
                m_Collider = gameObject.AddComponent<BoxCollider>();
            m_Collider.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent(out GearItem otherGear)) return;
            if (otherGear == this) return;
            if (GetInstanceID() >= otherGear.GetInstanceID()) return;
            if (m_Constraints.ContainsKey(otherGear)) return;

            GameObject constraintGO = new GameObject($"GearConstraint_{name}_{otherGear.name}");
            constraintGO.transform.SetParent(null);

            GearConstraint constraint = constraintGO.AddComponent<GearConstraint>();
            constraint.gearA = m_GearTransform;
            constraint.gearB = otherGear.m_GearTransform;
            constraint.radiusA = m_Radius;
            constraint.radiusB = otherGear.m_Radius;
            constraint.axisA = m_Axis;
            constraint.axisB = otherGear.m_Axis;
            constraint.toothDensity = Mathf.Max(m_ToothDensity, otherGear.m_ToothDensity);
            constraint.toothHeight = Mathf.Max(m_ToothHeight, otherGear.m_ToothHeight);
            constraint.maxTorque = Mathf.Min(m_MaxTorque, otherGear.m_MaxTorque);

            m_Constraints[otherGear] = constraintGO;
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.TryGetComponent(out GearItem otherGear)) return;

            if (m_Constraints.TryGetValue(otherGear, out GameObject constraintGO))
            {
                m_Constraints.Remove(otherGear);
                Destroy(constraintGO);
            }
        }

        private void OnDestroy()
        {
            foreach (GameObject go in m_Constraints.Values)
            {
                if (go != null)
                    Destroy(go);
            }
            m_Constraints.Clear();
        }
    }
}
