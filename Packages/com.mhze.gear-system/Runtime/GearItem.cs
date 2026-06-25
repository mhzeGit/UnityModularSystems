using System.Collections.Generic;
using UnityEngine;

namespace MHZE.GearSystem
{
    public enum GearStage { Normal, Offset }

    [AddComponentMenu("Mechanical/Gear Item")]
    [RequireComponent(typeof(Rigidbody))]
    public class GearItem : MonoBehaviour
    {
        [Header("Gear")]
        [SerializeField] private float m_Radius = 0.5f;
        [SerializeField] private GearAxis m_Axis = GearAxis.Y;
        [SerializeField] private GearStage m_Stage = GearStage.Normal;
        [SerializeField] private bool m_DebugDrawStage = true;

        private Rigidbody m_Rigidbody;
        private readonly Dictionary<GearItem, GearConstraint> m_ActiveConstraints = new();
        private readonly Dictionary<GearItem, int> m_OverlapCount = new();

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
        public IReadOnlyCollection<GearConstraint> activeConstraints => m_ActiveConstraints.Values;
        public GearStage stage => m_Stage;

        public event System.Action<GearItem, GearConstraint> OnGearConnected;
        public event System.Action<GearItem, GearConstraint> OnGearDisconnected;

        private void Awake()
        {
            m_Rigidbody = GetComponent<Rigidbody>();
        }

        private void OnTriggerEnter(Collider other)
        {
            GearItem otherGear = other.GetComponentInParent<GearItem>();
            if (otherGear == null || otherGear == this) return;

            m_OverlapCount.TryGetValue(otherGear, out int count);
            m_OverlapCount[otherGear] = count + 1;

            if (count > 0) return;

            if (GetEntityId() > otherGear.GetEntityId()) return;
            if (m_ActiveConstraints.ContainsKey(otherGear)) return;

            CreateConstraint(otherGear);
        }

        private void OnTriggerExit(Collider other)
        {
            GearItem otherGear = other.GetComponentInParent<GearItem>();
            if (otherGear == null || otherGear == this) return;

            if (m_OverlapCount.TryGetValue(otherGear, out int count))
            {
                count--;
                if (count > 0)
                {
                    m_OverlapCount[otherGear] = count;
                    return;
                }
                m_OverlapCount.Remove(otherGear);
            }

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
            constraint.debugDraw = true;

            m_ActiveConstraints[other] = constraint;
            other.m_ActiveConstraints[this] = constraint;

            AssignStage(other);

            OnGearConnected?.Invoke(other, constraint);
            other.OnGearConnected?.Invoke(this, constraint);

            return constraint;
        }

        private void AssignStage(GearItem other)
        {
            m_Stage = other.m_Stage == GearStage.Normal ? GearStage.Offset : GearStage.Normal;

            var visited = new HashSet<GearItem> { this, other };
            PropagateStage(this, visited);
            PropagateStage(other, visited);
        }

        private static void PropagateStage(GearItem gear, HashSet<GearItem> visited)
        {
            foreach (GearItem neighbor in gear.m_ActiveConstraints.Keys)
            {
                if (!visited.Add(neighbor)) continue;

                neighbor.m_Stage = gear.m_Stage == GearStage.Normal ? GearStage.Offset : GearStage.Normal;
                PropagateStage(neighbor, visited);
            }
        }

        private void DestroyConstraint(GearItem other)
        {
            if (!m_ActiveConstraints.TryGetValue(other, out GearConstraint constraint))
                return;

            m_ActiveConstraints.Remove(other);
            other.m_ActiveConstraints.Remove(this);

            m_OverlapCount.Remove(other);
            other.m_OverlapCount.Remove(this);

            OnGearDisconnected?.Invoke(other, constraint);
            other.OnGearDisconnected?.Invoke(this, constraint);

            if (constraint != null && constraint.gameObject != null)
                Destroy(constraint.gameObject);
        }

        private void OnDrawGizmos()
        {
            if (!m_DebugDrawStage) return;

            Gizmos.color = m_Stage == GearStage.Normal ? Color.green : Color.red;
            Gizmos.DrawSphere(transform.position, 0.12f);
        }
    }
}
