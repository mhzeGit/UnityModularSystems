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
        private Texture2D m_CaptureTex;
        private bool m_ResourcesInitialized;
        private GUIStyle m_GuiStyle;
        private Texture2D m_GuiBgTex;
        private CommandBuffer m_Cmd;
        private Material m_SampleMat;
        private Shader m_SampleMatShader;

        private RoughnessResult m_LastResult;
        public RoughnessResult LastResult => m_LastResult;

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

            m_CaptureTex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.DontSave
            };

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

            if (m_CaptureTex != null)
                DestroyImmediate(m_CaptureTex);

            if (m_Cmd != null)
                m_Cmd.Release();

            if (m_SampleMat != null)
                DestroyImmediate(m_SampleMat);

            if (m_GuiBgTex != null)
                DestroyImmediate(m_GuiBgTex);

            m_GuiStyle = null;
            m_ResourcesInitialized = false;
        }

        public RoughnessResult DetectRoughness(Transform source)
        {
            return DetectRoughness(source, 5f, -1, 0, "_MetallicGlossMap", 3, true);
        }

        public RoughnessResult DetectRoughness(
            Transform source, float maxDistance, int layerMask = -1, int uvChannel = 0,
            string roughnessTexProperty = "_MetallicGlossMap", int roughnessTexChannel = 3, bool roughnessTexInverted = true)
        {
            m_LastResult = RoughnessResult.Invalid;

            if (roughnessOutputShader == null || !m_ResourcesInitialized)
                return m_LastResult;

            var result = new RoughnessResult();
            var ray = new Ray(source.position, source.forward);
            result.hasHit = Physics.Raycast(ray, out result.hit, maxDistance, layerMask);

            if (result.hasHit)
            {
                result.uv = GetUVAtChannel(result.hit, uvChannel);
                result.roughness = CaptureRoughnessGPU(result.hit, result.uv, roughnessTexProperty, roughnessTexChannel, roughnessTexInverted);

                if (result.roughness < 0f)
                    result.roughness = CaptureRoughnessMaterial(result.hit, result.uv, roughnessTexProperty, roughnessTexChannel, roughnessTexInverted);
            }
            else
            {
                result.roughness = -1f;
            }

            m_LastResult = result;
            return result;
        }

        private static readonly System.Collections.Generic.HashSet<int> s_WarnedColliders = new System.Collections.Generic.HashSet<int>();

        private static Vector2 GetUVAtChannel(RaycastHit hit, int channel)
        {
            var instanceUV = InterpolateMeshUV(hit, channel);
            if (instanceUV != Vector2.zero)
                return instanceUV;

            if (channel <= 1)
                return channel == 0 ? hit.textureCoord : hit.textureCoord2;

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
            var renderer = hit.collider.GetComponent<Renderer>();
            if (renderer == null)
            {
                if (hit.collider is MeshCollider mc && mc.sharedMesh != null)
                    return InterpolateUVFromMesh(mc.sharedMesh, hit, channel);
                return Vector2.zero;
            }

            if (renderer is SkinnedMeshRenderer smr)
            {
                var mesh = smr.sharedMesh;
                if (mesh == null) return Vector2.zero;
                return InterpolateUVFromMesh(mesh, hit, channel);
            }

            if (renderer.TryGetComponent<MeshFilter>(out var mf))
            {
                var mesh = mf.mesh;
                if (mesh == null) return Vector2.zero;
                return InterpolateUVFromMesh(mesh, hit, channel);
            }

            return Vector2.zero;
        }

        private static Vector2 InterpolateUVFromMesh(Mesh mesh, RaycastHit hit, int channel)
        {
            var uv = channel switch
            {
                0 => mesh.uv,
                1 => mesh.uv2,
                2 => mesh.uv3,
                3 => mesh.uv4,
                _ => null
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

            var instanceMaterials = renderer.materials;
            if (instanceMaterials == null || instanceMaterials.Length == 0) return -1f;

            var submeshIndex = hit.triangleIndex / 3;
            if (submeshIndex >= instanceMaterials.Length) submeshIndex = 0;

            var material = instanceMaterials[submeshIndex];
            if (material == null) return -1f;

            try
            {
                EnsureSampleMaterial();
                if (m_SampleMat == null) return -1f;

                var texProperty = roughnessTexProperty;
                Texture roughnessTex = material.HasProperty(texProperty) ? material.GetTexture(texProperty) : null;
                m_SampleMat.SetTexture("_RoughnessTex", roughnessTex);

                string stProperty = texProperty + "_ST";
                Vector4 st = material.HasProperty(stProperty)
                    ? material.GetVector(stProperty)
                    : material.HasProperty("_BaseMap_ST")
                        ? material.GetVector("_BaseMap_ST")
                        : new Vector4(1, 1, 0, 0);
                m_SampleMat.SetVector("_RoughnessTex_ST", st);

                float fallback = 0.5f;
                if (material.HasProperty("_Smoothness"))
                    fallback = 1f - material.GetFloat("_Smoothness");

                m_SampleMat.SetFloat("_RoughnessFallback", fallback);
                m_SampleMat.SetVector("_RoughnessParams", new Vector4(roughnessTexChannel, roughnessTexInverted ? 1f : 0f, roughnessTex != null ? 1f : 0f, 0));
                m_SampleMat.SetVector("_SampleUV", new Vector4(uv.x, uv.y, 0, 0));

                m_Cmd.Clear();
                m_Cmd.SetRenderTarget(m_CaptureRT);
                m_Cmd.ClearRenderTarget(true, true, Color.clear);
                m_Cmd.DrawProcedural(Matrix4x4.identity, m_SampleMat, 0, MeshTopology.Triangles, 3);
                Graphics.ExecuteCommandBuffer(m_Cmd);

                var prevRT = RenderTexture.active;
                RenderTexture.active = m_CaptureRT;

                m_CaptureTex.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
                m_CaptureTex.Apply();

                RenderTexture.active = prevRT;

                var pixel = m_CaptureTex.GetPixel(0, 0);
                return pixel.r;
            }
            catch
            {
                return -1f;
            }
        }

        private void EnsureSampleMaterial()
        {
            if (m_SampleMat != null && m_SampleMatShader == roughnessOutputShader)
                return;

            if (m_SampleMat != null)
                DestroyImmediate(m_SampleMat);

            m_SampleMat = new Material(roughnessOutputShader)
            {
                hideFlags = HideFlags.DontSave
            };
            m_SampleMatShader = roughnessOutputShader;
        }

        private float CaptureRoughnessMaterial(
            RaycastHit hit, Vector2 uv,
            string roughnessTexProperty, int roughnessTexChannel, bool roughnessTexInverted)
        {
            var renderer = hit.collider.GetComponent<Renderer>();
            if (renderer == null) return -1f;

            var instanceMaterials = renderer.materials;
            if (instanceMaterials == null || instanceMaterials.Length == 0)
                return -1f;

            var submeshIndex = hit.triangleIndex / 3;
            if (submeshIndex >= instanceMaterials.Length)
                submeshIndex = 0;

            var material = instanceMaterials[submeshIndex];
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
            if (!m_LastResult.IsValid)
            {
                if (showRay && roughnessOutputShader != null)
                {
                    Gizmos.color = new Color(rayColor.r, rayColor.g, rayColor.b, 0.15f);
                    Gizmos.DrawRay(transform.position, transform.forward * 5f);
                }
                return;
            }

            var origin = transform.position;
            var direction = transform.forward;

            if (showRay)
            {
                Gizmos.color = rayColor;
                Gizmos.DrawRay(origin, direction * m_LastResult.hit.distance);

                var end = m_LastResult.hit.point;
                Gizmos.DrawLine(end, end + m_LastResult.hit.normal * 0.2f);
            }

            if (showHitPoint)
            {
                Gizmos.color = hitPointColor;
                Gizmos.DrawSphere(m_LastResult.hit.point, hitPointRadius);
            }

#if UNITY_EDITOR
            if (showRoughnessLabel && m_LastResult.roughness >= 0f)
            {
                var roughnessT = Mathf.Clamp01(m_LastResult.roughness);
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

                var offset = (transform.position - m_LastResult.hit.point).normalized * 0.2f + Vector3.up * 0.25f;

                UnityEditor.Handles.Label(m_LastResult.hit.point + offset, $"R: {m_LastResult.roughness:F3}", style);

                var smallStyle = new GUIStyle(style) { fontSize = 10, fontStyle = FontStyle.Normal };
                UnityEditor.Handles.Label(m_LastResult.hit.point + offset + Vector3.down * 0.18f, $"UV({m_LastResult.uv.x:F3}, {m_LastResult.uv.y:F3})", smallStyle);
            }
#endif
        }

        private void OnGUI()
        {
            if (!showGUI || !Application.isPlaying || !m_LastResult.IsValid)
                return;

            EnsureGuiStyle();
            m_GuiStyle.fontSize = guiFontSize;

            var roughnessT = Mathf.Clamp01(m_LastResult.roughness);
            var labelColor = Color.Lerp(
                new Color(0.2f, 0.6f, 1f),
                new Color(1f, 0.3f, 0.1f),
                roughnessT
            );
            m_GuiStyle.normal.textColor = labelColor;

            var rect = new Rect(12, 12, 300, 32);
            GUI.Label(rect, $"R: {m_LastResult.roughness:F3}  UV({m_LastResult.uv.x:F3},{m_LastResult.uv.y:F3})", m_GuiStyle);
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
