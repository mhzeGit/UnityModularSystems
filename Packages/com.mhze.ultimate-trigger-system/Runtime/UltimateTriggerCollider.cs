using UnityEngine;
using ArgEvent;

namespace MHZE.UltimateTriggerSystem
{
    public class UltimateTriggerCollider : MonoBehaviour
    {
        [SerializeField, TagSelector] private string[] _targetTags = { "Player" };

        [SerializeField] private bool _useOnEnter = true;
        [SerializeField] private bool _useOnStay = true;
        [SerializeField] private bool _useOnExit = true;
        public ArgEventBinding OnEnter= new ArgEventBinding();
        public ArgEventBinding OnStay= new ArgEventBinding();
        public ArgEventBinding OnExit= new ArgEventBinding();

        public event System.Action<Collider> OnTriggerEntered;
        public event System.Action<Collider> OnTriggerStayed;
        public event System.Action<Collider> OnTriggerExited;

        [SerializeField] private bool _showDebugPreview;

        private Collider _collider;
        private Mesh _fillMesh;
        private Mesh _wireframeMesh;
        private Material _fillMaterial;
        private Material _wireframeMaterial;
        private Vector3 _debugScale;
        private Vector3 _debugPosition;
        private System.Type _prevColliderType;
        private Vector3 _prevCenter;
        private Vector3 _prevScale;

        private void Reset()
        {
            if (!TryGetComponent(out Collider hitCollider))
                hitCollider = gameObject.AddComponent<BoxCollider>();

            hitCollider.isTrigger = true;
        }

        private void Awake()
        {
            CacheComponents();

            if (_collider == null)
                _collider = gameObject.AddComponent<BoxCollider>();

            _collider.isTrigger = true;
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
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_useOnEnter) return;
            if (!IsTagValid(other)) return;
            OnTriggerEntered?.Invoke(other);
            OnEnter?.Invoke();
        }

        private void OnTriggerStay(Collider other)
        {
            if (!_useOnStay) return;
            if (!IsTagValid(other)) return;
            OnTriggerStayed?.Invoke(other);
            OnStay?.Invoke();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!_useOnExit) return;
            if (!IsTagValid(other)) return;
            OnTriggerExited?.Invoke(other);
            OnExit?.Invoke();
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
            if (!_showDebugPreview) return;

            if (_fillMesh == null || ColliderStateChanged())
                GenerateDebugVisuals();

            if (_fillMesh == null) return;

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

            if (_fillMesh == null || ColliderStateChanged())
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
            _prevColliderType = _collider.GetType();
            _prevCenter = _debugPosition;
            _prevScale = _debugScale;

            _fillMesh = CreateFillMesh();
            if (_fillMesh != null)
                _wireframeMesh = CreateWireframeFromMesh(_fillMesh);

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

        private bool ColliderStateChanged()
        {
            if (_collider == null) return true;
            if (_collider.GetType() != _prevColliderType) return true;
            ComputeColliderData(out Vector3 center, out Vector3 scale);
            return center != _prevCenter || scale != _prevScale;
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
}
