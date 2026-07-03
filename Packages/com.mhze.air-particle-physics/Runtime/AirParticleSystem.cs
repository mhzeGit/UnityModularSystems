using UnityEngine;

namespace MHZE.AirParticlePhysics
{
    public class AirParticleSystem : MonoBehaviour
    {
        [SerializeField] private AirParticleBoxVolume _volume;

        private AirParticleDebugView _debug;

        public AirParticleBoxVolume Volume => _volume;
        public AirParticleDebugView DebugView => _debug;

        private void OnValidate()
        {
            if (_volume == null)
                _volume = GetComponent<AirParticleBoxVolume>();
            if (_volume == null)
                _volume = gameObject.AddComponent<AirParticleBoxVolume>();
        }

        private void Awake()
        {
            if (_volume == null) _volume = GetComponent<AirParticleBoxVolume>();
            if (_volume == null) _volume = gameObject.AddComponent<AirParticleBoxVolume>();
            _debug = new AirParticleDebugView();
        }

        private void Update()
        {
            if (_volume != null)
                _debug.Draw(_volume);
        }

        private void OnDrawGizmos()
        {
            if (_volume == null)
                _volume = GetComponent<AirParticleBoxVolume>();
            if (_volume == null)
                return;

            if (_debug == null)
                _debug = new AirParticleDebugView();
            _debug.DrawGizmos(_volume);
        }

        public void SetCellDensity(float density)
        {
            if (_volume != null)
                _volume.CellDensity = density;
        }
    }
}
