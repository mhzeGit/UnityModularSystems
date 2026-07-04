using UnityEngine;

namespace MHZE.RopePhysics
{
    [ExecuteAlways]
    [AddComponentMenu("Physics/Rope")]
    public class Rope : MonoBehaviour
    {
        [SerializeField] private Transform m_StartPoint;
        [SerializeField] private Transform m_EndPoint;
        [SerializeField] private float m_DesiredDistance = 5f;

        private LineRenderer m_LineRenderer;
        private ConfigurableJoint m_Joint;
        private Rigidbody m_StartRigidbody;
        private Rigidbody m_EndRigidbody;
        private bool m_NeedsRebuild;

        private void Awake()
        {
            m_LineRenderer = GetComponent<LineRenderer>();
            if (m_LineRenderer == null)
                m_LineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        private void OnEnable()
        {
            CleanupOrphanedJoints();
        }

        private void OnValidate()
        {
            m_NeedsRebuild = true;
        }

        private void Update()
        {
            if (m_StartPoint == null || m_EndPoint == null)
            {
                TeardownJoint();
                return;
            }

            m_LineRenderer.positionCount = 2;
            m_LineRenderer.SetPosition(0, m_StartPoint.position);
            m_LineRenderer.SetPosition(1, m_EndPoint.position);

            if (m_NeedsRebuild || m_Joint == null)
                SetupJoint();
        }

        private void SetupJoint()
        {
            TeardownJoint();
            m_NeedsRebuild = false;

            EnsureRigidbodies();
            if (m_StartRigidbody == null || m_EndRigidbody == null || m_StartPoint == m_EndPoint)
                return;

            m_Joint = m_StartPoint.gameObject.AddComponent<ConfigurableJoint>();
            m_Joint.connectedBody = m_EndRigidbody;
            m_Joint.autoConfigureConnectedAnchor = false;
            m_Joint.anchor = Vector3.zero;
            m_Joint.connectedAnchor = Vector3.zero;
            m_Joint.xMotion = ConfigurableJointMotion.Limited;
            m_Joint.yMotion = ConfigurableJointMotion.Limited;
            m_Joint.zMotion = ConfigurableJointMotion.Limited;
            m_Joint.angularXMotion = ConfigurableJointMotion.Free;
            m_Joint.angularYMotion = ConfigurableJointMotion.Free;
            m_Joint.angularZMotion = ConfigurableJointMotion.Free;

            SoftJointLimit limit = m_Joint.linearLimit;
            limit.limit = m_DesiredDistance;
            limit.bounciness = 0f;
            m_Joint.linearLimit = limit;

            SoftJointLimitSpring spring = m_Joint.linearLimitSpring;
            spring.spring = 0f;
            spring.damper = 0f;
            m_Joint.linearLimitSpring = spring;
        }

        private void TeardownJoint()
        {
            if (m_Joint != null)
            {
                ConfigurableJoint joint = m_Joint;
                m_Joint = null;
                if (Application.isPlaying)
                    Destroy(joint);
                else
                    DestroyImmediate(joint);
            }
        }

        private void CleanupOrphanedJoints()
        {
            if (m_StartPoint != null)
            {
                Joint[] joints = m_StartPoint.GetComponents<Joint>();
                for (int i = joints.Length - 1; i >= 0; i--)
                {
                    if (Application.isPlaying)
                        Destroy(joints[i]);
                    else
                        DestroyImmediate(joints[i]);
                }
            }

            if (m_EndPoint != null)
            {
                Joint[] joints = m_EndPoint.GetComponents<Joint>();
                for (int i = joints.Length - 1; i >= 0; i--)
                {
                    if (Application.isPlaying)
                        Destroy(joints[i]);
                    else
                        DestroyImmediate(joints[i]);
                }
            }
        }

        private void EnsureRigidbodies()
        {
            if (m_StartPoint != null)
            {
                m_StartRigidbody = m_StartPoint.GetComponent<Rigidbody>();
                if (m_StartRigidbody == null)
                    m_StartRigidbody = m_StartPoint.gameObject.AddComponent<Rigidbody>();
            }

            if (m_EndPoint != null)
            {
                m_EndRigidbody = m_EndPoint.GetComponent<Rigidbody>();
                if (m_EndRigidbody == null)
                    m_EndRigidbody = m_EndPoint.gameObject.AddComponent<Rigidbody>();
            }
        }
    }
}
