using System.Collections.Generic;
using UnityEngine;

namespace MHZE.FirstPersonController
{
    [CreateAssetMenu(fileName = "FPCHeadbobSettings", menuName = "MHZE/First Person Controller/Headbob Settings", order = 2)]
    public class FPCHeadbobSettings : ScriptableObject
    {
        [Header("Global")]
        public bool enabled = true;
        [Tooltip("How smoothly the headbob fades in/out and transitions between presets.")]
        public float smoothing = 8f;
        [Tooltip("Global minimum speed. Below this, headbob is disabled entirely.")]
        public float minSpeed = 0.1f;
        [Tooltip("Log headbob state to console.")]
        public bool debugLogging = false;

        [Header("Speed-Based Presets")]
        [Tooltip("Evaluated in order. The preset with the highest minSpeed <= current speed is used.")]
        public List<FPCHeadbobPreset> presets = new List<FPCHeadbobPreset>
        {
            new FPCHeadbobPreset
            {
                minSpeed = 0f,
                positionFrequency = 1.5f,
                positionAmplitude = new Vector3(0.015f, 0.01f, 0.005f),
                rotationFrequency = 2f,
                rotationAmplitude = new Vector3(0.5f, 0.1f, 1f)
            },
            new FPCHeadbobPreset
            {
                minSpeed = 2f,
                positionFrequency = 4f,
                positionAmplitude = new Vector3(0.04f, 0.03f, 0.01f),
                rotationFrequency = 4f,
                rotationAmplitude = new Vector3(0.5f, 0.2f, 1.5f)
            },
            new FPCHeadbobPreset
            {
                minSpeed = 6f,
                positionFrequency = 6f,
                positionAmplitude = new Vector3(0.06f, 0.05f, 0.015f),
                rotationFrequency = 5f,
                rotationAmplitude = new Vector3(0.8f, 0.3f, 2f)
            }
        };
    }

    [System.Serializable]
    public struct FPCHeadbobPreset
    {
        [Tooltip("Minimum speed required to activate this preset.")]
        public float minSpeed;
        [Tooltip("Position oscillation rate. Higher = faster bob cycles.")]
        public float positionFrequency;
        [Tooltip("Position oscillation amplitude in local space (X = lateral sway, Y = vertical bob, Z = forward surge).")]
        public Vector3 positionAmplitude;
        [Tooltip("Rotation oscillation rate. Higher = faster bob cycles.")]
        public float rotationFrequency;
        [Tooltip("Rotation oscillation amplitude in degrees (X = pitch nod, Y = yaw shake, Z = roll tilt).")]
        public Vector3 rotationAmplitude;
    }
}
