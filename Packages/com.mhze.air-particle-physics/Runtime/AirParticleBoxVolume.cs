using UnityEngine;

namespace MHZE.AirParticlePhysics
{
    public class AirParticleBoxVolume : MonoBehaviour
    {
        [SerializeField] private Vector3 _size = Vector3.one;

        public Vector3 Size
        {
            get => _size;
            set => _size = value;
        }

        public Vector3 Center => transform.position;

        public Vector3 Min => Center - _size * 0.5f;
        public Vector3 Max => Center + _size * 0.5f;

        public Bounds Bounds => new Bounds(Center, _size);

        public Vector3 RandomPoint()
        {
            Vector3 half = _size * 0.5f;
            return Center + new Vector3(
                Random.Range(-half.x, half.x),
                Random.Range(-half.y, half.y),
                Random.Range(-half.z, half.z)
            );
        }

        public bool ContainsPoint(Vector3 point)
        {
            Vector3 half = _size * 0.5f;
            Vector3 local = point - Center;
            return Mathf.Abs(local.x) <= half.x
                && Mathf.Abs(local.y) <= half.y
                && Mathf.Abs(local.z) <= half.z;
        }

        public Vector3 ClampPoint(Vector3 point)
        {
            Vector3 half = _size * 0.5f;
            Vector3 local = point - Center;
            local.x = Mathf.Clamp(local.x, -half.x, half.x);
            local.y = Mathf.Clamp(local.y, -half.y, half.y);
            local.z = Mathf.Clamp(local.z, -half.z, half.z);
            return Center + local;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.3f);
            Gizmos.DrawWireCube(Center, _size);
        }
    }
}
