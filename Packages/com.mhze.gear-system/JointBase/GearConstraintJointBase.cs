using UnityEngine;

namespace MHZE.GearSystem
{
    [AddComponentMenu("Mechanical/Gear Constraint Joint Base")]
    public class GearConstraintJointBase : GearConstraintBase
    {
        public GameObject jointHost { get; private set; }
        public ConfigurableJoint jointToA { get; private set; }
        public ConfigurableJoint jointToB { get; private set; }

        private Rigidbody m_RbA;
        private Rigidbody m_RbB;
        private Vector3? m_CachedOverlapContact;

        private void Start()
        {
            if (gearA == null || gearB == null) return;

            m_RbA = gearA.GetComponent<Rigidbody>();
            m_RbB = gearB.GetComponent<Rigidbody>();

            if (m_RbA == null || m_RbB == null)
            {
                Debug.LogError("[GearConstraintJointBase] Both gears require Rigidbody components.");
                return;
            }

            CreateJoints();
        }

        private void CreateJoints()
        {
            Vector3 dir = (gearB.position - gearA.position).normalized;
            Vector3 contactPoint = m_CachedOverlapContact ?? (gearA.position + dir * radiusA);

            jointHost = new GameObject($"GearJoint_{gearA.name}_{gearB.name}");
            jointHost.transform.position = contactPoint;
            jointHost.transform.SetParent(transform, true);

            var hostRb = jointHost.AddComponent<Rigidbody>();
            hostRb.isKinematic = true;

            Vector3 axisAWorld = GetWorldAxis(gearA, axisA);
            Vector3 axisBWorld = GetWorldAxis(gearB, axisB);

            jointToA = AddJointToHost(m_RbA, contactPoint, gearA, axisAWorld, dir);
            jointToB = AddJointToHost(m_RbB, contactPoint, gearB, axisBWorld, dir);

            if (debugLog)
                Debug.Log($"[GearConstraintJointBase] host at {contactPoint:F3} " +
                          $"axisA={axisAWorld:F3} axisB={axisBWorld:F3}");
        }

        private ConfigurableJoint AddJointToHost(Rigidbody connectedBody, Vector3 contactPoint,
            Transform gear, Vector3 gearAxisWorld, Vector3 dir)
        {
            var joint = jointHost.AddComponent<ConfigurableJoint>();
            joint.connectedBody = connectedBody;
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = Vector3.zero;
            joint.connectedAnchor = gear.InverseTransformPoint(contactPoint);

            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;

            joint.axis = jointHost.transform.InverseTransformDirection(gearAxisWorld);

            Vector3 secondary = Vector3.Cross(dir, gearAxisWorld);
            if (secondary.sqrMagnitude < 0.0001f)
                secondary = dir - Vector3.Project(dir, gearAxisWorld);
            if (secondary.sqrMagnitude < 0.0001f)
                secondary = Vector3.Cross(dir, Vector3.up).normalized;
            else
                secondary.Normalize();
            joint.secondaryAxis = jointHost.transform.InverseTransformDirection(secondary);

            joint.angularXMotion = ConfigurableJointMotion.Free;
            joint.angularYMotion = ConfigurableJointMotion.Locked;
            joint.angularZMotion = ConfigurableJointMotion.Locked;

            return joint;
        }

        private void SnapJointAnchors(Vector3 contactPoint)
        {
            jointHost.transform.position = contactPoint;

            if (jointToA != null && gearA != null)
                jointToA.connectedAnchor = gearA.InverseTransformPoint(contactPoint);

            if (jointToB != null && gearB != null)
                jointToB.connectedAnchor = gearB.InverseTransformPoint(contactPoint);
        }

        private void OnDestroy()
        {
            if (jointHost != null)
            {
                Destroy(jointHost);
            }
        }

        protected override void OnDrawGizmos()
        {
            int winA = -1, winB = -1;

            var teethA = GearConstraintDebugger.GetToothPositions(gearA, meshA, axisA, radiusA, toothHeight, toothCountA, false);
            var teethB = GearConstraintDebugger.GetToothPositions(gearB, meshB, axisB, radiusB, toothHeight, toothCountB, true);

            float bestDist = float.MaxValue;
            Vector3 contactPoint = Vector3.zero;
            bool found = false;

            for (int i = 0; i < teethA.Count; i++)
            {
                for (int j = 0; j < teethB.Count; j++)
                {
                    float dist = Vector3.Distance(teethA[i], teethB[j]);
                    if (dist < 0.1f && dist < bestDist)
                    {
                        bestDist = dist;
                        contactPoint = (teethA[i] + teethB[j]) * 0.5f;
                        winA = i;
                        winB = j;
                        found = true;
                    }
                }
            }

            if (found)
            {
                m_CachedOverlapContact = contactPoint;

                if (jointHost != null)
                {
                    SnapJointAnchors(contactPoint);
                }
            }

            if (debugDraw)
            {
                GearConstraintDebugger.Draw(this, winA, winB);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!enabled || gearA == null || gearB == null) return;

            Vector3 dir = (gearB.position - gearA.position).normalized;
            Vector3 cp = gearA.position + dir * radiusA;

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(cp, 0.05f);

            Vector3 axisAWorld = GetWorldAxis(gearA, axisA);
            Vector3 axisBWorld = GetWorldAxis(gearB, axisB);

            Gizmos.color = Color.green;
            Gizmos.DrawRay(cp, axisAWorld * 0.4f);
            Gizmos.DrawRay(gearA.position, axisAWorld * 0.5f);

            Gizmos.color = Color.blue;
            Gizmos.DrawRay(cp, axisBWorld * 0.4f);
            Gizmos.DrawRay(gearB.position, axisBWorld * 0.5f);
        }
    }
}
