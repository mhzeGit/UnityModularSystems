using System.Collections.Generic;
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
        [SerializeField] private LayerMask m_CollisionMask = -1;
        [SerializeField] private int m_MaxContactPoints = 10;
        [SerializeField] private float m_SurfaceOffset = 0.01f;

        private LineRenderer m_LineRenderer;
        [System.NonSerialized] private List<Transform> m_Points = new List<Transform>();
        [System.NonSerialized] private List<ConfigurableJoint> m_Joints = new List<ConfigurableJoint>();
        [System.NonSerialized] private List<GameObject> m_CreatedContactObjects = new List<GameObject>();
        private bool m_NeedsRebuild;

        private void Awake()
        {
            m_LineRenderer = GetComponent<LineRenderer>();
            if (m_LineRenderer == null)
                m_LineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        private void OnEnable()
        {
            if (m_StartPoint == null && m_EndPoint == null)
                return;
            CleanupOrphanedJoints();
            if (m_StartPoint != null)
                EnsureRigidbody(m_StartPoint);
            if (m_EndPoint != null)
                EnsureRigidbody(m_EndPoint);
        }

        private void OnDisable()
        {
            Cleanup();
        }

        private void OnValidate()
        {
            m_NeedsRebuild = true;
        }

        private void Update()
        {
            if (m_StartPoint == null || m_EndPoint == null || m_StartPoint == m_EndPoint)
            {
                Cleanup();
                return;
            }

            DetectCollisions();
            UpdateVisuals();

            if (m_NeedsRebuild)
                RebuildJoints();
        }

        public void ResetContactPoints()
        {
            DestroyAllContactPoints();
            m_Points.Clear();
            m_Points.Add(m_StartPoint);
            m_Points.Add(m_EndPoint);
            m_NeedsRebuild = true;
        }

        private void DetectCollisions()
        {
            if (m_Points.Count < 2 ||
                m_Points[0] != m_StartPoint ||
                m_Points[m_Points.Count - 1] != m_EndPoint)
            {
                DestroyAllContactPoints();
                m_Points.Clear();
                m_Points.Add(m_StartPoint);
                m_Points.Add(m_EndPoint);
                m_NeedsRebuild = true;
            }

            bool changed = false;
            int maxIter = Mathf.Min(m_MaxContactPoints, 20);

            for (int iter = 0; iter < maxIter; iter++)
            {
                bool inserted = false;
                for (int i = 0; i < m_Points.Count - 1 && !inserted; i++)
                {
                    Transform segStart = m_Points[i];
                    Transform segEnd = m_Points[i + 1];

                    Vector3 direction = (segEnd.position - segStart.position).normalized;
                    float distance = Vector3.Distance(segStart.position, segEnd.position);

                    if (distance < 0.001f) continue;

                    if (Physics.Raycast(segStart.position, direction, out RaycastHit hit, distance, m_CollisionMask))
                    {
                        bool exists = false;
                        for (int j = 0; j < m_CreatedContactObjects.Count; j++)
                        {
                            GameObject contactObj = m_CreatedContactObjects[j];
                            if (contactObj == null) continue;
                            if (Vector3.Distance(contactObj.transform.position, hit.point) < 0.1f)
                            {
                                Vector3 contactPos = hit.point + hit.normal * m_SurfaceOffset;
                                contactObj.transform.position = contactPos;
                                exists = true;
                                break;
                            }
                        }

                        if (!exists)
                        {
                            Vector3 contactPos = hit.point + hit.normal * m_SurfaceOffset;
                            GameObject go = new GameObject("Rope Contact Point");
                            go.transform.position = contactPos;
                            go.hideFlags = HideFlags.DontSave;
                            Rigidbody rb = go.AddComponent<Rigidbody>();
                            rb.isKinematic = true;
                            rb.useGravity = false;
                            m_CreatedContactObjects.Add(go);
                            m_Points.Insert(i + 1, go.transform);
                            changed = true;
                            inserted = true;
                        }
                    }
                }
                if (!inserted) break;
            }

            if (changed)
                m_NeedsRebuild = true;
        }

        private void UpdateVisuals()
        {
            m_LineRenderer.positionCount = m_Points.Count;
            for (int i = 0; i < m_Points.Count; i++)
            {
                if (m_Points[i] != null)
                    m_LineRenderer.SetPosition(i, m_Points[i].position);
            }
        }

        private void RebuildJoints()
        {
            foreach (var joint in m_Joints)
            {
                if (joint != null)
                {
                    if (Application.isPlaying)
                        Destroy(joint);
                    else
                        DestroyImmediate(joint);
                }
            }
            m_Joints.Clear();

            if (m_Points.Count < 2)
            {
                m_NeedsRebuild = false;
                return;
            }

            for (int i = 0; i < m_Points.Count; i++)
            {
                EnsureRigidbody(m_Points[i]);
            }

            for (int i = 0; i < m_Points.Count - 1; i++)
            {
                Transform start = m_Points[i];
                Transform end = m_Points[i + 1];

                Rigidbody startRb = start.GetComponent<Rigidbody>();
                Rigidbody endRb = end.GetComponent<Rigidbody>();
                if (startRb == null || endRb == null || startRb == endRb) continue;

                ConfigurableJoint joint = start.gameObject.AddComponent<ConfigurableJoint>();
                joint.connectedBody = endRb;
                joint.autoConfigureConnectedAnchor = false;
                joint.anchor = Vector3.zero;
                joint.connectedAnchor = Vector3.zero;
                joint.xMotion = ConfigurableJointMotion.Limited;
                joint.yMotion = ConfigurableJointMotion.Limited;
                joint.zMotion = ConfigurableJointMotion.Limited;
                joint.angularXMotion = ConfigurableJointMotion.Free;
                joint.angularYMotion = ConfigurableJointMotion.Free;
                joint.angularZMotion = ConfigurableJointMotion.Free;

                SoftJointLimit limit = joint.linearLimit;
                limit.limit = m_DesiredDistance;
                limit.bounciness = 0f;
                joint.linearLimit = limit;

                SoftJointLimitSpring spring = joint.linearLimitSpring;
                spring.spring = 0f;
                spring.damper = 0f;
                joint.linearLimitSpring = spring;

                m_Joints.Add(joint);
            }

            m_NeedsRebuild = false;
        }

        private static void EnsureRigidbody(Transform point)
        {
            if (point == null) return;
            if (point.GetComponent<Rigidbody>() == null)
                point.gameObject.AddComponent<Rigidbody>();
        }

        private void CleanupOrphanedJoints()
        {
            Transform[] targets = new Transform[] { m_StartPoint, m_EndPoint };
            foreach (var t in targets)
            {
                if (t == null) continue;
                Joint[] joints = t.GetComponents<Joint>();
                for (int i = joints.Length - 1; i >= 0; i--)
                {
                    if (Application.isPlaying)
                        Destroy(joints[i]);
                    else
                        DestroyImmediate(joints[i]);
                }
            }
        }

        private void DestroyAllContactPoints()
        {
            for (int i = m_CreatedContactObjects.Count - 1; i >= 0; i--)
            {
                if (m_CreatedContactObjects[i] != null)
                {
                    if (Application.isPlaying)
                        Destroy(m_CreatedContactObjects[i]);
                    else
                        DestroyImmediate(m_CreatedContactObjects[i]);
                }
            }
            m_CreatedContactObjects.Clear();
        }

        private void Cleanup()
        {
            foreach (var joint in m_Joints)
            {
                if (joint != null)
                {
                    if (Application.isPlaying)
                        Destroy(joint);
                    else
                        DestroyImmediate(joint);
                }
            }
            m_Joints.Clear();

            DestroyAllContactPoints();
            m_Points.Clear();
        }
    }
}
