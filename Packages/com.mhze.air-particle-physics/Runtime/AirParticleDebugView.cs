using UnityEngine;

namespace MHZE.AirParticlePhysics
{
    [RequireComponent(typeof(AirParticleSystem))]
    public class AirParticleDebugView : MonoBehaviour
    {
        [SerializeField] private AirParticleSystem _particleSystem;

        [Header("Display")]
        [SerializeField] private Color _boundsColor = new Color(0f, 0.8f, 1f, 0.3f);

        private void OnValidate()
        {
            if (_particleSystem == null)
                _particleSystem = GetComponent<AirParticleSystem>();
        }

        private void OnDrawGizmos()
        {
            if (_particleSystem == null) return;

            AirParticleBoxVolume volume = _particleSystem.Volume;
            if (volume == null) return;

            Gizmos.color = _boundsColor;
            Gizmos.DrawWireCube(volume.Center, volume.Size);
        }
    }
}
