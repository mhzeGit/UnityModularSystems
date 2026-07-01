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
        [Tooltip("Max distance for spring pull. Beyond this, the joint acts as a hard clamp.")]
        public float jointMaxDistance = 0.1f;
        [Tooltip("Maximum force the joint spring can apply.")]
        public float jointMaxForce = 1000f;
        [Tooltip("Apply spring pull on gear A's local X axis.")]
        public bool springAxisX = true;
        [Tooltip("Apply spring pull on gear A's local Y axis.")]
        public bool springAxisY = true;
        [Tooltip("Apply spring pull on gear A's local Z axis.")]
        public bool springAxisZ = true;
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

        private static readonly OverlapInfoComparer m_OverlapComparer = new OverlapInfoComparer();
        private OverlapInfo m_ActiveOverlap;
        private bool m_HasActiveOverlap;

        public bool HasActiveOverlap => m_HasActiveOverlap;
        public OverlapInfo ActiveOverlap => m_ActiveOverlap;
        private ConfigurableJoint m_ActiveJoint;
        private int m_FrameCounter;

        private Transform EffectiveTransformA => meshA != null ? meshA : gearA;
        private Transform EffectiveTransformB => meshB != null ? meshB : gearB;

        private void Update()
        {
            if (overlapCheckInterval <= 0) return;

            m_FrameCounter++;
            if (m_FrameCounter < overlapCheckInterval) return;
            m_FrameCounter = 0;

            CheckOverlaps();

            if (createJoints && m_ActiveJoint != null && m_HasActiveOverlap)
                UpdateJointGO(m_ActiveOverlap);
        }

        public void CheckOverlaps()
        {
            Transform tA = EffectiveTransformA;
            Transform tB = EffectiveTransformB;
            if (tA == null || tB == null) return;

            Vector3[] posA = GetSpherePositions(tA, radiusA, axisA, toothCountA, true, sphereRadiusOffsetA);
            Vector3[] posB = GetSpherePositions(tB, radiusB, axisB, toothCountB, false, sphereRadiusOffsetB);

            float minDist = overlapSphereRadius * 2f;
            OverlapInfo? candidate = null;

            for (int i = 0; i < posA.Length && candidate == null; i++)
            {
                for (int j = 0; j < posB.Length && candidate == null; j++)
                {
                    float dist = Vector3.Distance(posA[i], posB[j]);
                    if (dist < minDist)
                    {
                        candidate = new OverlapInfo
                        {
                            pointA = posA[i],
                            pointB = posB[j],
                            distance = dist,
                            transformA = tA,
                            transformB = tB
                        };
                    }
                }
            }

            bool hasCandidate = candidate.HasValue;

            if (hasCandidate && !m_HasActiveOverlap)
            {
                onOverlapStarted?.Invoke(candidate.Value);
                if (createJoints)
                    CreateJointGO(candidate.Value);
            }
            else if (hasCandidate && m_HasActiveOverlap)
            {
                if (!m_OverlapComparer.Equals(candidate.Value, m_ActiveOverlap))
                {
                    onOverlapEnded?.Invoke(m_ActiveOverlap);
                    onOverlapStarted?.Invoke(candidate.Value);
                }
                if (createJoints && m_ActiveJoint != null)
                    UpdateJointGO(candidate.Value);
            }
            else if (!hasCandidate && m_HasActiveOverlap)
            {
                onOverlapEnded?.Invoke(m_ActiveOverlap);
                if (createJoints)
                    DestroyJointGO();
            }

            if (hasCandidate)
            {
                m_ActiveOverlap = candidate.Value;
                m_HasActiveOverlap = true;
            }
            else
            {
                m_HasActiveOverlap = false;
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
                            transformB = tB
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

            joint.xMotion = springAxisX ? ConfigurableJointMotion.Limited : ConfigurableJointMotion.Free;
            joint.yMotion = springAxisY ? ConfigurableJointMotion.Limited : ConfigurableJointMotion.Free;
            joint.zMotion = springAxisZ ? ConfigurableJointMotion.Limited : ConfigurableJointMotion.Free;
            joint.angularXMotion = ConfigurableJointMotion.Free;
            joint.angularYMotion = ConfigurableJointMotion.Free;
            joint.angularZMotion = ConfigurableJointMotion.Free;

            var drive = new JointDrive
            {
                positionSpring = jointSpring,
                positionDamper = jointDamper,
                maximumForce = jointMaxForce
            };

            if (springAxisX) joint.xDrive = drive;
            if (springAxisY) joint.yDrive = drive;
            if (springAxisZ) joint.zDrive = drive;

            joint.targetPosition = Vector3.zero;
            joint.targetAngularVelocity = Vector3.zero;

            joint.linearLimit = new SoftJointLimit
            {
                limit = jointMaxDistance,
                bounciness = 0f,
                contactDistance = 0f
            };

            m_ActiveJoint = joint;
        }

        private void UpdateJointGO(OverlapInfo ov)
        {
            Rigidbody rbA = gearA != null ? gearA.GetComponent<Rigidbody>() : null;
            Rigidbody rbB = gearB != null ? gearB.GetComponent<Rigidbody>() : null;
            if (rbA == null || rbB == null) return;

            m_ActiveJoint.anchor = rbA.transform.InverseTransformPoint(ov.pointA);
            m_ActiveJoint.connectedAnchor = rbB.transform.InverseTransformPoint(ov.pointB);
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
        }

        private class OverlapInfoComparer : IEqualityComparer<OverlapInfo>
        {
            public bool Equals(OverlapInfo a, OverlapInfo b)
            {
                return Vector3.Distance(a.pointA, b.pointA) < 0.0001f &&
                       Vector3.Distance(a.pointB, b.pointB) < 0.0001f;
            }

            public int GetHashCode(OverlapInfo obj)
            {
                return obj.pointA.GetHashCode() ^ (obj.pointB.GetHashCode() << 2);
            }
        }
    }
}
