using System.Collections.Generic;
using UnityEngine;

namespace MHZE.GearSystem.Physics
{
    public enum PhysicsGearAxis { X, Y, Z }

    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("Mechanical/Physics Gear")]
    public class PhysicsGear : MonoBehaviour
    {
        [SerializeField, Range(0.1f, 10f)]
        private float m_Radius = 0.5f;

        [SerializeField, Range(4, 200)]
        private int m_Teeth = 16;

        [SerializeField]
        private PhysicsGearAxis m_Axis = PhysicsGearAxis.Y;

        [SerializeField]
        private LayerMask m_LayerMask = ~0;

        [SerializeField, Range(0.01f, 1f)]
        private float m_ContactDistance = 0.05f;

        [Header("Debug")]
        [SerializeField]
        private bool m_DrawGizmos = true;

        [SerializeField]
        private Color m_GizmoColor = new Color(0.2f, 0.7f, 1f, 0.8f);

        private Rigidbody m_Rigidbody;
        private readonly Dictionary<PhysicsGear, GearJoint> m_Joints = new Dictionary<PhysicsGear, GearJoint>();
        private readonly List<PhysicsGear> m_Neighbors = new List<PhysicsGear>();
        private Collider[] m_OverlapBuffer = new Collider[32];

        public float radius => m_Radius;
        public int teeth => m_Teeth;
        public Rigidbody attachedRigidbody => m_Rigidbody;
        public Vector3 worldAxis => GetWorldAxis();

        private void Awake()
        {
            m_Rigidbody = GetComponent<Rigidbody>();
            m_Rigidbody.useGravity = false;
        }

        private void FixedUpdate()
        {
            ScanForNeighbors();
            UpdateJoints();
        }

        private void ScanForNeighbors()
        {
            Vector3 center = m_Rigidbody.worldCenterOfMass;
            float scanRadius = m_Radius + m_ContactDistance;
            int count = UnityEngine.Physics.OverlapSphereNonAlloc(center, scanRadius, m_OverlapBuffer, m_LayerMask, QueryTriggerInteraction.Ignore);

            m_Neighbors.Clear();

            for (int i = 0; i < count; i++)
            {
                Collider col = m_OverlapBuffer[i];
                if (col == null) continue;

                PhysicsGear other = col.GetComponentInParent<PhysicsGear>();
                if (other == null || other == this) continue;

                if (!m_Neighbors.Contains(other))
                    m_Neighbors.Add(other);
            }

            System.Array.Clear(m_OverlapBuffer, 0, m_OverlapBuffer.Length);
        }

        private void UpdateJoints()
        {
            var toRemove = new List<PhysicsGear>();

            foreach (var kvp in m_Joints)
            {
                if (kvp.Key == null)
                {
                    kvp.Value.Destroy();
                    toRemove.Add(kvp.Key);
                    continue;
                }

                if (!m_Neighbors.Contains(kvp.Key))
                {
                    kvp.Value.Destroy();
                    kvp.Key.UnregisterJoint(this);
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var key in toRemove)
                m_Joints.Remove(key);
            toRemove.Clear();

            foreach (var other in m_Neighbors)
            {
                if (m_Joints.ContainsKey(other)) continue;
                if (GetInstanceID() >= other.GetInstanceID()) continue;

                if (CheckContact(other))
                {
                    GearJoint joint = new GearJoint(this, other);
                    m_Joints[other] = joint;
                    other.RegisterJoint(this, joint);
                }
            }

            foreach (var kvp in m_Joints)
            {
                if (kvp.Value.IsValid)
                    kvp.Value.UpdateAnchors();
                else
                {
                    kvp.Value.Destroy();
                    kvp.Key.UnregisterJoint(this);
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var key in toRemove)
                m_Joints.Remove(key);
            toRemove.Clear();
        }

        private bool CheckContact(PhysicsGear other)
        {
            Vector3 posA = m_Rigidbody.worldCenterOfMass;
            Vector3 posB = other.m_Rigidbody.worldCenterOfMass;
            float dist = Vector3.Distance(posA, posB);
            float contactDist = m_Radius + other.m_Radius;
            return Mathf.Abs(dist - contactDist) < m_ContactDistance + other.m_ContactDistance;
        }

        public Vector3 ToothToWorldPosition(int toothIndex)
        {
            float anglePerTooth = 360f / m_Teeth;
            float toothAngle = toothIndex * anglePerTooth;

            Quaternion rotation = transform.rotation * Quaternion.AngleAxis(toothAngle, GetAxisVector(m_Axis));
            Vector3 radiusDir = rotation * Vector3.right;
            return m_Rigidbody.worldCenterOfMass + radiusDir * m_Radius;
        }

        public int ClosestToothToPoint(Vector3 worldPoint)
        {
            Vector3 dir = worldPoint - m_Rigidbody.worldCenterOfMass;
            Vector3 axisDir = GetWorldAxis();
            Vector3 projected = Vector3.ProjectOnPlane(dir, axisDir);

            if (projected.sqrMagnitude < 1e-8f)
                return 0;

            float angle = Vector3.SignedAngle(transform.right, projected.normalized, axisDir);
            if (angle < 0f) angle += 360f;

            float anglePerTooth = 360f / m_Teeth;
            return Mathf.RoundToInt(angle / anglePerTooth) % m_Teeth;
        }

        public Vector3 GetWorldAxis()
        {
            return m_Axis switch
            {
                PhysicsGearAxis.X => transform.right,
                PhysicsGearAxis.Z => transform.forward,
                _ => transform.up
            };
        }

        public static Vector3 GetAxisVector(PhysicsGearAxis axis)
        {
            return axis switch
            {
                PhysicsGearAxis.X => Vector3.right,
                PhysicsGearAxis.Z => Vector3.forward,
                _ => Vector3.up
            };
        }

        internal void RegisterJoint(PhysicsGear other, GearJoint joint)
        {
            m_Joints[other] = joint;
        }

        internal void UnregisterJoint(PhysicsGear other)
        {
            m_Joints.Remove(other);
        }

        private void OnDrawGizmos()
        {
            if (!m_DrawGizmos) return;
            if (m_Rigidbody == null) m_Rigidbody = GetComponent<Rigidbody>();
            if (m_Rigidbody == null) return;

            Gizmos.color = m_GizmoColor;

            DrawCircle(m_Rigidbody.worldCenterOfMass, GetWorldAxis(), m_Radius, 64);

            float anglePerTooth = 360f / m_Teeth;
            float toothHeight = m_Radius * 0.15f;

            for (int i = 0; i < m_Teeth; i++)
            {
                Vector3 pos = ToothToWorldPosition(i);
                Gizmos.DrawWireSphere(pos, toothHeight * 0.5f);
            }

            Gizmos.color = Color.white;
            Gizmos.DrawRay(m_Rigidbody.worldCenterOfMass, GetWorldAxis() * m_Radius * 0.3f);

            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.4f);
            Vector3 scanCenter = m_Rigidbody.worldCenterOfMass;
            DrawCircle(scanCenter, GetWorldAxis(), m_Radius + m_ContactDistance, 48);

            Gizmos.color = m_GizmoColor;
            foreach (var kvp in m_Joints)
            {
                if (kvp.Value.IsValid)
                    Gizmos.DrawLine(m_Rigidbody.worldCenterOfMass, kvp.Key.attachedRigidbody.worldCenterOfMass);
            }
        }

        private static void DrawCircle(Vector3 center, Vector3 axis, float radius, int segments)
        {
            Vector3 b1, b2;
            if (Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.99f)
            {
                b1 = Vector3.right; b2 = Vector3.forward;
            }
            else
            {
                b1 = Vector3.Cross(axis, Vector3.up).normalized;
                b2 = Vector3.Cross(axis, b1).normalized;
            }

            Vector3 prev = center + b1 * radius;
            for (int i = 1; i <= segments; i++)
            {
                float angle = 2f * Mathf.PI * i / segments;
                Vector3 dir = b1 * Mathf.Cos(angle) + b2 * Mathf.Sin(angle);
                Vector3 curr = center + dir * radius;
                Gizmos.DrawLine(prev, curr);
                prev = curr;
            }
        }

        private void OnDestroy()
        {
            foreach (var kvp in m_Joints)
                kvp.Value.Destroy();
            m_Joints.Clear();
        }
    }
}
