using System.Collections.Generic;
using UnityEngine;

namespace MHZE.GearSystem
{
    [AddComponentMenu("Mechanical/Gear Item")]
    [RequireComponent(typeof(Rigidbody))]
    public class GearItem : MonoBehaviour
    {
        [Header("Gear")]
        [SerializeField] private float m_Radius = 0.5f;
        [SerializeField] private GearAxis m_Axis = GearAxis.Y;

        [Header("Detection")]
        [SerializeField] private float m_DetectionRadius = 2f;
        [SerializeField] private bool m_AutoConnect = true;

        private Rigidbody m_Rigidbody;
        private SphereCollider m_DetectionTrigger;
        private readonly Dictionary<GearItem, GearConstraint> m_ActiveConstraints = new();
        private bool m_HasSetupTrigger;

        public Rigidbody connectedBody => m_Rigidbody;
        public float radius
        {
            get => m_Radius;
            set => m_Radius = Mathf.Max(0.001f, value);
        }
        public GearAxis axis
        {
            get => m_Axis;
            set => m_Axis = value;
        }
        public float detectionRadius
        {
            get => m_DetectionRadius;
            set
            {
                m_DetectionRadius = Mathf.Max(0f, value);
                if (m_HasSetupTrigger && m_DetectionTrigger != null)
                    m_DetectionTrigger.radius = m_DetectionRadius;
            }
        }
        public bool autoConnect
        {
            get => m_AutoConnect;
            set => m_AutoConnect = value;
        }
        public IReadOnlyCollection<GearConstraint> activeConstraints => m_ActiveConstraints.Values;

        public event System.Action<GearItem, GearConstraint> OnGearConnected;
        public event System.Action<GearItem, GearConstraint> OnGearDisconnected;

        private void Awake()
        {
            m_Rigidbody = GetComponent<Rigidbody>();
            SetupDetectionTrigger();
        }

        private void Start()
        {
            if (m_AutoConnect)
                ScanForNearbyGears();
        }

        private void SetupDetectionTrigger()
        {
            m_DetectionTrigger = gameObject.AddComponent<SphereCollider>();
            m_DetectionTrigger.isTrigger = true;
            m_DetectionTrigger.radius = m_DetectionRadius;
            m_HasSetupTrigger = true;
        }

        private void OnValidate()
        {
            if (m_HasSetupTrigger && m_DetectionTrigger != null)
                m_DetectionTrigger.radius = m_DetectionRadius;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!m_AutoConnect) return;

            GearItem otherGear = other.GetComponentInParent<GearItem>();
            if (otherGear == null || otherGear == this) return;

            if (GetInstanceID() > otherGear.GetInstanceID()) return;
            if (m_ActiveConstraints.ContainsKey(otherGear)) return;

            CreateConstraint(otherGear);
        }

        private void OnTriggerExit(Collider other)
        {
            GearItem otherGear = other.GetComponentInParent<GearItem>();
            if (otherGear == null || otherGear == this) return;

            DestroyConstraint(otherGear);
        }

        private void OnDestroy()
        {
            List<GearItem> keys = new List<GearItem>(m_ActiveConstraints.Keys);
            foreach (GearItem other in keys)
                DestroyConstraint(other);
        }

        public GearConstraint ConnectTo(GearItem other)
        {
            if (other == null || other == this) return null;
            if (m_ActiveConstraints.ContainsKey(other)) return m_ActiveConstraints[other];

            return CreateConstraint(other);
        }

        public void DisconnectFrom(GearItem other)
        {
            if (other == null) return;
            DestroyConstraint(other);
        }

        public void DisconnectAll()
        {
            List<GearItem> keys = new List<GearItem>(m_ActiveConstraints.Keys);
            foreach (GearItem other in keys)
                DestroyConstraint(other);
        }

        private GearConstraint CreateConstraint(GearItem other)
        {
            string name = $"GearConstraint_{gameObject.name}_{other.gameObject.name}";
            GameObject go = new GameObject(name);

            GearConstraint constraint = go.AddComponent<GearConstraint>();
            constraint.gearA = m_Rigidbody;
            constraint.gearB = other.m_Rigidbody;
            constraint.radiusA = m_Radius;
            constraint.radiusB = other.m_Radius;
            constraint.axisA = m_Axis;
            constraint.axisB = other.m_Axis;

            m_ActiveConstraints[other] = constraint;
            other.m_ActiveConstraints[this] = constraint;

            OnGearConnected?.Invoke(other, constraint);
            other.OnGearConnected?.Invoke(this, constraint);

            return constraint;
        }

        private void DestroyConstraint(GearItem other)
        {
            if (!m_ActiveConstraints.TryGetValue(other, out GearConstraint constraint))
                return;

            m_ActiveConstraints.Remove(other);
            other.m_ActiveConstraints.Remove(this);

            OnGearDisconnected?.Invoke(other, constraint);
            other.OnGearDisconnected?.Invoke(this, constraint);

            if (constraint != null && constraint.gameObject != null)
                Destroy(constraint.gameObject);
        }

        private void ScanForNearbyGears()
        {
            Collider[] results = new Collider[16];
            int count = Physics.OverlapSphereNonAlloc(transform.position, m_DetectionRadius, results);

            for (int i = 0; i < count; i++)
            {
                GearItem other = results[i].GetComponentInParent<GearItem>();
                if (other == null || other == this) continue;
                if (GetInstanceID() > other.GetInstanceID()) continue;
                if (m_ActiveConstraints.ContainsKey(other)) continue;

                CreateConstraint(other);
            }
        }
    }
}
