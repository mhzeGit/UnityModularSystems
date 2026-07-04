using UnityEngine;

namespace MHZE.RopePhysics
{
    [ExecuteAlways]
    [AddComponentMenu("Physics/Rope")]
    public class Rope : MonoBehaviour
    {
        [SerializeField] private Transform m_StartPoint;
        [SerializeField] private Transform m_EndPoint;

        private LineRenderer m_LineRenderer;
        private int m_PrevSegmentCount;

        private void Awake()
        {
            m_LineRenderer = GetComponent<LineRenderer>();
            if (m_LineRenderer == null)
                m_LineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        private void Update()
        {
            if (m_StartPoint == null || m_EndPoint == null)
                return;

            m_LineRenderer.positionCount = 2;
            m_LineRenderer.SetPosition(0, m_StartPoint.position);
            m_LineRenderer.SetPosition(1, m_EndPoint.position);
        }
    }
}
