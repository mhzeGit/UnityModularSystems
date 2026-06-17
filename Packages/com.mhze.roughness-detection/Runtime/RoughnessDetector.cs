using UnityEngine;
using UnityEngine.Rendering;

namespace MHZE.RoughnessDetection
{
    [ExecuteAlways]
    public class RoughnessDetector : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField] private float maxDistance = 5f;
        [SerializeField] private LayerMask layerMask = -1;
        [SerializeField] [Range(0.01f, 2f)] private float updateInterval = 0.1f;

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

        private float m_LastUpdateTime;
        private RaycastHit m_LastHit;
        private float m_CurrentRoughness = -1f;
        private bool m_HasHit;
        private RenderTexture m_CaptureRT;
        private bool m_ResourcesInitialized;
        private GUIStyle m_GuiStyle;
        private Texture2D m_GuiBgTex;
        private CommandBuffer m_Cmd;

        public float CurrentRoughness => m_CurrentRoughness;
        public bool HasHit => m_HasHit;
        public RaycastHit LastHit => m_LastHit;

        private void OnEnable()
        {
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

        private void Update()
        {
            if (roughnessOutputShader == null) return;

            if (Time.time - m_LastUpdateTime < updateInterval)
                return;
            m_LastUpdateTime = Time.time;

            PerformDetection();
        }

        private void PerformDetection()
        {
            var ray = new Ray(transform.position, transform.forward);
            m_HasHit = Physics.Raycast(ray, out m_LastHit, maxDistance, layerMask);

            if (m_HasHit)
            {
                m_CurrentRoughness = CaptureRoughnessGPU(m_LastHit);

                if (m_CurrentRoughness < 0f)
                    m_CurrentRoughness = CaptureRoughnessMaterial(m_LastHit);
            }
            else
            {
                m_CurrentRoughness = -1f;
            }
        }

        private float CaptureRoughnessGPU(RaycastHit hit)
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

                sampleMat.SetTexture("_MetallicGlossMap", material.HasProperty("_MetallicGlossMap") ? material.GetTexture("_MetallicGlossMap") : null);
                sampleMat.SetTexture("_SpecGlossMap", material.HasProperty("_SpecGlossMap") ? material.GetTexture("_SpecGlossMap") : null);
                sampleMat.SetTexture("_BaseMap", material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null);

                sampleMat.SetFloat("_Smoothness", material.HasProperty("_Smoothness") ? material.GetFloat("_Smoothness") : 0.5f);
                sampleMat.SetVector("_BaseMap_ST", material.HasProperty("_BaseMap_ST") ? material.GetVector("_BaseMap_ST") : new Vector4(1, 1, 0, 0));
                sampleMat.SetFloat("_SmoothnessTextureChannel", material.HasProperty("_SmoothnessTextureChannel") ? material.GetFloat("_SmoothnessTextureChannel") : 0f);
                sampleMat.SetFloat("_WorkflowMode", material.HasProperty("_WorkflowMode") ? material.GetFloat("_WorkflowMode") : 1f);

                var uv = hit.textureCoord;
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

        private float CaptureRoughnessMaterial(RaycastHit hit)
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

            var smoothness = 0.5f;

            if (material.HasProperty("_Smoothness"))
                smoothness = material.GetFloat("_Smoothness");

            var metallicGlossMap = material.GetTexture("_MetallicGlossMap");
            if (metallicGlossMap is Texture2D tex2D && tex2D != null)
            {
                try
                {
                    var uv = hit.textureCoord;
                    var scale = material.GetTextureScale("_MetallicGlossMap");
                    var offset = material.GetTextureOffset("_MetallicGlossMap");
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
                        smoothness *= sample.a;
                    }
                }
                catch
                {
                }
            }

            return 1f - Mathf.Clamp01(smoothness);
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
            var origin = transform.position;
            var direction = transform.forward;

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
                if (showRoughnessLabel && m_CurrentRoughness >= 0f)
                {
                    var roughnessT = Mathf.Clamp01(m_CurrentRoughness);
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

                    var offset = (transform.position - m_LastHit.point).normalized * 0.2f + Vector3.up * 0.25f;
                    UnityEditor.Handles.Label(m_LastHit.point + offset, $"R: {m_CurrentRoughness:F3}", style);
                }
#endif
            }
            else
            {
                if (showRay)
                {
                    Gizmos.color = new Color(rayColor.r, rayColor.g, rayColor.b, 0.15f);
                    Gizmos.DrawRay(origin, direction * maxDistance);
                }
            }
        }

        private void OnGUI()
        {
            if (!showGUI || !Application.isPlaying || !m_HasHit || m_CurrentRoughness < 0f)
                return;

            EnsureGuiStyle();
            m_GuiStyle.fontSize = guiFontSize;

            var roughnessT = Mathf.Clamp01(m_CurrentRoughness);
            var labelColor = Color.Lerp(
                new Color(0.2f, 0.6f, 1f),
                new Color(1f, 0.3f, 0.1f),
                roughnessT
            );
            m_GuiStyle.normal.textColor = labelColor;

            var rect = new Rect(12, 12, 240, 32);
            GUI.Label(rect, $"R: {m_CurrentRoughness:F3}", m_GuiStyle);
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
