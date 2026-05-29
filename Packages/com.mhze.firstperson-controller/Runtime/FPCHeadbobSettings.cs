// ScriptableObject that configures headbob behaviour. Contains global enable toggle, smoothing rate, minimum speed threshold, and a list of speed-based presets with per-axis oscillation parameters. Also includes a serializable FPCHeadbobPreset struct for defining frequency, amplitude, and phase offset for both position and rotation waves.

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
        public float smoothing = 5f;
        [Tooltip("Global minimum speed. Below this, the first preset (idle) is used.")]
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
                positionFrequency = new Vector3(2f, 2f, 0.5f),
                positionAmplitude = new Vector3(0.04f, 0.07f, 0.1f),
                positionPhaseOffset = Vector3.zero,
                rotationFrequency = Vector3.zero,
                rotationAmplitude = new Vector3(2f, 0f, 0f),
                rotationPhaseOffset = Vector3.zero
            },
            new FPCHeadbobPreset
            {
                minSpeed = 2f,
                positionFrequency = new Vector3(6f, 6f, 10f),
                positionAmplitude = new Vector3(0.01f, 0.03f, 0.005f),
                positionPhaseOffset = Vector3.zero,
                rotationFrequency = new Vector3(6f, 3f, 0f),
                rotationAmplitude = new Vector3(1f, 1f, 0f),
                rotationPhaseOffset = new Vector3(1.5f, 0f, 0f)
            },
            new FPCHeadbobPreset
            {
                minSpeed = 4f,
                positionFrequency = new Vector3(10f, 10f, 10f),
                positionAmplitude = new Vector3(0.01f, 0.05f, 0.01f),
                positionPhaseOffset = Vector3.zero,
                rotationFrequency = new Vector3(10f, 5f, 10f),
                rotationAmplitude = new Vector3(1f, 2f, 0.5f),
                rotationPhaseOffset = new Vector3(1.77f, 0f, 0f)
            },
            new FPCHeadbobPreset
            {
                minSpeed = 7f,
                positionFrequency = new Vector3(20f, 20f, 20f),
                positionAmplitude = new Vector3(0.01f, 0.02f, 0.01f),
                positionPhaseOffset = Vector3.zero,
                rotationFrequency = new Vector3(20f, 10f, 20f),
                rotationAmplitude = new Vector3(1.5f, 2f, 0.5f),
                rotationPhaseOffset = new Vector3(1.5f, 0f, 0f)
            }
        };
    }

    [System.Serializable]
    public struct FPCHeadbobPreset
    {
        [Tooltip("Minimum speed required to activate this preset.")]
        public float minSpeed;
        [Tooltip("Per-axis position oscillation frequency. Controls how fast each axis oscillates.")]
        public Vector3 positionFrequency;
        [Tooltip("Per-axis position oscillation amplitude in local space (X = lateral sway, Y = vertical bob, Z = forward surge).")]
        public Vector3 positionAmplitude;
        [Tooltip("Per-axis position phase offset in radians. Shifts the wave to control the waveform type (0 = sine, 1.57 = cosine).")]
        public Vector3 positionPhaseOffset;
        [Tooltip("Per-axis rotation oscillation frequency. Controls how fast each axis oscillates.")]
        public Vector3 rotationFrequency;
        [Tooltip("Per-axis rotation oscillation amplitude in degrees (X = pitch nod, Y = yaw shake, Z = roll tilt).")]
        public Vector3 rotationAmplitude;
        [Tooltip("Per-axis rotation phase offset in radians. Shifts the wave to control the waveform type (0 = sine, 1.57 = cosine).")]
        public Vector3 rotationPhaseOffset;
    }
}
