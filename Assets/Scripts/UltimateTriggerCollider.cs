using UnityEngine;
using UnityEngine.Events;

namespace MHZE
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public class UltimateTriggerCollider : MonoBehaviour
    {
        [Header("Tag Filtering")]
        [SerializeField] private string[] _targetTags = { "Player" };

        [Header("Trigger Events")]
        [SerializeField] private bool _useOnEnter = true;
        [SerializeField] private bool _useOnStay = true;
        [SerializeField] private bool _useOnExit = true;
        public ColliderUnityEvent OnEnter;
        public ColliderUnityEvent OnStay;
        public ColliderUnityEvent OnExit;

        public event System.Action<Collider> OnTriggerEntered;
        public event System.Action<Collider> OnTriggerStayed;
        public event System.Action<Collider> OnTriggerExited;

        [Header("Debug")]
        [SerializeField] private bool _showDebugPreview;

        private Collider _collider;
        private Rigidbody _rigidbody;
        private Mesh _fillMesh;
        private Mesh _wireframeMesh;
        private Material _fillMaterial;
        private Material _wireframeMaterial;
        private Vector3 _debugScale;
        private Vector3 _debugPosition;

        private void Reset()
        {
            if (!TryGetComponent(out Collider hitCollider))
                hitCollider = gameObject.AddComponent<BoxCollider>();

            hitCollider.isTrigger = true;

            if (!TryGetComponent(out Rigidbody rb))
                rb = gameObject.AddComponent<Rigidbody>();

            rb.isKinematic = true;
            rb.useGravity = false;
            rb.hideFlags = HideFlags.NotEditable;
        }

        private void Awake()
        {
            CacheComponents();

            _collider.isTrigger = true;
            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = false;
        }

        private void Start()
        {
            if (_showDebugPreview)
                GenerateDebugVisuals();
        }

        private void OnDestroy()
        {
            DestroyDebugVisuals();
        }

        private void CacheComponents()
        {
            _collider = GetComponent<Collider>();
            _rigidbody = GetComponent<Rigidbody>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_useOnEnter) return;
            if (!IsTagValid(other)) return;
            OnTriggerEntered?.Invoke(other);
            OnEnter?.Invoke(other);
        }

        private void OnTriggerStay(Collider other)
        {
            if (!_useOnStay) return;
            if (!IsTagValid(other)) return;
            OnTriggerStayed?.Invoke(other);
            OnStay?.Invoke(other);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!_useOnExit) return;
            if (!IsTagValid(other)) return;
            OnTriggerExited?.Invoke(other);
            OnExit?.Invoke(other);
        }

        private bool IsTagValid(Collider other)
        {
            if (_targetTags == null || _targetTags.Length == 0) return true;
            for (int i = 0; i < _targetTags.Length; i++)
            {
                if (other.CompareTag(_targetTags[i])) return true;
            }
            return false;
        }

        private void LateUpdate()
        {
            if (!_showDebugPreview || _fillMesh == null) return;

            Vector3 position = transform.TransformPoint(_debugPosition);
            Quaternion rotation = transform.rotation;
            Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, _debugScale);

            if (_fillMaterial != null)
                Graphics.DrawMesh(_fillMesh, matrix, _fillMaterial, gameObject.layer);

            if (_wireframeMesh != null && _wireframeMaterial != null)
                Graphics.DrawMesh(_wireframeMesh, matrix, _wireframeMaterial, gameObject.layer);
        }

        private void OnDrawGizmos()
        {
            if (!_showDebugPreview) return;

            if (_fillMesh == null)
                GenerateDebugVisuals();

            if (_fillMesh == null) return;

            Vector3 position = transform.TransformPoint(_debugPosition);
            Quaternion rotation = transform.rotation;

            Gizmos.color = new Color(0, 1, 0, 0.3f);
            Gizmos.DrawMesh(_fillMesh, position, rotation, _debugScale);

            Gizmos.color = Color.green;
            Gizmos.DrawWireMesh(_fillMesh, position, rotation, _debugScale);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying) return;

            UnityEditor.EditorApplication.delayCall -= OnDeferredValidate;
            UnityEditor.EditorApplication.delayCall += OnDeferredValidate;
        }

        private void OnDeferredValidate()
        {
            if (this == null) return;

            if (_showDebugPreview)
            {
                CacheComponents();
                GenerateDebugVisuals();
            }
            else
            {
                DestroyDebugVisuals();
            }
        }
#endif

        public void SetDebugPreview(bool show)
        {
            _showDebugPreview = show;
            if (show)
                GenerateDebugVisuals();
            else
                DestroyDebugVisuals();
        }

        private void GenerateDebugVisuals()
        {
            DestroyDebugVisuals();

            if (_collider == null)
            {
                CacheComponents();
                if (_collider == null) return;
            }

            ComputeColliderData(out _debugPosition, out _debugScale);

            _fillMesh = CreateFillMesh();
            _wireframeMesh = CreateWireframeMesh();

            if (_fillMaterial == null)
                _fillMaterial = CreateMaterial(URPShaderMode.Lit, new Color(0, 1, 0, 0.35f));

            if (_wireframeMaterial == null)
                _wireframeMaterial = CreateMaterial(URPShaderMode.Unlit, Color.green);
        }

        private void DestroyDebugVisuals()
        {
            if (_fillMesh != null)
            {
                if (Application.isPlaying) Destroy(_fillMesh);
                else DestroyImmediate(_fillMesh);
                _fillMesh = null;
            }

            if (_wireframeMesh != null)
            {
                if (Application.isPlaying) Destroy(_wireframeMesh);
                else DestroyImmediate(_wireframeMesh);
                _wireframeMesh = null;
            }

            if (_fillMaterial != null)
            {
                if (Application.isPlaying) Destroy(_fillMaterial);
                else DestroyImmediate(_fillMaterial);
                _fillMaterial = null;
            }

            if (_wireframeMaterial != null)
            {
                if (Application.isPlaying) Destroy(_wireframeMaterial);
                else DestroyImmediate(_wireframeMaterial);
                _wireframeMaterial = null;
            }
        }

        private void ComputeColliderData(out Vector3 center, out Vector3 scale)
        {
            center = Vector3.zero;
            scale = Vector3.one;

            if (_collider is BoxCollider box)
            {
                center = box.center;
                scale = Vector3.Scale(transform.lossyScale, box.size);
            }
            else if (_collider is SphereCollider sphere)
            {
                center = sphere.center;
                float avgScale = (transform.lossyScale.x + transform.lossyScale.y + transform.lossyScale.z) / 3f;
                float diameter = sphere.radius * 2f;
                scale = Vector3.one * (diameter * avgScale);
            }
            else if (_collider is CapsuleCollider capsule)
            {
                center = capsule.center;
                Vector3 lossy = transform.lossyScale;

                if (capsule.direction == 0)
                    scale = new Vector3(capsule.height, capsule.radius * 2f, capsule.radius * 2f);
                else if (capsule.direction == 1)
                    scale = new Vector3(capsule.radius * 2f, capsule.height, capsule.radius * 2f);
                else
                    scale = new Vector3(capsule.radius * 2f, capsule.radius * 2f, capsule.height);

                scale = Vector3.Scale(scale, lossy);
            }
            else if (_collider is MeshCollider meshCol)
            {
                center = meshCol.sharedMesh != null ? meshCol.sharedMesh.bounds.center : Vector3.zero;
                scale = transform.lossyScale;
            }
        }

        private Mesh CreateFillMesh()
        {
            if (_collider is BoxCollider)
                return Instantiate(Resources.GetBuiltinResource<Mesh>("Cube.fbx"));

            if (_collider is SphereCollider)
                return Instantiate(Resources.GetBuiltinResource<Mesh>("Sphere.fbx"));

            if (_collider is CapsuleCollider)
                return Instantiate(Resources.GetBuiltinResource<Mesh>("Capsule.fbx"));

            if (_collider is MeshCollider meshCol && meshCol.sharedMesh != null)
                return Instantiate(meshCol.sharedMesh);

            return null;
        }

        private Mesh CreateWireframeMesh()
        {
            if (_collider is BoxCollider box)
                return CreateWireframeBox(box.size);

            if (_collider is SphereCollider sphere)
                return CreateWireframeSphere(sphere.radius, 16);

            if (_collider is CapsuleCollider capsule)
                return CreateWireframeCapsule(capsule.radius, capsule.height, capsule.direction, 12);

            if (_collider is MeshCollider meshCol && meshCol.sharedMesh != null)
                return CreateWireframeFromMesh(meshCol.sharedMesh);

            return null;
        }

        private static Mesh CreateWireframeBox(Vector3 size)
        {
            Vector3 h = size * 0.5f;
            Vector3[] verts = new Vector3[8]
            {
                new Vector3(-h.x, -h.y, -h.z),
                new Vector3( h.x, -h.y, -h.z),
                new Vector3( h.x, -h.y,  h.z),
                new Vector3(-h.x, -h.y,  h.z),
                new Vector3(-h.x,  h.y, -h.z),
                new Vector3( h.x,  h.y, -h.z),
                new Vector3( h.x,  h.y,  h.z),
                new Vector3(-h.x,  h.y,  h.z),
            };

            int[] lines = new int[]
            {
                0, 1, 1, 2, 2, 3, 3, 0,
                4, 5, 5, 6, 6, 7, 7, 4,
                0, 4, 1, 5, 2, 6, 3, 7,
            };

            Mesh mesh = new Mesh();
            mesh.vertices = verts;
            mesh.SetIndices(lines, MeshTopology.Lines, 0);
            return mesh;
        }

        private static Mesh CreateWireframeSphere(float radius, int segments)
        {
            var verts = new System.Collections.Generic.List<Vector3>();
            var indices = new System.Collections.Generic.List<int>();

            for (int i = 0; i < segments; i++)
            {
                float theta = i * Mathf.PI * 2f / segments;
                int baseIndex = verts.Count;
                int steps = segments / 2;
                for (int j = 0; j <= steps; j++)
                {
                    float phi = j * Mathf.PI / steps - Mathf.PI / 2f;
                    float x = radius * Mathf.Cos(phi) * Mathf.Cos(theta);
                    float y = radius * Mathf.Sin(phi);
                    float z = radius * Mathf.Cos(phi) * Mathf.Sin(theta);
                    verts.Add(new Vector3(x, y, z));
                }
                for (int j = 0; j < steps; j++)
                {
                    indices.Add(baseIndex + j);
                    indices.Add(baseIndex + j + 1);
                }
            }

            for (int j = 1; j < segments / 2; j++)
            {
                float phi = j * Mathf.PI / (segments / 2) - Mathf.PI / 2f;
                int baseIndex = verts.Count;
                for (int i = 0; i <= segments; i++)
                {
                    float theta = i * Mathf.PI * 2f / segments;
                    float x = radius * Mathf.Cos(phi) * Mathf.Cos(theta);
                    float y = radius * Mathf.Sin(phi);
                    float z = radius * Mathf.Cos(phi) * Mathf.Sin(theta);
                    verts.Add(new Vector3(x, y, z));
                }
                for (int i = 0; i < segments; i++)
                {
                    indices.Add(baseIndex + i);
                    indices.Add(baseIndex + i + 1);
                }
            }

            Mesh mesh = new Mesh();
            mesh.vertices = verts.ToArray();
            mesh.SetIndices(indices.ToArray(), MeshTopology.Lines, 0);
            return mesh;
        }

        private static Mesh CreateWireframeCapsule(float radius, float height, int direction, int segments)
        {
            float cylinderHeight = Mathf.Max(0, height - radius * 2f);
            float halfCylinder = cylinderHeight * 0.5f;

            var verts = new System.Collections.Generic.List<Vector3>();
            var indices = new System.Collections.Generic.List<int>();

            int arcSteps = segments / 2;

            for (int side = 0; side < 2; side++)
            {
                float yOffset = side == 0 ? halfCylinder : -halfCylinder;
                float hemisphereSign = side == 0 ? 1f : -1f;

                int baseIndex = verts.Count;
                for (int i = 0; i <= segments; i++)
                {
                    float theta = i * Mathf.PI * 2f / segments;
                    float x = radius * Mathf.Cos(theta);
                    float z = radius * Mathf.Sin(theta);
                    float y = yOffset;
                    verts.Add(ApplyCapsuleDirection(new Vector3(x, y, z), direction));
                }
                for (int i = 0; i < segments; i++)
                {
                    indices.Add(baseIndex + i);
                    indices.Add(baseIndex + i + 1);
                }

                for (int j = 0; j < arcSteps; j++)
                {
                    float phi = (j + 1) * (Mathf.PI * 0.5f / arcSteps) * hemisphereSign;
                    baseIndex = verts.Count;
                    for (int i = 0; i <= segments; i++)
                    {
                        float theta = i * Mathf.PI * 2f / segments;
                        float r = radius * Mathf.Cos(phi);
                        float y = yOffset + radius * Mathf.Sin(phi);
                        float x = r * Mathf.Cos(theta);
                        float z = r * Mathf.Sin(theta);
                        verts.Add(ApplyCapsuleDirection(new Vector3(x, y, z), direction));
                    }
                    for (int i = 0; i < segments; i++)
                    {
                        indices.Add(baseIndex + i);
                        indices.Add(baseIndex + i + 1);
                    }
                }
            }

            int verticalLines = segments / 2;
            for (int i = 0; i <= verticalLines; i++)
            {
                float theta = i * Mathf.PI * 2f / segments;
                int stepsPerSide = 1 + arcSteps;
                int prevOffset = -1;
                for (int side = 0; side < 2; side++)
                {
                    for (int j = 0; j < stepsPerSide; j++)
                    {
                        float phi = j * (Mathf.PI * 0.5f / arcSteps) * (side == 0 ? 1f : -1f);
                        float y = (side == 0 ? halfCylinder : -halfCylinder) + radius * Mathf.Sin(phi);
                        float r = radius * Mathf.Cos(phi);
                        float x = r * Mathf.Cos(theta);
                        float z = r * Mathf.Sin(theta);
                        verts.Add(ApplyCapsuleDirection(new Vector3(x, y, z), direction));
                        int idx = verts.Count - 1;
                        if (prevOffset >= 0)
                        {
                            indices.Add(prevOffset);
                            indices.Add(idx);
                        }
                        prevOffset = idx;
                    }
                }
            }

            Mesh mesh = new Mesh();
            mesh.vertices = verts.ToArray();
            mesh.SetIndices(indices.ToArray(), MeshTopology.Lines, 0);
            return mesh;
        }

        private static Vector3 ApplyCapsuleDirection(Vector3 localPoint, int direction)
        {
            if (direction == 0)
                return new Vector3(localPoint.y, localPoint.z, localPoint.x);
            if (direction == 2)
                return new Vector3(localPoint.x, localPoint.z, localPoint.y);
            return localPoint;
        }

        private static Mesh CreateWireframeFromMesh(Mesh source)
        {
            Vector3[] verts = source.vertices;
            int[] tris = source.triangles;
            var edgeSet = new System.Collections.Generic.HashSet<System.ValueTuple<int, int>>();

            for (int i = 0; i < tris.Length; i += 3)
            {
                AddEdge(edgeSet, tris[i], tris[i + 1]);
                AddEdge(edgeSet, tris[i + 1], tris[i + 2]);
                AddEdge(edgeSet, tris[i + 2], tris[i]);
            }

            var lineVerts = new System.Collections.Generic.List<Vector3>();
            var lineIndices = new System.Collections.Generic.List<int>();

            var vertMap = new System.Collections.Generic.Dictionary<int, int>();

            foreach (var edge in edgeSet)
            {
                if (!vertMap.ContainsKey(edge.Item1))
                {
                    vertMap[edge.Item1] = lineVerts.Count;
                    lineVerts.Add(verts[edge.Item1]);
                }
                if (!vertMap.ContainsKey(edge.Item2))
                {
                    vertMap[edge.Item2] = lineVerts.Count;
                    lineVerts.Add(verts[edge.Item2]);
                }
                lineIndices.Add(vertMap[edge.Item1]);
                lineIndices.Add(vertMap[edge.Item2]);
            }

            Mesh mesh = new Mesh();
            mesh.vertices = lineVerts.ToArray();
            mesh.SetIndices(lineIndices.ToArray(), MeshTopology.Lines, 0);
            return mesh;
        }

        private static void AddEdge(System.Collections.Generic.HashSet<(int, int)> set, int a, int b)
        {
            if (a <= b)
                set.Add((a, b));
            else
                set.Add((b, a));
        }

        private enum URPShaderMode { Lit, Unlit }

        private static Material CreateMaterial(URPShaderMode mode, Color color)
        {
            string shaderName = mode == URPShaderMode.Lit
                ? "Universal Render Pipeline/Lit"
                : "Universal Render Pipeline/Unlit";

            Shader shader = Shader.Find(shaderName);
            if (shader == null)
                shader = Shader.Find("Standard");

            if (shader == null) return null;

            Material mat = new Material(shader);
            mat.color = color;

            if (mode == URPShaderMode.Lit)
            {
                mat.SetFloat("_Surface", 1);
                mat.SetFloat("_Blend", 0);
                mat.SetFloat("_AlphaClip", 0);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0);
                mat.SetFloat("_Cull", 0);
                mat.renderQueue = 3000;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }

            return mat;
        }
    }

    [System.Serializable]
    public class ColliderUnityEvent : UnityEvent<Collider> { }
}
