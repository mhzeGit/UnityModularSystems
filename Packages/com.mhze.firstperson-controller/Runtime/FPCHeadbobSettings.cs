using UnityEngine;

namespace MHZE.FirstPersonController
{
    [CreateAssetMenu(fileName = "FPCHeadbobSettings", menuName = "MHZE/First Person Controller/Headbob Settings", order = 2)]
    public class FPCHeadbobSettings : ScriptableObject
    {
        [Header("Global")]
        public bool enabled = true;
        [Tooltip("How smoothly the headbob fades in/out when starting/stopping movement.")]
        public float smoothing = 6f;
        [Tooltip("Minimum horizontal speed required for headbob to activate.")]
        public float minSpeed = 0.5f;

        [Header("State Presets")]
        public FPCHeadbobPreset idle = new FPCHeadbobPreset
        {
            positionFrequency = 1.5f,
            rotationFrequency = 2f,
            positionAmplitude = new Vector2(0.015f, 0.01f),
            rotationAmplitude = new Vector2(0.5f, 0.3f)
        };

        public FPCHeadbobPreset walking = new FPCHeadbobPreset
        {
            positionFrequency = 4f,
            rotationFrequency = 4f,
            positionAmplitude = new Vector2(0.04f, 0.03f),
            rotationAmplitude = new Vector2(0.5f, 0.3f)
        };

        public FPCHeadbobPreset running = new FPCHeadbobPreset
        {
            positionFrequency = 6f,
            rotationFrequency = 5f,
            positionAmplitude = new Vector2(0.06f, 0.05f),
            rotationAmplitude = new Vector2(0.8f, 0.5f)
        };

        public FPCHeadbobPreset crouching = new FPCHeadbobPreset
        {
            positionFrequency = 3f,
            rotationFrequency = 3.5f,
            positionAmplitude = new Vector2(0.02f, 0.015f),
            rotationAmplitude = new Vector2(0.2f, 0.1f)
        };

        public FPCHeadbobPreset airborne = new FPCHeadbobPreset
        {
            positionFrequency = 2f,
            rotationFrequency = 2.5f,
            positionAmplitude = new Vector2(0.01f, 0.005f),
            rotationAmplitude = new Vector2(0.1f, 0.05f)
        };
    }

    [System.Serializable]
    public class FPCHeadbobPreset
    {
        [Tooltip("Position oscillation rate. Higher = faster bob cycles.")]
        public float positionFrequency = 4f;
        [Tooltip("Position oscillation amplitude in local space (X = lateral sway, Y = vertical bob).")]
        public Vector2 positionAmplitude = new Vector2(0.04f, 0.03f);
        [Tooltip("Rotation oscillation rate. Higher = faster bob cycles.")]
        public float rotationFrequency = 4f;
        [Tooltip("Rotation oscillation amplitude in degrees (X = pitch nod, Z = roll tilt).")]
        public Vector2 rotationAmplitude = new Vector2(0.5f, 0.3f);
    }
}
