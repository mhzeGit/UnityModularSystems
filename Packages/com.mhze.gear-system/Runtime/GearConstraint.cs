using System.Collections.Generic;
using UnityEngine;

namespace MHZE.GearSystem
{
    public enum GearAxis { X, Y, Z }

    [System.Serializable]
    public struct OverlapInfo
    {
        public Vector3 pointA;
        public Vector3 pointB;
        public float distance;
        public Transform transformA;
        public Transform transformB;
        public int toothIndexA;
        public int toothIndexB;
        public readonly bool IsSamePair(in OverlapInfo other)
        {
            return toothIndexA == other.toothIndexA && toothIndexB == other.toothIndexB;
        }
    }

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
        public float overlapSphereRadius = 0.06f;
        [Tooltip("Sphere radial offset for gear A. 0 = at radius, 1 = at tooth tip.")]
        [Range(0f, 1f)]
        public float sphereRadiusOffsetA = 0.5f;
        [Tooltip("Sphere radial offset for gear B. 0 = at radius, 1 = at tooth tip.")]
        [Range(0f, 1f)]
        public float sphereRadiusOffsetB = 0.5f;
        [Tooltip("How often (frames) to check for sphere overlaps. 0 = disabled.")]
        public int overlapCheckInterval = 1;
        [Tooltip("Spring force pulling the two contact spheres together.")]
        public float jointSpring = 500f;
        [Tooltip("Damping for the joint spring.")]
        public float jointDamper = 10f;
        [Tooltip("Maximum force the joint spring can apply.")]
        public float jointMaxForce = 1000f;
        public Color debugColorA = Color.red;
        public Color debugColorB = Color.blue;
        public bool debugDraw;
        public bool debugShowOverlaps;
        [Tooltip("Log debug values to console when enabled.")]
        public bool debugLog;

        [Tooltip("Automatically create joint GameObjects at overlapping tooth sphere positions.")]
        public bool createJoints = true;
        public System.Action<OverlapInfo> onOverlapStarted;
        public System.Action<OverlapInfo> onOverlapEnded;

        [Tooltip("Spawn a helper GameObject at gear A that continuously orients its axis toward gear B.")]
        public bool spawnLookAt;

        private static readonly OverlapInfoComparer m_OverlapComparer = new OverlapInfoComparer();
        private OverlapInfo m_ActiveOverlap;
        private bool m_HasActiveOverlap;
        private OverlapInfo m_LastActiveOverlap;
        private bool m_HasLastActive;

        public bool HasActiveOverlap => m_HasActiveOverlap;
        public OverlapInfo ActiveOverlap => m_ActiveOverlap;
        private ConfigurableJoint m_ActiveJoint;
        private GearLookAt m_LookAt;
        private int m_FrameCounter;

        private Transform EffectiveTransformA => meshA != null ? meshA : gearA;
        private Transform EffectiveTransformB => meshB != null ? meshB : gearB;

        private void Update()
        {
            UpdateLookAt();

            if (overlapCheckInterval <= 0) return;

            m_FrameCounter++;
            if (m_FrameCounter < overlapCheckInterval) return;
            m_FrameCounter = 0;

            CheckOverlaps();

            if (m_ActiveJoint != null)
            {
                if (!createJoints)
                    DestroyJointGO();
                else if (m_HasActiveOverlap)
                    UpdateJointGO(m_ActiveOverlap);
            }
        }

        private void UpdateLookAt()
        {
            if (m_LookAt == null && spawnLookAt)
                SpawnLookAt();
            else if (m_LookAt != null && !spawnLookAt)
                DestroyLookAt();
        }

        private void SpawnLookAt()
        {
            if (gearA == null || gearB == null) return;
            m_LookAt = GearLookAt.Spawn(gearA, gearB, axisA);
        }

        private void DestroyLookAt()
        {
            if (m_LookAt != null)
            {
                if (m_LookAt.gameObject != null)
                    Destroy(m_LookAt.gameObject);
                m_LookAt = null;
            }
        }

        public void CheckOverlaps()
        {
            Transform tA = EffectiveTransformA;
            Transform tB = EffectiveTransformB;
            if (tA == null || tB == null) return;

            Vector3[] posA = GetSpherePositions(tA, radiusA, axisA, toothCountA, true, sphereRadiusOffsetA);
            Vector3[] posB = GetSpherePositions(tB, radiusB, axisB, toothCountB, false, sphereRadiusOffsetB);

            float minDistSq = (overlapSphereRadius + overlapSphereRadius) * (overlapSphereRadius + overlapSphereRadius);
            OverlapInfo? closestOv = null;
            OverlapInfo? currentActiveOv = null;
            OverlapInfo? differentOv = null;
            float bestDistSq = float.MaxValue;
            bool lastActiveStillPresent = false;

            for (int i = 0; i < posA.Length; i++)
            {
                for (int j = 0; j < posB.Length; j++)
                {
                    float distSq = (posA[i] - posB[j]).sqrMagnitude;
                    if (distSq < minDistSq)
                    {
                        bool isActive = m_HasActiveOverlap && i == m_ActiveOverlap.toothIndexA && j == m_ActiveOverlap.toothIndexB;
                        bool isLastActive = m_HasLastActive && i == m_LastActiveOverlap.toothIndexA && j == m_LastActiveOverlap.toothIndexB;

                        if (isLastActive)
                            lastActiveStillPresent = true;

                        var ov = new OverlapInfo
                        {
                            pointA = posA[i],
                            pointB = posB[j],
                            distance = Mathf.Sqrt(distSq),
                            transformA = tA,
                            transformB = tB,
                            toothIndexA = i,
                            toothIndexB = j
                        };

                        if (isActive)
                            currentActiveOv = ov;
                        else if (m_HasActiveOverlap && differentOv == null && !isLastActive)
                            differentOv = ov;

                        if (!isLastActive && distSq < bestDistSq)
                        {
                            bestDistSq = distSq;
                            closestOv = ov;
                        }
                    }
                }
            }

            if (!lastActiveStillPresent)
                m_HasLastActive = false;

            if (m_HasActiveOverlap)
            {
                if (currentActiveOv.HasValue)
                {
                    if (differentOv.HasValue)
                    {
                        m_LastActiveOverlap = m_ActiveOverlap;
                        m_HasLastActive = true;
                        onOverlapEnded?.Invoke(m_ActiveOverlap);
                        m_ActiveOverlap = differentOv.Value;
                        onOverlapStarted?.Invoke(m_ActiveOverlap);
                        if (createJoints)
                        {
                            if (m_ActiveJoint != null)
                                UpdateJointGO(m_ActiveOverlap);
                            else
                                CreateJointGO(m_ActiveOverlap);
                        }
                    }
                    else
                    {
                        m_ActiveOverlap = currentActiveOv.Value;
                        if (createJoints && m_ActiveJoint != null)
                            UpdateJointGO(m_ActiveOverlap);
                    }
                    return;
                }

                if (closestOv.HasValue)
                {
                    m_LastActiveOverlap = m_ActiveOverlap;
                    m_HasLastActive = true;
                    onOverlapEnded?.Invoke(m_ActiveOverlap);
                    m_ActiveOverlap = closestOv.Value;
                    onOverlapStarted?.Invoke(m_ActiveOverlap);
                    if (createJoints)
                    {
                        DestroyJointGO();
                        CreateJointGO(m_ActiveOverlap);
                    }
                }
                else
                {
                    if (createJoints && m_ActiveJoint != null)
                        DestroyJointGO();
                }
                return;
            }

            if (closestOv.HasValue)
            {
                m_ActiveOverlap = closestOv.Value;
                m_HasActiveOverlap = true;
                onOverlapStarted?.Invoke(m_ActiveOverlap);
                if (createJoints)
                    CreateJointGO(m_ActiveOverlap);
            }
        }

        public Vector3[] GetSpherePositions(Transform t, float radius, GearAxis axis, float toothCount, bool onTeeth, float normalizedOffset)
        {
            Vector3 center = t.position;
            Vector3 nml = AxisToVector(axis, t);
            Vector3 tan = Vector3.ProjectOnPlane(t.right, nml).normalized;
            if (tan.sqrMagnitude < 0.001f)
                tan = Vector3.ProjectOnPlane(t.forward, nml).normalized;

            float angleStep = 360f / toothCount;
            float offsetAngle = (toothWidth / radius) * Mathf.Rad2Deg;
            float sphereOffset = onTeeth ? 0f : angleStep * 0.5f;
            int count = Mathf.Max(1, Mathf.RoundToInt(toothCount));

            float radialOffset = normalizedOffset * toothHeight;

            Vector3[] positions = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Quaternion.AngleAxis(i * angleStep + offsetAngle + sphereOffset, nml) * tan;
                positions[i] = center + dir * (radius + radialOffset);
            }
            return positions;
        }

        public OverlapInfo[] GetOverlaps()
        {
            Transform tA = EffectiveTransformA;
            Transform tB = EffectiveTransformB;
            if (tA == null || tB == null) return System.Array.Empty<OverlapInfo>();

            Vector3[] posA = GetSpherePositions(tA, radiusA, axisA, toothCountA, true, sphereRadiusOffsetA);
            Vector3[] posB = GetSpherePositions(tB, radiusB, axisB, toothCountB, false, sphereRadiusOffsetB);

            float minDist = overlapSphereRadius * 2f;
            var overlaps = new List<OverlapInfo>();

            for (int i = 0; i < posA.Length; i++)
            {
                for (int j = 0; j < posB.Length; j++)
                {
                    float dist = Vector3.Distance(posA[i], posB[j]);
                    if (dist < minDist)
                    {
                        overlaps.Add(new OverlapInfo
                        {
                            pointA = posA[i],
                            pointB = posB[j],
                            distance = dist,
                            transformA = tA,
                            transformB = tB,
                            toothIndexA = i,
                            toothIndexB = j
                        });
                    }
                }
            }
            return overlaps.ToArray();
        }

        private static Vector3 AxisToVector(GearAxis axis, Transform transform)
        {
            switch (axis)
            {
                case GearAxis.X: return transform.right;
                case GearAxis.Y: return transform.up;
                case GearAxis.Z: return transform.forward;
                default: return transform.up;
            }
        }

        private Vector3 GetLookAtUpDownAxis()
        {
            if (m_LookAt == null)
                return Vector3.up;

            switch (axisA)
            {
                case GearAxis.X: return m_LookAt.transform.up;
                case GearAxis.Y: return m_LookAt.transform.right;
                case GearAxis.Z: return m_LookAt.transform.up;
                default: return m_LookAt.transform.up;
            }
        }

        private void CreateJointGO(OverlapInfo ov)
        {
            Rigidbody rbA = gearA != null ? gearA.GetComponent<Rigidbody>() : null;
            Rigidbody rbB = gearB != null ? gearB.GetComponent<Rigidbody>() : null;
            if (rbA == null || rbB == null) return;

            ConfigurableJoint joint = gearA.gameObject.AddComponent<ConfigurableJoint>();
            joint.connectedBody = rbB;
            joint.anchor = rbA.transform.InverseTransformPoint(ov.pointA);
            joint.connectedAnchor = rbB.transform.InverseTransformPoint(ov.pointB);
            joint.autoConfigureConnectedAnchor = false;

            Vector3 worldUpDown = GetLookAtUpDownAxis();
            joint.axis = rbA.transform.InverseTransformDirection(worldUpDown).normalized;
            joint.secondaryAxis = Vector3.zero;

            joint.xMotion = ConfigurableJointMotion.Limited;
            joint.yMotion = ConfigurableJointMotion.Free;
            joint.zMotion = ConfigurableJointMotion.Free;
            joint.angularXMotion = ConfigurableJointMotion.Free;
            joint.angularYMotion = ConfigurableJointMotion.Free;
            joint.angularZMotion = ConfigurableJointMotion.Free;

            joint.xDrive = new JointDrive
            {
                positionSpring = jointSpring,
                positionDamper = jointDamper,
                maximumForce = jointMaxForce
            };

            joint.targetPosition = Vector3.zero;
            joint.targetAngularVelocity = Vector3.zero;

            m_ActiveJoint = joint;
        }

        private void UpdateJointGO(OverlapInfo ov)
        {
            Rigidbody rbA = gearA != null ? gearA.GetComponent<Rigidbody>() : null;
            Rigidbody rbB = gearB != null ? gearB.GetComponent<Rigidbody>() : null;
            if (rbA == null || rbB == null) return;

            m_ActiveJoint.anchor = rbA.transform.InverseTransformPoint(ov.pointA);
            m_ActiveJoint.connectedAnchor = rbB.transform.InverseTransformPoint(ov.pointB);

            Vector3 worldUpDown = GetLookAtUpDownAxis();
            m_ActiveJoint.axis = rbA.transform.InverseTransformDirection(worldUpDown).normalized;
        }

        private void DestroyJointGO()
        {
            if (m_ActiveJoint != null)
            {
                Destroy(m_ActiveJoint);
                m_ActiveJoint = null;
            }
        }

        private void OnDestroy()
        {
            DestroyJointGO();
            DestroyLookAt();
        }

        private class OverlapInfoComparer : IEqualityComparer<OverlapInfo>
        {
            public bool Equals(OverlapInfo a, OverlapInfo b)
            {
                return a.toothIndexA == b.toothIndexA && a.toothIndexB == b.toothIndexB;
            }

            public int GetHashCode(OverlapInfo obj)
            {
                return obj.toothIndexA ^ (obj.toothIndexB << 16);
            }
        }
    }
}
