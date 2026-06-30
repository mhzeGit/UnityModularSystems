using System.Collections.Generic;
using UnityEngine;

namespace MHZE.CylinderCollider
{
    [ExecuteAlways]
    [AddComponentMenu("Physics/Cylinder Collider")]
    public class CylinderCollider : MonoBehaviour
    {
        [SerializeField] private Vector3 m_Center = Vector3.zero;
        [SerializeField] private float m_Radius = 0.5f;
        [SerializeField] private float m_InnerRadius = 0f;
        [SerializeField] private float m_Height = 2f;
        [SerializeField] [Range(3, 64)] private int m_Sides = 16;
        [SerializeField] private int m_Direction = 1;

        [SerializeField] private PhysicsMaterial m_Material;
        [SerializeField] private bool m_IsTrigger;
        [SerializeField] private bool m_ProvidesContacts;
        [SerializeField] private int m_LayerOverridePriority;
        [SerializeField] private LayerMask m_IncludeLayers;
        [SerializeField] private LayerMask m_ExcludeLayers;

        private Vector3 m_PrevCenter;
        private float m_PrevRadius;
        private float m_PrevInnerRadius;
        private float m_PrevHeight;
        private int m_PrevSides;
        private int m_PrevDirection;

#if UNITY_EDITOR
        private bool m_PendingOnValidateRebuild;
#endif

        private MeshCollider m_MeshCollider;
        private List<MeshCollider> m_SegmentColliders;
        private Mesh m_Mesh;

        public Vector3 center
        {
            get => m_Center;
            set { m_Center = value; DirtyAndRebuild(); }
        }

        public float radius
        {
            get => m_Radius;
            set { m_Radius = Mathf.Max(0.001f, value); m_InnerRadius = Mathf.Min(m_InnerRadius, m_Radius - 0.001f); DirtyAndRebuild(); }
        }

        public float innerRadius
        {
            get => m_InnerRadius;
            set { m_InnerRadius = Mathf.Clamp(value, 0f, m_Radius - 0.001f); DirtyAndRebuild(); }
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
                foreach (var c in GetAllColliders())
                    c.sharedMaterial = m_Material;
            }
        }

        public bool isTrigger
        {
            get => m_IsTrigger;
            set
            {
                m_IsTrigger = value;
                foreach (var c in GetAllColliders())
                    c.isTrigger = m_IsTrigger;
            }
        }

        public bool providesContacts
        {
            get => m_ProvidesContacts;
            set
            {
                m_ProvidesContacts = value;
                foreach (var c in GetAllColliders())
                    c.providesContacts = m_ProvidesContacts;
            }
        }

        public int layerOverridePriority
        {
            get => m_LayerOverridePriority;
            set
            {
                m_LayerOverridePriority = value;
                foreach (var c in GetAllColliders())
                    c.layerOverridePriority = m_LayerOverridePriority;
            }
        }

        public LayerMask includeLayers
        {
            get => m_IncludeLayers;
            set
            {
                m_IncludeLayers = value;
                foreach (var c in GetAllColliders())
                    c.includeLayers = m_IncludeLayers;
            }
        }

        public LayerMask excludeLayers
        {
            get => m_ExcludeLayers;
            set
            {
                m_ExcludeLayers = value;
                foreach (var c in GetAllColliders())
                    c.excludeLayers = m_ExcludeLayers;
            }
        }

        public MeshCollider meshCollider
        {
            get
            {
                if (m_MeshCollider != null)
                    return m_MeshCollider;
                if (m_SegmentColliders != null && m_SegmentColliders.Count > 0)
                    return m_SegmentColliders[0];
                return null;
            }
        }

        private IEnumerable<MeshCollider> GetAllColliders()
        {
            if (m_MeshCollider != null)
                yield return m_MeshCollider;
            if (m_SegmentColliders != null)
                foreach (var c in m_SegmentColliders)
                    if (c != null)
                        yield return c;
        }

        private void CopyPropertiesTo(MeshCollider collider)
        {
            collider.sharedMaterial = m_Material;
            collider.isTrigger = m_IsTrigger;
            collider.providesContacts = m_ProvidesContacts;
            collider.layerOverridePriority = m_LayerOverridePriority;
            collider.includeLayers = m_IncludeLayers;
            collider.excludeLayers = m_ExcludeLayers;
        }

        private void Reset()
        {
            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                var bounds = meshFilter.sharedMesh.bounds;

                m_Center = bounds.center;

                float rX = bounds.extents.x;
                float rY = bounds.extents.y;
                float rZ = bounds.extents.z;

                switch (m_Direction)
                {
                    case 0:
                        m_Height = bounds.size.x;
                        m_Radius = Mathf.Max(rY, rZ);
                        break;
                    case 2:
                        m_Height = bounds.size.z;
                        m_Radius = Mathf.Max(rX, rY);
                        break;
                    default:
                        m_Height = bounds.size.y;
                        m_Radius = Mathf.Max(rX, rZ);
                        break;
                }

                m_Radius = Mathf.Max(0.001f, m_Radius);
                m_Height = Mathf.Max(0.001f, m_Height);
            }
        }

        private void Awake()
        {
            Rebuild();
        }

        private void OnEnable()
        {
            bool enabledAny = false;
            foreach (var c in GetAllColliders())
            {
                if (c != null)
                {
                    c.enabled = true;
                    enabledAny = true;
                }
            }

            if (!enabledAny)
                Rebuild();
        }

        private void OnDisable()
        {
            foreach (var c in GetAllColliders())
            {
                if (c != null)
                    c.enabled = false;
            }
        }

        private void OnDestroy()
        {
            ReleaseMesh();
            DestroySegmentColliders();
            m_MeshCollider = null;
        }

        private void OnValidate()
        {
            m_Radius = Mathf.Max(0.001f, m_Radius);
            m_InnerRadius = Mathf.Clamp(m_InnerRadius, 0f, m_Radius - 0.001f);
            m_Height = Mathf.Max(0.001f, m_Height);
            m_Sides = Mathf.Clamp(m_Sides, 3, 64);

            if (!isActiveAndEnabled)
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying && !m_PendingOnValidateRebuild)
            {
                m_PendingOnValidateRebuild = true;
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    m_PendingOnValidateRebuild = false;
                    if (this == null || !isActiveAndEnabled)
                        return;
                    Rebuild();
                };
            }
#else
            Rebuild();
#endif
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && ParametersChanged())
                Rebuild();
#endif
        }

        private void GetBasisVectors(out Vector3 axis, out Vector3 basis1, out Vector3 basis2)
        {
            switch (m_Direction)
            {
                case 0: axis = Vector3.right; basis1 = Vector3.up; basis2 = Vector3.forward; break;
                case 2: axis = Vector3.forward; basis1 = Vector3.right; basis2 = Vector3.up; break;
                default: axis = Vector3.up; basis1 = Vector3.right; basis2 = Vector3.forward; break;
            }
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
                   !Mathf.Approximately(m_PrevInnerRadius, m_InnerRadius) ||
                   !Mathf.Approximately(m_PrevHeight, m_Height) ||
                   m_PrevSides != m_Sides ||
                   m_PrevDirection != m_Direction;
        }

        private void Rebuild()
        {
            ReleaseMesh();
            DestroySegmentColliders();

            if (m_MeshCollider != null)
            {
                if (Application.isPlaying)
                    Destroy(m_MeshCollider);
                else
                    DestroyImmediate(m_MeshCollider);
                m_MeshCollider = null;
            }

            var cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation
                | MeshColliderCookingOptions.EnableMeshCleaning
                | MeshColliderCookingOptions.WeldColocatedVertices;

            if (m_InnerRadius <= 0f)
            {
                m_Mesh = GenerateCylinderMesh();
                m_MeshCollider = gameObject.AddComponent<MeshCollider>();
                m_MeshCollider.hideFlags = HideFlags.HideInInspector | HideFlags.DontSaveInEditor;
                m_MeshCollider.convex = true;
                m_MeshCollider.cookingOptions = cookingOptions;
                m_MeshCollider.sharedMesh = m_Mesh;
                CopyPropertiesTo(m_MeshCollider);
                m_MeshCollider.enabled = isActiveAndEnabled;
            }
            else
            {
                int n = Mathf.Max(3, m_Sides);
                m_SegmentColliders = new List<MeshCollider>(n);

                Vector3 axis, basis1, basis2;
                GetBasisVectors(out axis, out basis1, out basis2);
                Vector3 halfAxis = axis * (m_Height * 0.5f);

                for (int seg = 0; seg < n; seg++)
                {
                    float angle0 = 2f * Mathf.PI * seg / n;
                    float angle1 = 2f * Mathf.PI * (seg + 1) / n;

                    var mesh = GenerateSegmentMesh(m_Center, m_Radius, m_InnerRadius,
                        halfAxis, basis1, basis2, angle0, angle1);

                    var segGO = new GameObject($"Segment_{seg}");
                    segGO.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector | HideFlags.DontSave;
                    segGO.transform.SetParent(transform);
                    segGO.transform.localPosition = Vector3.zero;
                    segGO.transform.localRotation = Quaternion.identity;
                    segGO.transform.localScale = Vector3.one;

                    var mc = segGO.AddComponent<MeshCollider>();
                    mc.hideFlags = HideFlags.HideInInspector | HideFlags.DontSave;
                    mc.convex = true;
                    mc.cookingOptions = cookingOptions;
                    mc.sharedMesh = mesh;
                    CopyPropertiesTo(mc);
                    mc.enabled = true;

                    m_SegmentColliders.Add(mc);
                }
            }

            m_PrevCenter = m_Center;
            m_PrevRadius = m_Radius;
            m_PrevInnerRadius = m_InnerRadius;
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

        private void DestroySegmentColliders()
        {
            if (m_SegmentColliders == null)
                return;

            foreach (var c in m_SegmentColliders)
            {
                if (c == null)
                    continue;
                if (c.sharedMesh != null)
                {
                    if (Application.isPlaying)
                        Destroy(c.sharedMesh);
                    else
                        DestroyImmediate(c.sharedMesh);
                }
                var go = c.gameObject;
                if (go != null)
                {
                    if (Application.isPlaying)
                        Destroy(go);
                    else
                        DestroyImmediate(go);
                }
            }

            m_SegmentColliders = null;
        }

        private static Mesh GenerateCylinderMesh(
            Vector3 center, float radius, float height, int sides, int direction)
        {
            var mesh = new Mesh { hideFlags = HideFlags.DontSave };
            mesh.name = "CylinderCollider_Mesh";

            int n = Mathf.Max(3, sides);

            Vector3 axis, basis1, basis2;
            switch (direction)
            {
                case 0: axis = Vector3.right; basis1 = Vector3.up; basis2 = Vector3.forward; break;
                case 2: axis = Vector3.forward; basis1 = Vector3.right; basis2 = Vector3.up; break;
                default: axis = Vector3.up; basis1 = Vector3.right; basis2 = Vector3.forward; break;
            }

            Vector3 halfAxis = axis * (height * 0.5f);

            var vertices = new Vector3[2 * n + 2];
            var triangles = new int[12 * n];

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

        private static Mesh GenerateSegmentMesh(
            Vector3 center, float radius, float innerRadius,
            Vector3 halfAxis, Vector3 basis1, Vector3 basis2,
            float angle0, float angle1)
        {
            var mesh = new Mesh { hideFlags = HideFlags.DontSave };

            float cos0 = Mathf.Cos(angle0), sin0 = Mathf.Sin(angle0);
            float cos1 = Mathf.Cos(angle1), sin1 = Mathf.Sin(angle1);

            Vector3 o0 = basis1 * (cos0 * radius) + basis2 * (sin0 * radius);
            Vector3 o1 = basis1 * (cos1 * radius) + basis2 * (sin1 * radius);
            Vector3 i0 = basis1 * (cos0 * innerRadius) + basis2 * (sin0 * innerRadius);
            Vector3 i1 = basis1 * (cos1 * innerRadius) + basis2 * (sin1 * innerRadius);

            var vertices = new Vector3[8];
            var triangles = new int[36];

            vertices[0] = center + o0 - halfAxis;
            vertices[1] = center + o1 - halfAxis;
            vertices[2] = center + i0 - halfAxis;
            vertices[3] = center + i1 - halfAxis;
            vertices[4] = center + o0 + halfAxis;
            vertices[5] = center + o1 + halfAxis;
            vertices[6] = center + i0 + halfAxis;
            vertices[7] = center + i1 + halfAxis;

            int tri = 0;

            triangles[tri++] = 0; triangles[tri++] = 1; triangles[tri++] = 3;
            triangles[tri++] = 0; triangles[tri++] = 3; triangles[tri++] = 2;

            triangles[tri++] = 4; triangles[tri++] = 6; triangles[tri++] = 7;
            triangles[tri++] = 4; triangles[tri++] = 7; triangles[tri++] = 5;

            triangles[tri++] = 0; triangles[tri++] = 4; triangles[tri++] = 1;
            triangles[tri++] = 4; triangles[tri++] = 5; triangles[tri++] = 1;

            triangles[tri++] = 2; triangles[tri++] = 3; triangles[tri++] = 7;
            triangles[tri++] = 2; triangles[tri++] = 7; triangles[tri++] = 6;

            triangles[tri++] = 0; triangles[tri++] = 2; triangles[tri++] = 4;
            triangles[tri++] = 2; triangles[tri++] = 6; triangles[tri++] = 4;

            triangles[tri++] = 1; triangles[tri++] = 5; triangles[tri++] = 7;
            triangles[tri++] = 1; triangles[tri++] = 7; triangles[tri++] = 3;

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            return mesh;
        }

        private static void DrawCircle(Vector3 center, Vector3 b1, Vector3 b2, float radius, int segments)
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

        private void DrawWireframe()
        {
            float halfH = m_Height * 0.5f;

            Vector3 axis, b1, b2;
            GetBasisVectors(out axis, out b1, out b2);

            Vector3 topCenter = m_Center + axis * halfH;
            Vector3 botCenter = m_Center - axis * halfH;

            int segments = Mathf.Min(m_Sides, 32);
            DrawCircle(topCenter, b1, b2, m_Radius, segments);
            DrawCircle(botCenter, b1, b2, m_Radius, segments);

            if (m_InnerRadius > 0f)
            {
                DrawCircle(topCenter, b1, b2, m_InnerRadius, segments);
                DrawCircle(botCenter, b1, b2, m_InnerRadius, segments);
            }

            for (int i = 0; i < m_Sides; i++)
            {
                float angle = 2f * Mathf.PI * i / m_Sides;
                Vector3 dir = b1 * Mathf.Cos(angle) + b2 * Mathf.Sin(angle);
                Gizmos.DrawLine(topCenter + dir * m_Radius, botCenter + dir * m_Radius);
                if (m_InnerRadius > 0f)
                {
                    Gizmos.DrawLine(topCenter + dir * m_InnerRadius, botCenter + dir * m_InnerRadius);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            var prevColor = Gizmos.color;
            var prevMatrix = Gizmos.matrix;

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0f, 0.8f, 0.2f, 0.8f);

            DrawWireframe();

            Gizmos.matrix = prevMatrix;
            Gizmos.color = prevColor;
        }
    }
}
