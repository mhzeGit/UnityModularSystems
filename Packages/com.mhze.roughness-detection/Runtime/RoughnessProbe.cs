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

        [Header("References")]
        [SerializeField] private RoughnessDetector roughnessDetector;

        private float m_LastUpdateTime;
        private float m_CurrentRoughness = -1f;

        public float CurrentRoughness => m_CurrentRoughness;

        private void OnEnable()
        {
            if (roughnessDetector == null)
                roughnessDetector = FindObjectOfType<RoughnessDetector>();

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

            m_CurrentRoughness = roughnessDetector.DetectRoughness(transform, maxDistance, layerMask);
        }
    }
}
