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

        private float m_LastUpdateTime;
        private float m_CurrentRoughness = -1f;

        public float CurrentRoughness => m_CurrentRoughness;

        private void OnEnable()
        {
            if (roughnessDetector == null)
                roughnessDetector = FindFirstObjectByType<RoughnessDetector>();

            if (roughnessDetector == null)
            {
                var go = new GameObject("RoughnessDetector") { hideFlags = HideFlags.DontSave };
                roughnessDetector = go.AddComponent<RoughnessDetector>();
            }
        }

        private void Update()
        {
            if (roughnessDetector == null) return;

            if (Time.time - m_LastUpdateTime < updateInterval)
                return;
            m_LastUpdateTime = Time.time;

            m_CurrentRoughness = roughnessDetector.DetectRoughness(
                transform, maxDistance, layerMask, uvChannel,
                roughnessTexProperty, roughnessTexChannel, roughnessTexInverted
            );
        }
    }
}
