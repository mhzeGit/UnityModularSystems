using UnityEngine;

namespace MHZE.GearSystem.Physics
{
    public class GearJoint
    {
        private PhysicsGear m_GearA;
        private PhysicsGear m_GearB;
        private ConfigurableJoint m_Joint;
        private GameObject m_HostObject;
        private int m_ToothA;
        private int m_ToothB;

        public bool IsValid => m_Joint != null && m_GearA != null && m_GearB != null;
        public PhysicsGear gearA => m_GearA;
        public PhysicsGear gearB => m_GearB;

        public GearJoint(PhysicsGear a, PhysicsGear b)
        {
            m_GearA = a;
            m_GearB = b;
            Initialize();
        }

        private void Initialize()
        {
            m_HostObject = new GameObject("GearJoint_" + m_GearA.name + "_" + m_GearB.name);
            m_HostObject.hideFlags = HideFlags.HideAndDontSave;
            m_HostObject.transform.SetParent(m_GearA.transform);
            m_HostObject.transform.localPosition = Vector3.zero;
            m_HostObject.transform.localRotation = Quaternion.identity;

            m_Joint = m_HostObject.AddComponent<ConfigurableJoint>();
            m_Joint.connectedBody = m_GearB.attachedRigidbody;
            m_Joint.autoConfigureConnectedAnchor = false;
            m_Joint.enableCollision = false;
            m_Joint.enablePreprocessing = false;

            m_Joint.xMotion = ConfigurableJointMotion.Locked;
            m_Joint.yMotion = ConfigurableJointMotion.Locked;
            m_Joint.zMotion = ConfigurableJointMotion.Locked;
            m_Joint.angularXMotion = ConfigurableJointMotion.Free;
            m_Joint.angularYMotion = ConfigurableJointMotion.Free;
            m_Joint.angularZMotion = ConfigurableJointMotion.Free;

            m_Joint.projectionMode = JointProjectionMode.PositionAndRotation;
            m_Joint.projectionDistance = 0.001f;
            m_Joint.projectionAngle = 0.1f;

            UpdateAnchors();
        }

        public void UpdateAnchors()
        {
            if (!IsValid) return;

            m_ToothA = FindContactTooth(m_GearA, m_GearB);
            m_ToothB = FindContactTooth(m_GearB, m_GearA);

            Vector3 worldAnchorA = m_GearA.ToothToWorldPosition(m_ToothA);
            Vector3 worldAnchorB = m_GearB.ToothToWorldPosition(m_ToothB);

            m_Joint.anchor = m_GearA.transform.InverseTransformPoint(worldAnchorA);
            m_Joint.connectedAnchor = m_GearB.transform.InverseTransformPoint(worldAnchorB);

            Vector3 axisA = m_GearA.GetWorldAxis();
            Vector3 axisB = m_GearB.GetWorldAxis();

            m_Joint.axis = m_GearA.transform.InverseTransformDirection(axisA);
            m_Joint.secondaryAxis = m_GearA.transform.InverseTransformDirection(
                Vector3.Cross(axisA, (m_GearB.attachedRigidbody.worldCenterOfMass - m_GearA.attachedRigidbody.worldCenterOfMass).normalized));

            SoftJointLimit linearLimit = m_Joint.linearLimit;
            linearLimit.limit = 0f;
            m_Joint.linearLimit = linearLimit;
        }

        private static int FindContactTooth(PhysicsGear gear, PhysicsGear other)
        {
            Vector3 contactPoint = GetContactPoint(gear, other);
            return gear.ClosestToothToPoint(contactPoint);
        }

        private static Vector3 GetContactPoint(PhysicsGear gear, PhysicsGear other)
        {
            Vector3 centerA = gear.attachedRigidbody.worldCenterOfMass;
            Vector3 centerB = other.attachedRigidbody.worldCenterOfMass;
            Vector3 direction = (centerB - centerA).normalized;
            return centerA + direction * gear.radius;
        }

        public void Destroy()
        {
            if (m_Joint != null)
            {
                UnityEngine.Object.Destroy(m_Joint);
                m_Joint = null;
            }

            if (m_HostObject != null)
            {
                UnityEngine.Object.Destroy(m_HostObject);
                m_HostObject = null;
            }

            m_GearA = null;
            m_GearB = null;
        }
    }
}
