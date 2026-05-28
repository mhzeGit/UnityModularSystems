using UnityEngine;

namespace MHZE.FirstPersonController
{
    /// <summary>
    /// Thin wrapper around FPCLook's force-look functionality with a simpler public API.
    /// </summary>
    public class FPCForceLook
    {
        private readonly FPCLook look;
        private readonly FPCSettings settings;

        public bool IsActive => look != null && look.IsForced;

        public FPCForceLook(FPCLook lookModule, FPCSettings settings)
        {
            this.look = lookModule;
            this.settings = settings;
        }

        public void LookAt(Vector3 worldPoint, float? duration = null, float? speed = null)
        {
            if (look == null) return;
            look.ForceLookAt(worldPoint, duration, speed);
        }

        public void LookAt(Transform target, float? duration = null, float? speed = null)
        {
            if (look == null || target == null) return;
            look.ForceLookAt(target, duration, speed);
        }

        public void Release(bool snapBack = false)
        {
            if (look == null) return;
            look.StopForceLook(snapBack);
        }

        public void Update(float deltaTime)
        {
            // FPCLook handles its own update; this wrapper is for state tracking
        }
    }
}