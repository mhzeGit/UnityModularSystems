using UnityEngine;

namespace MHZE.RoughnessDetection
{
    public struct RoughnessResult
    {
        public bool hasHit;
        public float roughness;
        public RaycastHit hit;
        public Vector2 uv;

        public bool IsValid => hasHit && roughness >= 0f;

        public static readonly RoughnessResult Invalid = new RoughnessResult
        {
            hasHit = false,
            roughness = -1f
        };
    }
}
