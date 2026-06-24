using UnityEngine;

namespace MHZE.CylinderCollider
{
    [ExecuteAlways]
    [AddComponentMenu("Physics/Cylinder Collider")]
    public class CylinderCollider : MonoBehaviour
    {
        [Header("Shape")]
        [SerializeField] private Vector3 m_Center = Vector3.zero;
        [SerializeField] private float m_Radius = 0.5f;
        [SerializeField] private float m_Height = 2f;
        [SerializeField] [Range(3, 64)] private int m_Sides = 16;
        [SerializeField] private int m_Direction = 1;

        [Header("Collision")]
        [SerializeField] private PhysicsMaterial m_Material;
        [SerializeField] private bool m_IsTrigger;

        private Vector3 m_PrevCenter;
        private float m_PrevRadius;
        private float m_PrevHeight;
        private int m_PrevSides;
        private int m_PrevDirection;

        private MeshCollider m_MeshCollider;
        private Mesh m_Mesh;

        public Vector3 center
        {
            get => m_Center;
            set { m_Center = value; DirtyAndRebuild(); }
        }

        public float radius
        {
            get => m_Radius;
            set { m_Radius = Mathf.Max(0.001f, value); DirtyAndRebuild(); }
        }

        public float height
        {
            get => m_Height;
            set { m_Height = Mathf.Max(0.001f, value); DirtyAndRebuild(); }
        }

        public int sides
        {
            get => m_Sides;
            set { m_Sides = Mathf.Clamp(value, 3, 64); DirtyAndRebuild(); }
        }

        public int direction
        {
            get => m_Direction;
            set { m_Direction = value; DirtyAndRebuild(); }
        }

        public PhysicsMaterial sharedMaterial
        {
            get => m_Material;
            set
            {
                m_Material = value;
                if (m_MeshCollider != null)
                    m_MeshCollider.sharedMaterial = m_Material;
            }
        }

        public bool isTrigger
        {
            get => m_IsTrigger;
            set
            {
                m_IsTrigger = value;
                if (m_MeshCollider != null)
                    m_MeshCollider.isTrigger = m_IsTrigger;
            }
        }

        public MeshCollider meshCollider => m_MeshCollider;

        private void OnEnable()
        {
            EnsureCollider();
            RebuildIfNeeded();
        }

        private void OnDisable()
        {
            if (m_MeshCollider != null)
                m_MeshCollider.enabled = false;
        }

        private void OnDestroy()
        {
            ReleaseMesh();
            if (m_MeshCollider != null)
            {
                if (Application.isPlaying)
                    Destroy(m_MeshCollider);
                else
                    DestroyImmediate(m_MeshCollider);
                m_MeshCollider = null;
            }
        }

        private void OnValidate()
        {
            m_Radius = Mathf.Max(0.001f, m_Radius);
            m_Height = Mathf.Max(0.001f, m_Height);
            m_Sides = Mathf.Clamp(m_Sides, 3, 64);

            if (!isActiveAndEnabled)
                return;

            EnsureCollider();
            RebuildIfNeeded();
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && ParametersChanged())
                Rebuild();
#endif
        }

        private void EnsureCollider()
        {
            if (m_MeshCollider != null)
            {
                m_MeshCollider.enabled = true;
                return;
            }

            m_MeshCollider = GetComponent<MeshCollider>();
            if (m_MeshCollider == null)
                m_MeshCollider = gameObject.AddComponent<MeshCollider>();

            m_MeshCollider.hideFlags = HideFlags.HideInInspector;
            m_MeshCollider.convex = true;
            m_MeshCollider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation;
            m_MeshCollider.enabled = true;
        }

        private void DirtyAndRebuild()
        {
            if (isActiveAndEnabled)
                Rebuild();
        }

        private void RebuildIfNeeded()
        {
            if (m_Mesh == null || ParametersChanged())
                Rebuild();
        }

        private bool ParametersChanged()
        {
            return m_PrevCenter != m_Center ||
                   !Mathf.Approximately(m_PrevRadius, m_Radius) ||
                   !Mathf.Approximately(m_PrevHeight, m_Height) ||
                   m_PrevSides != m_Sides ||
                   m_PrevDirection != m_Direction;
        }

        private void Rebuild()
        {
            if (m_MeshCollider == null)
                EnsureCollider();

            ReleaseMesh();
            m_Mesh = GenerateCylinderMesh();
            m_MeshCollider.sharedMesh = m_Mesh;
            m_MeshCollider.isTrigger = m_IsTrigger;
            m_MeshCollider.sharedMaterial = m_Material;

            m_PrevCenter = m_Center;
            m_PrevRadius = m_Radius;
            m_PrevHeight = m_Height;
            m_PrevSides = m_Sides;
            m_PrevDirection = m_Direction;
        }

        private void ReleaseMesh()
        {
            if (m_MeshCollider != null)
                m_MeshCollider.sharedMesh = null;

            if (m_Mesh != null)
            {
                if (Application.isPlaying)
                    Destroy(m_Mesh);
                else
                    DestroyImmediate(m_Mesh);
                m_Mesh = null;
            }
        }

        private static Mesh GenerateCylinderMesh(
            Vector3 center, float radius, float height, int sides, int direction)
        {
            var mesh = new Mesh { hideFlags = HideFlags.DontSave };
            mesh.name = "CylinderCollider_Mesh";

            int n = Mathf.Max(3, sides);
            var vertices = new Vector3[2 * n + 2];
            var triangles = new int[12 * n];

            Vector3 axis, basis1, basis2;
            switch (direction)
            {
                case 0:
                    axis = Vector3.right;
                    basis1 = Vector3.up;
                    basis2 = Vector3.forward;
                    break;
                case 2:
                    axis = Vector3.forward;
                    basis1 = Vector3.right;
                    basis2 = Vector3.up;
                    break;
                default:
                    axis = Vector3.up;
                    basis1 = Vector3.right;
                    basis2 = Vector3.forward;
                    break;
            }

            Vector3 halfAxis = axis * (height * 0.5f);

            vertices[0] = center - halfAxis;
            vertices[2 * n + 1] = center + halfAxis;

            for (int i = 0; i < n; i++)
            {
                float angle = 2f * Mathf.PI * i / n;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                Vector3 offset = basis1 * (cos * radius) + basis2 * (sin * radius);

                vertices[i + 1] = center + offset - halfAxis;
                vertices[i + 1 + n] = center + offset + halfAxis;
            }

            int t = 0;

            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                triangles[t++] = 0;
                triangles[t++] = i + 1;
                triangles[t++] = next + 1;
            }

            int topCenter = 2 * n + 1;
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                triangles[t++] = topCenter;
                triangles[t++] = next + 1 + n;
                triangles[t++] = i + 1 + n;
            }

            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                int bl = i + 1;
                int br = next + 1;
                int tl = i + 1 + n;
                int tr = next + 1 + n;

                triangles[t++] = bl;
                triangles[t++] = tl;
                triangles[t++] = br;
                triangles[t++] = tl;
                triangles[t++] = tr;
                triangles[t++] = br;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        private Mesh GenerateCylinderMesh()
        {
            return GenerateCylinderMesh(m_Center, m_Radius, m_Height, m_Sides, m_Direction);
        }

        private void DrawWireframe()
        {
            int n = Mathf.Min(m_Sides, 32);
            float halfH = m_Height * 0.5f;

            Vector3 axis;
            Vector3 b1;
            Vector3 b2;
            switch (m_Direction)
            {
                case 0:
                    axis = Vector3.right; b1 = Vector3.up; b2 = Vector3.forward;
                    break;
                case 2:
                    axis = Vector3.forward; b1 = Vector3.right; b2 = Vector3.up;
                    break;
                default:
                    axis = Vector3.up; b1 = Vector3.right; b2 = Vector3.forward;
                    break;
            }

            Vector3 topCenter = m_Center + axis * halfH;
            Vector3 botCenter = m_Center - axis * halfH;

            Vector3 prevTop = topCenter + b1 * m_Radius;
            Vector3 prevBot = botCenter + b1 * m_Radius;

            for (int i = 1; i <= n; i++)
            {
                float angle = 2f * Mathf.PI * i / n;
                Vector3 dir = b1 * Mathf.Cos(angle) + b2 * Mathf.Sin(angle);

                Vector3 currTop = topCenter + dir * m_Radius;
                Vector3 currBot = botCenter + dir * m_Radius;

                Gizmos.DrawLine(prevTop, currTop);
                Gizmos.DrawLine(prevBot, currBot);
                Gizmos.DrawLine(prevTop, prevBot);

                prevTop = currTop;
                prevBot = currBot;
            }
        }

        private void OnDrawGizmosSelected()
        {
            var prevColor = Gizmos.color;
            var prevMatrix = Gizmos.matrix;

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0f, 0.8f, 0.2f, 0.6f);

            DrawWireframe();

            Gizmos.matrix = prevMatrix;
            Gizmos.color = prevColor;
        }
    }
}
