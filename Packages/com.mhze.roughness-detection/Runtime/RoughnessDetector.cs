using UnityEngine;
using UnityEngine.Rendering;

namespace MHZE.RoughnessDetection
{
    [ExecuteAlways]
    public class RoughnessDetector : MonoBehaviour
    {
        [Header("GPU Capture")]
        [SerializeField] private Shader roughnessOutputShader;

        [Header("Visualization")]
        [SerializeField] private bool showRay = true;
        [SerializeField] private Color rayColor = new Color(1f, 0.8f, 0f);
        [SerializeField] private bool showHitPoint = true;
        [SerializeField] private Color hitPointColor = Color.red;
        [SerializeField] private bool showRoughnessLabel = true;
        [SerializeField] [Range(0.01f, 0.5f)] private float hitPointRadius = 0.08f;

        [Header("GUI Overlay")]
        [SerializeField] private bool showGUI = true;
        [SerializeField] [Range(10, 40)] private int guiFontSize = 18;

        private RenderTexture m_CaptureRT;
        private bool m_ResourcesInitialized;
        private GUIStyle m_GuiStyle;
        private Texture2D m_GuiBgTex;
        private CommandBuffer m_Cmd;

        private Transform m_LastSourceTransform;
        private RaycastHit m_LastHit;
        private float m_LastRoughness = -1f;
        private bool m_HasHit;
        private Vector2 m_LastUV;

        public float LastRoughness => m_LastRoughness;
        public bool HasHit => m_HasHit;
        public RaycastHit LastHit => m_LastHit;
        public Vector2 LastUV => m_LastUV;

        private void OnEnable()
        {
            s_WarnedColliders.Clear();
            InitializeResources();
        }

        private void OnDisable()
        {
            ReleaseResources();
        }

        private void InitializeResources()
        {
            if (m_ResourcesInitialized) return;

            m_CaptureRT = new RenderTexture(1, 1, 0, RenderTextureFormat.ARGB32)
            {
                hideFlags = HideFlags.DontSave
            };
            m_CaptureRT.Create();

            m_Cmd = new CommandBuffer { name = "RoughnessCapture" };

            m_ResourcesInitialized = true;
        }

        private void ReleaseResources()
        {
            if (!m_ResourcesInitialized) return;

            if (m_CaptureRT != null)
            {
                m_CaptureRT.Release();
                DestroyImmediate(m_CaptureRT);
            }

            if (m_Cmd != null)
                m_Cmd.Release();

            if (m_GuiBgTex != null)
                DestroyImmediate(m_GuiBgTex);

            m_GuiStyle = null;
            m_ResourcesInitialized = false;
        }

        public float DetectRoughness(Transform source)
        {
            return DetectRoughness(source, 5f, -1, 0, "_MetallicGlossMap", 3, true);
        }

        public float DetectRoughness(
            Transform source, float maxDistance, int layerMask = -1, int uvChannel = 0,
            string roughnessTexProperty = "_MetallicGlossMap", int roughnessTexChannel = 3, bool roughnessTexInverted = true)
        {
            if (roughnessOutputShader == null || !m_ResourcesInitialized)
                return -1f;

            m_LastSourceTransform = source;

            var ray = new Ray(source.position, source.forward);
            m_HasHit = Physics.Raycast(ray, out m_LastHit, maxDistance, layerMask);

            if (m_HasHit)
            {
                m_LastUV = GetUVAtChannel(m_LastHit, uvChannel);
                m_LastRoughness = CaptureRoughnessGPU(m_LastHit, m_LastUV, roughnessTexProperty, roughnessTexChannel, roughnessTexInverted);

                if (m_LastRoughness < 0f)
                    m_LastRoughness = CaptureRoughnessMaterial(m_LastHit, m_LastUV, roughnessTexProperty, roughnessTexChannel, roughnessTexInverted);
            }
            else
            {
                m_LastRoughness = -1f;
            }

            return m_LastRoughness;
        }

        private static readonly System.Collections.Generic.HashSet<int> s_WarnedColliders = new System.Collections.Generic.HashSet<int>();

        private static Vector2 GetUVAtChannel(RaycastHit hit, int channel)
        {
            if (channel <= 1)
                return channel == 0 ? hit.textureCoord : hit.textureCoord2;

            var uv = InterpolateMeshUV(hit, channel);
            if (uv != Vector2.zero)
                return uv;

            var id = hit.collider.GetHashCode();
            if (s_WarnedColliders.Add(id))
            {
                Debug.LogWarning(
                    $"[RoughnessDetector] Cannot read UV{channel} from '{hit.collider.gameObject.name}'. " +
                    $"Collider type '{hit.collider.GetType().Name}' has no accessible UV{channel} data. " +
                    "Falling back to UV0. Add a MeshCollider or ensure the mesh has UV data on the selected channel.",
                    hit.collider
                );
            }
            return hit.textureCoord;
        }

        private static Vector2 InterpolateMeshUV(RaycastHit hit, int channel)
        {
            Mesh mesh = null;

            if (hit.collider is MeshCollider mc && mc.sharedMesh != null)
                mesh = mc.sharedMesh;

            if (mesh == null)
            {
                var renderer = hit.collider.GetComponent<Renderer>();
                if (renderer != null)
                {
                    if (renderer is SkinnedMeshRenderer smr)
                        mesh = smr.sharedMesh;
                    else if (renderer.TryGetComponent<MeshFilter>(out var mf))
                        mesh = mf.sharedMesh;
                }
            }

            if (mesh == null)
                return Vector2.zero;

            var uv = channel switch
            {
                2 => mesh.uv2,
                3 => mesh.uv3,
                _ => mesh.uv
            };

            if (uv == null || uv.Length == 0)
                return Vector2.zero;

            var triangles = mesh.triangles;
            var triIndex = hit.triangleIndex;
            if (triIndex < 0 || triIndex * 3 + 2 >= triangles.Length)
                return Vector2.zero;

            var i0 = triangles[triIndex * 3];
            var i1 = triangles[triIndex * 3 + 1];
            var i2 = triangles[triIndex * 3 + 2];

            if (i0 >= uv.Length || i1 >= uv.Length || i2 >= uv.Length)
                return Vector2.zero;

            var bary = hit.barycentricCoordinate;
            return uv[i0] * bary.x + uv[i1] * bary.y + uv[i2] * bary.z;
        }

        private float CaptureRoughnessGPU(
            RaycastHit hit, Vector2 uv,
            string roughnessTexProperty, int roughnessTexChannel, bool roughnessTexInverted)
        {
            if (roughnessOutputShader == null || m_CaptureRT == null)
                return -1f;

            var renderer = hit.collider.GetComponent<Renderer>();
            if (renderer == null) return -1f;

            var sharedMaterials = renderer.sharedMaterials;
            if (sharedMaterials == null || sharedMaterials.Length == 0) return -1f;

            var submeshIndex = hit.triangleIndex / 3;
            if (submeshIndex >= sharedMaterials.Length) submeshIndex = 0;

            var material = sharedMaterials[submeshIndex];
            if (material == null) return -1f;

            try
            {
                var sampleMat = new Material(roughnessOutputShader);

                var texProperty = roughnessTexProperty;
                Texture roughnessTex = material.HasProperty(texProperty) ? material.GetTexture(texProperty) : null;
                sampleMat.SetTexture("_RoughnessTex", roughnessTex);

                string stProperty = texProperty + "_ST";
                Vector4 st = material.HasProperty(stProperty)
                    ? material.GetVector(stProperty)
                    : material.HasProperty("_BaseMap_ST")
                        ? material.GetVector("_BaseMap_ST")
                        : new Vector4(1, 1, 0, 0);
                sampleMat.SetVector("_RoughnessTex_ST", st);

                float fallback = 0.5f;
                if (material.HasProperty("_Smoothness"))
                    fallback = 1f - material.GetFloat("_Smoothness");

                sampleMat.SetFloat("_RoughnessFallback", fallback);
                sampleMat.SetVector("_RoughnessParams", new Vector4(roughnessTexChannel, roughnessTexInverted ? 1f : 0f, roughnessTex != null ? 1f : 0f, 0));
                sampleMat.SetVector("_SampleUV", new Vector4(uv.x, uv.y, 0, 0));

                m_Cmd.Clear();
                m_Cmd.SetRenderTarget(m_CaptureRT);
                m_Cmd.ClearRenderTarget(true, true, Color.clear);
                m_Cmd.DrawProcedural(Matrix4x4.identity, sampleMat, 0, MeshTopology.Triangles, 3);
                Graphics.ExecuteCommandBuffer(m_Cmd);

                var prevRT = RenderTexture.active;
                RenderTexture.active = m_CaptureRT;

                var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
                tex.Apply();

                RenderTexture.active = prevRT;

                var pixel = tex.GetPixel(0, 0);
                DestroyImmediate(tex);
                DestroyImmediate(sampleMat);

                return pixel.r;
            }
            catch
            {
                return -1f;
            }
        }

        private float CaptureRoughnessMaterial(
            RaycastHit hit, Vector2 uv,
            string roughnessTexProperty, int roughnessTexChannel, bool roughnessTexInverted)
        {
            var renderer = hit.collider.GetComponent<Renderer>();
            if (renderer == null) return -1f;

            var sharedMaterials = renderer.sharedMaterials;
            if (sharedMaterials == null || sharedMaterials.Length == 0)
                return -1f;

            var submeshIndex = hit.triangleIndex / 3;
            if (submeshIndex >= sharedMaterials.Length)
                submeshIndex = 0;

            var material = sharedMaterials[submeshIndex];
            if (material == null) return -1f;

            float value = 0.5f;

            if (material.HasProperty("_Smoothness"))
                value = 1f - material.GetFloat("_Smoothness");

            var roughnessTex = material.GetTexture(roughnessTexProperty);
            if (roughnessTex is Texture2D tex2D && tex2D != null)
            {
                try
                {
                    var stProperty = roughnessTexProperty + "_ST";
                    Vector2 scale = material.HasProperty(stProperty)
                        ? material.GetTextureScale(roughnessTexProperty)
                        : Vector2.one;
                    Vector2 offset = material.HasProperty(stProperty)
                        ? material.GetTextureOffset(roughnessTexProperty)
                        : Vector2.zero;

                    var sampledUV = new Vector2(
                        uv.x * scale.x + offset.x,
                        uv.y * scale.y + offset.y
                    );

                    var readable = tex2D.isReadable
                        ? tex2D
                        : CreateReadableCopy(tex2D);

                    if (readable != null)
                    {
                        var sample = readable.GetPixelBilinear(sampledUV.x, sampledUV.y);
                        float channelValue = roughnessTexChannel == 0 ? sample.r :
                                             roughnessTexChannel == 1 ? sample.g :
                                             roughnessTexChannel == 2 ? sample.b : sample.a;
                        value = roughnessTexInverted ? 1f - channelValue : channelValue;
                    }
                }
                catch
                {
                }
            }

            return Mathf.Clamp01(value);
        }

        private static Texture2D CreateReadableCopy(Texture2D source)
        {
            var rt = RenderTexture.GetTemporary(
                source.width, source.height, 0, RenderTextureFormat.ARGB32
            );
            Graphics.Blit(source, rt);

            var prev = RenderTexture.active;
            RenderTexture.active = rt;

            var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            copy.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            copy.hideFlags = HideFlags.HideAndDontSave;
            return copy;
        }

        private void OnDrawGizmos()
        {
            DrawGizmos();
        }

        private void DrawGizmos()
        {
            if (m_LastSourceTransform == null) return;

            var origin = m_LastSourceTransform.position;
            var direction = m_LastSourceTransform.forward;

            if (m_HasHit)
            {
                if (showRay)
                {
                    Gizmos.color = rayColor;
                    Gizmos.DrawRay(origin, direction * m_LastHit.distance);

                    var end = m_LastHit.point;
                    Gizmos.DrawLine(end, end + m_LastHit.normal * 0.2f);
                }

                if (showHitPoint)
                {
                    Gizmos.color = hitPointColor;
                    Gizmos.DrawSphere(m_LastHit.point, hitPointRadius);
                }

#if UNITY_EDITOR
                if (showRoughnessLabel && m_LastRoughness >= 0f)
                {
                    var roughnessT = Mathf.Clamp01(m_LastRoughness);
                    var labelColor = Color.Lerp(
                        new Color(0.2f, 0.6f, 1f),
                        new Color(1f, 0.3f, 0.1f),
                        roughnessT
                    );

                    var style = new GUIStyle
                    {
                        fontSize = 14,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = labelColor }
                    };

                    var offset = (m_LastSourceTransform.position - m_LastHit.point).normalized * 0.2f + Vector3.up * 0.25f;
                    var labelText = $"R: {m_LastRoughness:F3}\nUV({m_LastUV.x:F3}, {m_LastUV.y:F3})";

                    var smallStyle = new GUIStyle(style) { fontSize = 10, fontStyle = FontStyle.Normal };
                    UnityEditor.Handles.Label(m_LastHit.point + offset, labelText, style);
                    UnityEditor.Handles.Label(m_LastHit.point + offset + Vector3.down * 0.18f, $"UV({m_LastUV.x:F3}, {m_LastUV.y:F3})", smallStyle);
                }
#endif
            }
            else
            {
                if (showRay)
                {
                    Gizmos.color = new Color(rayColor.r, rayColor.g, rayColor.b, 0.15f);
                    Gizmos.DrawRay(origin, direction * (origin - m_LastSourceTransform.position).magnitude + direction * 5f);
                }
            }
        }

        private void OnGUI()
        {
            if (!showGUI || !Application.isPlaying || !m_HasHit || m_LastRoughness < 0f)
                return;

            EnsureGuiStyle();
            m_GuiStyle.fontSize = guiFontSize;

            var roughnessT = Mathf.Clamp01(m_LastRoughness);
            var labelColor = Color.Lerp(
                new Color(0.2f, 0.6f, 1f),
                new Color(1f, 0.3f, 0.1f),
                roughnessT
            );
            m_GuiStyle.normal.textColor = labelColor;

            var rect = new Rect(12, 12, 300, 32);
            GUI.Label(rect, $"R: {m_LastRoughness:F3}  UV({m_LastUV.x:F3},{m_LastUV.y:F3})", m_GuiStyle);
        }

        private void EnsureGuiStyle()
        {
            if (m_GuiStyle != null) return;

            m_GuiBgTex = new Texture2D(1, 1);
            m_GuiBgTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.55f));
            m_GuiBgTex.Apply();

            m_GuiStyle = new GUIStyle
            {
                fontSize = guiFontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal =
                {
                    textColor = Color.white,
                    background = m_GuiBgTex
                },
                padding = new RectOffset(8, 8, 4, 4)
            };
        }
    }
}
