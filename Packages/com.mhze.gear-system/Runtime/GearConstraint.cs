using UnityEngine;

namespace MHZE.GearSystem
{
    public enum GearAxis { X, Y, Z }

    [ExecuteAlways]
    [AddComponentMenu("Mechanical/Gear Constraint")]
    public class GearConstraint : MonoBehaviour
    {
        [Header("Gear A")]
        [SerializeField] private Transform m_GearA;
        [SerializeField] private float m_RadiusA = 0.5f;
        [SerializeField] private GearAxis m_AxisA = GearAxis.Y;
        [SerializeField] private Transform m_MeshA;

        [Header("Gear B")]
        [SerializeField] private Transform m_GearB;
        [SerializeField] private float m_RadiusB = 0.5f;
        [SerializeField] private GearAxis m_AxisB = GearAxis.Y;
        [SerializeField] private Transform m_MeshB;

        [Header("Visual")]
        [SerializeField] private float m_ToothDensity = 5f;
        [SerializeField] private float m_ToothHeight = 0.1f;
        [SerializeField] private bool m_DebugDraw;

        [Header("Limit")]
        [SerializeField]
        [Tooltip("Maximum constraint torque (Nm). 0 = unlimited.")]
        private float m_MaxTorque = 0f;

        [SerializeField]
        [Tooltip("Log debug values to console when enabled.")]
        private bool m_DebugLog;

        private Quaternion m_PrevWorldRotA;
        private Quaternion m_PrevWorldRotB;
        private bool m_HasInitialized;

        public Transform gearA { get => m_GearA; set => m_GearA = value; }
        public Transform gearB { get => m_GearB; set => m_GearB = value; }
        public Transform meshA { get => m_MeshA; set => m_MeshA = value; }
        public Transform meshB { get => m_MeshB; set => m_MeshB = value; }
        public GearAxis axisA { get => m_AxisA; set => m_AxisA = value; }
        public GearAxis axisB { get => m_AxisB; set => m_AxisB = value; }
        public float radiusA { get => m_RadiusA; set => m_RadiusA = value; }
        public float radiusB { get => m_RadiusB; set => m_RadiusB = value; }
        public float toothDensity { get => m_ToothDensity; set => m_ToothDensity = value; }
        public float toothHeight { get => m_ToothHeight; set => m_ToothHeight = value; }
        public bool debugDraw { get => m_DebugDraw; set => m_DebugDraw = value; }
        public float maxTorque { get => m_MaxTorque; set => m_MaxTorque = value; }
        public bool debugLog { get => m_DebugLog; set => m_DebugLog = value; }

        public static Vector3 GetAxisVector(GearAxis axis)
        {
            return axis switch
            {
                GearAxis.X => Vector3.right,
                GearAxis.Z => Vector3.forward,
                _ => Vector3.up
            };
        }

        private static void GetReferenceAxes(GearAxis axis, out Vector3 axisDir, out Vector3 b1, out Vector3 b2)
        {
            switch (axis)
            {
                case GearAxis.X:
                    axisDir = Vector3.right; b1 = Vector3.up; b2 = Vector3.forward;
                    break;
                case GearAxis.Z:
                    axisDir = Vector3.forward; b1 = Vector3.right; b2 = Vector3.up;
                    break;
                default:
                    axisDir = Vector3.up; b1 = Vector3.right; b2 = Vector3.forward;
                    break;
            }
        }

        private void OnDrawGizmos()
        {
            if (!m_DebugDraw) return;
            GearConstraintDebugger.Draw(this);
        }

        public static void DrawGearGizmo(Transform gearTransform, Transform meshTransform, float radius, GearAxis axis, float rotationOffset, float toothDensity, float toothHeight)
        {
            if (gearTransform == null || radius <= 0f) return;

            GetReferenceAxes(axis, out Vector3 axisDir, out Vector3 b1, out Vector3 b2);

            float outerRadius = radius + toothHeight;
            float halfDepth = 0.05f;
            int toothCount = Mathf.RoundToInt(2f * Mathf.PI * radius * toothDensity);
            if (toothCount % 2 != 0) toothCount++;
            toothCount = Mathf.Max(4, toothCount);
            int segments = toothCount * 2;

            Vector3 topCenter = axisDir * halfDepth;
            Vector3 botCenter = -axisDir * halfDepth;

            Color bodyColor = new Color(0.3f, 0.6f, 1f, 0.8f);
            Color toothColor = new Color(0.2f, 0.5f, 0.9f, 0.8f);

            var prevMatrix = Gizmos.matrix;
            Quaternion offsetRot = Quaternion.AngleAxis(rotationOffset, axisDir);
            Gizmos.matrix = gearTransform.localToWorldMatrix * Matrix4x4.Rotate(offsetRot);

            Gizmos.color = bodyColor;
            DrawGizmoCircle(topCenter, b1, b2, radius, segments);
            DrawGizmoCircle(botCenter, b1, b2, radius, segments);

            for (int i = 0; i < segments; i += 4)
            {
                float angle = 2f * Mathf.PI * i / segments;
                Vector3 dir = b1 * Mathf.Cos(angle) + b2 * Mathf.Sin(angle);
                Gizmos.DrawLine(topCenter + dir * radius, botCenter + dir * radius);
            }

            Gizmos.color = toothColor;
            float toothHalfWidth = Mathf.PI / toothCount * 0.35f;

            for (int i = 0; i < toothCount; i++)
            {
                float angle = 2f * Mathf.PI * i / toothCount;

                Vector3 dirLeft = b1 * Mathf.Cos(angle - toothHalfWidth) + b2 * Mathf.Sin(angle - toothHalfWidth);
                Vector3 dirRight = b1 * Mathf.Cos(angle + toothHalfWidth) + b2 * Mathf.Sin(angle + toothHalfWidth);

                Vector3 tlInner = topCenter + dirLeft * radius;
                Vector3 tlOuter = topCenter + dirLeft * outerRadius;
                Vector3 trInner = topCenter + dirRight * radius;
                Vector3 trOuter = topCenter + dirRight * outerRadius;

                Vector3 blInner = botCenter + dirLeft * radius;
                Vector3 blOuter = botCenter + dirLeft * outerRadius;
                Vector3 brInner = botCenter + dirRight * radius;
                Vector3 brOuter = botCenter + dirRight * outerRadius;

                Gizmos.DrawLine(tlInner, tlOuter);
                Gizmos.DrawLine(tlOuter, trOuter);
                Gizmos.DrawLine(trOuter, trInner);

                Gizmos.DrawLine(blInner, blOuter);
                Gizmos.DrawLine(blOuter, brOuter);
                Gizmos.DrawLine(brOuter, brInner);

                Gizmos.DrawLine(tlOuter, blOuter);
                Gizmos.DrawLine(trOuter, brOuter);
                Gizmos.DrawLine(tlInner, blInner);
            }
            Gizmos.matrix = prevMatrix;
        }

        private static void DrawGizmoCircle(Vector3 center, Vector3 b1, Vector3 b2, float radius, int segments)
        {
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

        private void OnEnable()
        {
            TryInitialize();
        }

        private void OnDisable()
        {
            m_HasInitialized = false;
        }

        private void TryInitialize()
        {
            if (m_GearA != null && m_GearB != null && m_RadiusA > 0f && m_RadiusB > 0f)
            {
                m_PrevWorldRotA = m_GearA.rotation;
                m_PrevWorldRotB = m_GearB.rotation;
                m_HasInitialized = true;
            }
        }

        private void LateUpdate()
        {
            if (!m_HasInitialized)
            {
                TryInitialize();
                return;
            }

            if (m_GearA == null || m_GearB == null) return;
            if (m_RadiusA <= 0f || m_RadiusB <= 0f) return;

            Quaternion worldA = m_GearA.rotation;
            Quaternion worldB = m_GearB.rotation;

            Quaternion deltaRotA = worldA * Quaternion.Inverse(m_PrevWorldRotA);
            Quaternion deltaRotB = worldB * Quaternion.Inverse(m_PrevWorldRotB);

            Vector3 axisDirA = GetWorldAxis(m_GearA, m_AxisA);
            Vector3 axisDirB = GetWorldAxis(m_GearB, m_AxisB);

            float deltaA = ExtractSignedAngle(deltaRotA, axisDirA);
            float deltaB = ExtractSignedAngle(deltaRotB, axisDirB);

            float ratio = m_RadiusA / m_RadiusB;
            float threshold = 0.001f;

            float scaledA = deltaA * m_RadiusA;
            float scaledB = deltaB * m_RadiusB;

            if (Mathf.Abs(scaledA) > Mathf.Abs(scaledB) && Mathf.Abs(scaledA) > threshold)
            {
                Quaternion applyB = Quaternion.AngleAxis(-deltaA * ratio, axisDirB);
                m_GearB.rotation = applyB * worldB;

                if (m_DebugLog)
                    Debug.Log(
                        $"[GearConstraint] A→B  ΔA={deltaA:F3}°  ΔB={deltaB:F3}°  " +
                        $"ratio={ratio:F4}  scaledA={scaledA:F4}  scaledB={scaledB:F4}",
                        this);
            }
            else if (Mathf.Abs(scaledB) > threshold)
            {
                Quaternion applyA = Quaternion.AngleAxis(-deltaB / ratio, axisDirA);
                m_GearA.rotation = applyA * worldA;

                if (m_DebugLog)
                    Debug.Log(
                        $"[GearConstraint] B→A  ΔA={deltaA:F3}°  ΔB={deltaB:F3}°  " +
                        $"ratio={ratio:F4}  scaledA={scaledA:F4}  scaledB={scaledB:F4}",
                        this);
            }

            m_PrevWorldRotA = m_GearA.rotation;
            m_PrevWorldRotB = m_GearB.rotation;
        }

        private static Vector3 GetWorldAxis(Transform t, GearAxis axis)
        {
            return axis switch
            {
                GearAxis.X => t.right,
                GearAxis.Y => t.up,
                GearAxis.Z => t.forward,
                _ => t.up
            };
        }

        private static float ExtractSignedAngle(Quaternion delta, Vector3 axis)
        {
            delta.ToAngleAxis(out float angle, out Vector3 rotationAxis);
            float sign = Mathf.Sign(Vector3.Dot(rotationAxis, axis));
            return angle * sign;
        }
    }
}
