using UnityEngine;

namespace MHZE.AirParticlePhysics
{
    public class AirParticleSystem : MonoBehaviour
    {
        [SerializeField] private AirParticleBoxVolume _volume;

        public AirParticleBoxVolume Volume => _volume;

        private void OnValidate()
        {
            if (_volume == null)
                _volume = GetComponent<AirParticleBoxVolume>();
        }
    }
}
