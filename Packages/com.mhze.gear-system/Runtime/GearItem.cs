using System.Collections.Generic;
using UnityEngine;

namespace MHZE.GearSystem
{
    [System.Serializable]
    public struct GearColliderEntry
    {
        public GameObject colliderObject;
        public float radius;
    }

    [AddComponentMenu("Mechanical/Gear Item")]
    public class GearItem : MonoBehaviour
    {
        [Header("Gear")]
        [SerializeField] private Transform m_GearTransform;
        [SerializeField] private GearAxis m_Axis = GearAxis.Y;
        [SerializeField] private float m_ToothDensity = 5f;
        [SerializeField] private float m_ToothHeight = 0.1f;
        [SerializeField] private float m_MaxTorque;

        [Header("Collider Radii")]
        [SerializeField] private List<GearColliderEntry> m_ColliderRadii = new List<GearColliderEntry>();

        private const float DefaultRadius = 0.5f;

        private Collider m_Collider;
        private readonly Dictionary<GearItem, GameObject> m_Constraints = new Dictionary<GearItem, GameObject>();
        private readonly Dictionary<GearItem, float> m_PendingRadiusOverrides = new Dictionary<GearItem, float>();

        public GearAxis axis { get => m_Axis; set => m_Axis = value; }
        public float toothDensity { get => m_ToothDensity; set => m_ToothDensity = value; }
        public float toothHeight { get => m_ToothHeight; set => m_ToothHeight = value; }
        public float maxTorque { get => m_MaxTorque; set => m_MaxTorque = value; }
        public Transform gearTransform { get => m_GearTransform; set => m_GearTransform = value; }

        private void Awake()
        {
            if (m_GearTransform == null)
                m_GearTransform = transform;

            for (int i = 0; i < m_ColliderRadii.Count; i++)
            {
                var entry = m_ColliderRadii[i];
                if (entry.colliderObject != null)
                {
                    if (entry.colliderObject.TryGetComponent(out Collider col))
                        col.isTrigger = true;

                    var proxy = entry.colliderObject.GetComponent<GearColliderProxy>();
                    if (proxy == null)
                        proxy = entry.colliderObject.AddComponent<GearColliderProxy>();
                    proxy.gearItem = this;
                    proxy.entryIndex = i;
                }
            }

            if (m_ColliderRadii.Count == 0 && !TryGetComponent(out m_Collider))
            {
                m_Collider = gameObject.AddComponent<BoxCollider>();
                m_Collider.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            GearItem otherGear = other.GetComponentInParent<GearItem>();
            if (otherGear == null) return;
            if (otherGear == this) return;
            if (GetInstanceID() >= otherGear.GetInstanceID()) return;
            if (m_Constraints.ContainsKey(otherGear)) return;

            if (!m_PendingRadiusOverrides.TryGetValue(otherGear, out float thisRadius))
                thisRadius = DefaultRadius;
            m_PendingRadiusOverrides.Remove(otherGear);

            if (!otherGear.m_PendingRadiusOverrides.TryGetValue(this, out float otherRadius))
                otherRadius = DefaultRadius;
            otherGear.m_PendingRadiusOverrides.Remove(this);

            GameObject constraintGO = new GameObject($"GearConstraint_{name}_{otherGear.name}");
            constraintGO.transform.SetParent(null);

            GearConstraint constraint = constraintGO.AddComponent<GearConstraint>();
            constraint.gearA = m_GearTransform;
            constraint.gearB = otherGear.m_GearTransform;
            constraint.radiusA = thisRadius;
            constraint.radiusB = otherRadius;
            constraint.axisA = m_Axis;
            constraint.axisB = otherGear.m_Axis;
            constraint.toothDensity = Mathf.Max(m_ToothDensity, otherGear.m_ToothDensity);
            constraint.toothHeight = Mathf.Max(m_ToothHeight, otherGear.m_ToothHeight);
            constraint.maxTorque = Mathf.Min(m_MaxTorque, otherGear.m_MaxTorque);

            m_Constraints[otherGear] = constraintGO;
        }

        private void OnTriggerExit(Collider other)
        {
            GearItem otherGear = other.GetComponentInParent<GearItem>();
            if (otherGear == null) return;

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

        internal void OnChildTriggerEnter(int entryIndex, Collider other)
        {
            GearItem otherGear = other.GetComponentInParent<GearItem>();
            if (otherGear == null || otherGear == this) return;

            float entryRadius = m_ColliderRadii[entryIndex].radius;

            if (m_Constraints.TryGetValue(otherGear, out GameObject constraintGO))
            {
                var constraint = constraintGO.GetComponent<GearConstraint>();
                if (constraint != null)
                    constraint.radiusA = entryRadius;
            }
            else
            {
                m_PendingRadiusOverrides[otherGear] = entryRadius;
            }
        }
    }

    public class GearColliderProxy : MonoBehaviour
    {
        [HideInInspector] public GearItem gearItem;
        [HideInInspector] public int entryIndex = -1;

        private void OnTriggerEnter(Collider other)
        {
            if (gearItem != null && entryIndex >= 0)
                gearItem.OnChildTriggerEnter(entryIndex, other);
        }
    }
}
