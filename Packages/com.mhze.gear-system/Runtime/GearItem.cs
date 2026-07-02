using System.Collections.Generic;
using UnityEngine;

namespace MHZE.GearSystem
{
    [System.Serializable]
    public struct GearDefinition
    {
        public Transform meshTransform;
        public float radius;
        public GearAxis axis;
        public float toothCount;
        [Range(0f, 1f)]
        public float sphereRadiusOffset;

        public GearDefinition(
            Transform meshTransform = null,
            float radius = 0.5f,
            GearAxis axis = GearAxis.Y,
            float toothCount = 5f,
            float sphereRadiusOffset = 0.5f)
        {
            this.meshTransform = meshTransform;
            this.radius = radius;
            this.axis = axis;
            this.toothCount = toothCount;
            this.sphereRadiusOffset = sphereRadiusOffset;
        }
    }

    [System.Serializable]
    public struct GearPair : System.IEquatable<GearPair>
    {
        private readonly EntityId m_ID;
        private readonly EntityId m_OtherID;

        public GearPair(GearItem a, GearItem b)
        {
            EntityId idA = a.GetEntityId();
            EntityId idB = b.GetEntityId();
            bool order = idA.GetHashCode() < idB.GetHashCode();
            m_ID = order ? idA : idB;
            m_OtherID = order ? idB : idA;
        }

        public bool Equals(GearPair other)
        {
            return m_ID == other.m_ID && m_OtherID == other.m_OtherID;
        }

        public override bool Equals(object obj)
        {
            return obj is GearPair other && Equals(other);
        }

        public override int GetHashCode()
        {
            return m_ID.GetHashCode() ^ (m_OtherID.GetHashCode() << 16);
        }

        public bool ContainsInstance(GearItem item)
        {
            EntityId id = item.GetEntityId();
            return m_ID == id || m_OtherID == id;
        }
    }

    public class GearItem : MonoBehaviour
    {
        public Transform gearTransform;
        public List<GearDefinition> gears = new List<GearDefinition>();

        [Header("Visual")]
        public float toothHeight = 0.1f;
        [Tooltip("Angular width of one tooth (degrees). Used for mesh offset alignment.")]
        public float toothWidth = 0.1f;
        [Tooltip("Angular offset for gear mesh alignment (degrees).")]
        public float meshOffset;
        public float overlapSphereRadius = 0.06f;

        [Header("Overlap Detection")]
        [Tooltip("How often (frames) to check for sphere overlaps. 0 = disabled.")]
        public int overlapCheckInterval = 1;

        [Header("Joint Physics")]
        [Tooltip("Spring force pulling the two contact spheres together.")]
        public float jointSpring = 500f;
        [Tooltip("Damping for the joint spring.")]
        public float jointDamper = 10f;
        [Tooltip("Maximum force the joint spring can apply.")]
        public float jointMaxForce = 1000f;

        [Header("Behavior")]
        [Tooltip("Automatically create joint GameObjects at overlapping tooth sphere positions.")]
        public bool createJoints = true;
        [Tooltip("Spawn a helper GameObject at gear A that continuously orients its axis toward gear B.")]
        public bool spawnLookAt;

        [Header("Debug")]
        public bool debugDraw;
        [Tooltip("Highlight overlapping spheres.")]
        public bool debugShowOverlaps;
        [Tooltip("Log debug values to console when enabled.")]
        public bool debugLog;

        private static Dictionary<GearPair, int> m_OverlapCounts = new Dictionary<GearPair, int>();
        private static Dictionary<GearPair, GameObject> m_ConstraintObjects = new Dictionary<GearPair, GameObject>();

        public void OnChildTriggerEnter(Transform triggerTransform, Collider other)
        {
            int gearIndex = ResolveGearIndex(triggerTransform);
            if (gearIndex < 0) return;

            GearItem otherItem = other.GetComponentInParent<GearItem>();
            if (otherItem == null) return;

            int otherIndex = otherItem.ResolveGearIndex(other.transform);
            if (otherIndex < 0) return;

            var pair = new GearPair(this, otherItem);

            m_OverlapCounts.TryGetValue(pair, out int count);
            m_OverlapCounts[pair] = count + 1;

            if (count > 0) return;

            GameObject constraintGO = CreateGearConstraint(otherItem, gearIndex, otherIndex);
            m_ConstraintObjects[pair] = constraintGO;
        }

        public void OnChildTriggerExit(Transform triggerTransform, Collider other)
        {
            int gearIndex = ResolveGearIndex(triggerTransform);
            if (gearIndex < 0) return;

            GearItem otherItem = other.GetComponentInParent<GearItem>();
            if (otherItem == null) return;

            var pair = new GearPair(this, otherItem);

            if (!m_OverlapCounts.TryGetValue(pair, out int count) || count <= 0) return;

            count--;
            if (count > 0)
            {
                m_OverlapCounts[pair] = count;
                return;
            }

            m_OverlapCounts.Remove(pair);

            if (m_ConstraintObjects.TryGetValue(pair, out GameObject constraintGO))
            {
                Destroy(constraintGO);
                m_ConstraintObjects.Remove(pair);
            }
        }

        private int ResolveGearIndex(Transform t)
        {
            for (int i = 0; i < gears.Count; i++)
            {
                if (gears[i].meshTransform == t)
                    return i;
            }
            return -1;
        }

        private GameObject CreateGearConstraint(GearItem other, int localIndex, int otherIndex)
        {
            GearDefinition localDef = gears[localIndex];
            GearDefinition otherDef = other.gears[otherIndex];

            GameObject go = new GameObject("GearConstraint");
            GearConstraint constraint = go.AddComponent<GearConstraint>();

            constraint.gearA = gearTransform;
            constraint.meshA = localDef.meshTransform;
            constraint.radiusA = localDef.radius;
            constraint.axisA = localDef.axis;
            constraint.toothCountA = localDef.toothCount;
            constraint.sphereRadiusOffsetA = localDef.sphereRadiusOffset;

            constraint.gearB = other.gearTransform;
            constraint.meshB = otherDef.meshTransform;
            constraint.radiusB = otherDef.radius;
            constraint.axisB = otherDef.axis;
            constraint.toothCountB = otherDef.toothCount;
            constraint.sphereRadiusOffsetB = otherDef.sphereRadiusOffset;

            constraint.toothHeight = toothHeight;
            constraint.toothWidth = toothWidth;
            constraint.meshOffset = meshOffset;
            constraint.overlapSphereRadius = overlapSphereRadius;
            constraint.overlapCheckInterval = overlapCheckInterval;
            constraint.jointSpring = jointSpring;
            constraint.jointDamper = jointDamper;
            constraint.jointMaxForce = jointMaxForce;
            constraint.debugDraw = debugDraw;
            constraint.debugShowOverlaps = debugShowOverlaps;
            constraint.debugLog = debugLog;
            constraint.createJoints = createJoints;
            constraint.spawnLookAt = spawnLookAt;

            return go;
        }

        private void OnDestroy()
        {
            List<GearPair> toRemove = null;
            foreach (var kvp in m_ConstraintObjects)
            {
                if (kvp.Key.ContainsInstance(this))
                {
                    if (toRemove == null) toRemove = new List<GearPair>();
                    toRemove.Add(kvp.Key);
                    Destroy(kvp.Value);
                }
            }
            if (toRemove != null)
            {
                foreach (var key in toRemove)
                {
                    m_OverlapCounts.Remove(key);
                    m_ConstraintObjects.Remove(key);
                }
            }
        }

    }
}
