using System.Collections.Generic;
using UnityEngine;

namespace MHZE.RoughnessDetection
{
    [ExecuteAlways]
    public class RoughnessProbe : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField] private float maxDistance = 5f;
        [SerializeField] private LayerMask layerMask = -1;
        [SerializeField] [Range(0.01f, 2f)] private float updateInterval = 0.1f;
        [SerializeField] [Range(0, 3)] private int uvChannel = 0;

        [Header("Roughness Texture")]
        [Tooltip("Property name of the roughness/smoothness texture in the target material. For URP Lit this is _MetallicGlossMap. For custom Shader Graph shaders, use the reference name of your texture property.")]
        [SerializeField] private string roughnessTexProperty = "_MetallicGlossMap";
        [Tooltip("Which color channel contains the value. 0=R, 1=G, 2=B, 3=A. Standard URP Lit stores smoothness in the Alpha channel.")]
        [SerializeField] [Range(0, 3)] private int roughnessTexChannel = 3;
        [Tooltip("If enabled, the sampled value is inverted (1 - value). Enable when the texture stores smoothness to convert to roughness.")]
        [SerializeField] private bool roughnessTexInverted = true;

        [Header("References")]
        [SerializeField] private RoughnessDetector roughnessDetector;

        [Header("Debug Visualization")]
        [SerializeField] private bool showDebug = true;
        [SerializeField] private Color debugRayColor = new Color(1f, 0.8f, 0f);
        [SerializeField] private Color debugHitColor = Color.red;

        [Header("GUI Overlay")]
        [SerializeField] private bool showGUI = true;
        [SerializeField] [Range(10, 40)] private int guiFontSize = 18;

        private float m_LastUpdateTime;
        private RoughnessResult m_LastResult;

        private static readonly List<RoughnessProbe> s_ActiveProbes = new List<RoughnessProbe>();
        private GUIStyle m_GuiStyle;
        private Texture2D m_GuiBgTex;

        public float CurrentRoughness => m_LastResult.roughness;
        public RoughnessResult LastResult => m_LastResult;

        public bool ShowDebug
        {
            get => showDebug;
            set => showDebug = value;
        }
        public Color DebugRayColor
        {
            get => debugRayColor;
            set => debugRayColor = value;
        }
        public Color DebugHitColor
        {
            get => debugHitColor;
            set => debugHitColor = value;
        }

        private void OnEnable()
        {
            s_ActiveProbes.Add(this);

            if (roughnessDetector == null)
                roughnessDetector = FindFirstObjectByType<RoughnessDetector>();

            if (roughnessDetector == null)
            {
                var go = new GameObject("RoughnessDetector") { hideFlags = HideFlags.DontSave };
                roughnessDetector = go.AddComponent<RoughnessDetector>();
            }
        }

        private void OnDisable()
        {
            s_ActiveProbes.Remove(this);
        }

        private void OnDestroy()
        {
            s_ActiveProbes.Remove(this);
        }

        private void Update()
        {
            if (roughnessDetector == null) return;

            if (Time.time - m_LastUpdateTime < updateInterval)
                return;
            m_LastUpdateTime = Time.time;

            m_LastResult = roughnessDetector.DetectRoughness(
                transform, maxDistance, layerMask, uvChannel,
                roughnessTexProperty, roughnessTexChannel, roughnessTexInverted
            );
        }

        private void OnDrawGizmos()
        {
            if (!showDebug) return;
            DrawGizmos();
        }

        private void DrawGizmos()
        {
            if (!m_LastResult.IsValid)
            {
                if (roughnessDetector != null)
                {
                    Gizmos.color = new Color(debugRayColor.r, debugRayColor.g, debugRayColor.b, 0.15f);
                    Gizmos.DrawRay(transform.position, transform.forward * maxDistance);
                }
                return;
            }

            var origin = transform.position;
            var direction = transform.forward;
            var hit = m_LastResult.hit;

            Gizmos.color = debugRayColor;
            Gizmos.DrawRay(origin, direction * hit.distance);
            Gizmos.DrawLine(hit.point, hit.point + hit.normal * 0.2f);

            Gizmos.color = debugHitColor;
            Gizmos.DrawSphere(hit.point, 0.08f);

#if UNITY_EDITOR
            if (m_LastResult.roughness >= 0f)
            {
                var roughnessT = Mathf.Clamp01(m_LastResult.roughness);
                var labelColor = Color.Lerp(
                    new Color(0.2f, 0.6f, 1f),
                    new Color(1f, 0.3f, 0.1f),
                    roughnessT
                );

                var style = new GUIStyle
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = labelColor }
                };

                var offset = (transform.position - hit.point).normalized * 0.2f + Vector3.up * 0.25f;
                UnityEditor.Handles.Label(hit.point + offset, $"R: {m_LastResult.roughness:F3}", style);

                var smallStyle = new GUIStyle(style) { fontSize = 10, fontStyle = FontStyle.Normal };
                UnityEditor.Handles.Label(
                    hit.point + offset + Vector3.down * 0.18f,
                    $"UV({m_LastResult.uv.x:F3}, {m_LastResult.uv.y:F3})",
                    smallStyle
                );
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

            var index = s_ActiveProbes.IndexOf(this);
            if (index < 0) index = 0;

            var rect = new Rect(12, 12 + index * 36, 400, 32);
            GUI.Label(rect, $"[{gameObject.name}] R: {m_LastResult.roughness:F3}  UV({m_LastResult.uv.x:F3},{m_LastResult.uv.y:F3})", m_GuiStyle);
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
