using UnityEngine;

namespace MHZE.GearSystem
{
    [AddComponentMenu("Mechanical/Gear Look At")]
    public class GearLookAt : MonoBehaviour
    {
        [SerializeField] private Transform m_GearA;
        [SerializeField] private Transform m_GearB;
        [SerializeField] private GearAxis m_Axis = GearAxis.Y;

        public Transform gearA { get => m_GearA; set => m_GearA = value; }
        public Transform gearB { get => m_GearB; set => m_GearB = value; }
        public GearAxis axis { get => m_Axis; set => m_Axis = value; }

        public static GearLookAt Spawn(Transform gearA, Transform gearB, GearAxis axis)
        {
            GameObject go = new GameObject($"GearLookAt_{gearA.name}_{gearB.name}");
            go.transform.SetParent(null);

            GearLookAt lookAt = go.AddComponent<GearLookAt>();
            lookAt.m_GearA = gearA;
            lookAt.m_GearB = gearB;
            lookAt.m_Axis = axis;
            lookAt.ApplyLookAt();

            return lookAt;
        }

        private void Update()
        {
            ApplyLookAt();
        }

        private void ApplyLookAt()
        {
            if (m_GearA == null || m_GearB == null) return;

            transform.position = m_GearA.position;
            transform.rotation = m_GearA.rotation;

            Vector3 direction = (m_GearB.position - m_GearA.position).normalized;
            if (direction.sqrMagnitude < 0.0001f) return;

            switch (m_Axis)
            {
                case GearAxis.X:
                {
                    Vector3 targetDir = Vector3.ProjectOnPlane(direction, transform.right).normalized;
                    float angle = Vector3.SignedAngle(transform.forward, targetDir, transform.right);
                    transform.rotation = Quaternion.AngleAxis(angle, transform.right) * transform.rotation;
                    break;
                }
                case GearAxis.Y:
                {
                    Vector3 targetDir = Vector3.ProjectOnPlane(direction, transform.up).normalized;
                    float angle = Vector3.SignedAngle(transform.forward, targetDir, transform.up);
                    transform.rotation = Quaternion.AngleAxis(angle, transform.up) * transform.rotation;
                    break;
                }
                case GearAxis.Z:
                {
                    Vector3 targetDir = Vector3.ProjectOnPlane(direction, transform.forward).normalized;
                    float angle = Vector3.SignedAngle(transform.right, targetDir, transform.forward);
                    transform.rotation = Quaternion.AngleAxis(angle, transform.forward) * transform.rotation;
                    break;
                }
            }
        }
    }
}
