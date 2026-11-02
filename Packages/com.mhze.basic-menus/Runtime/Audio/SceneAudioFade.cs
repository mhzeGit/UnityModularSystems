using System.Collections;
using UnityEngine;

namespace MHZE.BasicMenus
{
    /// <summary>Global scene-transition gain, independent of source volumes and the player's master setting.</summary>
    public static class SceneAudioFade
    {
        private static float masterVolume;
        private static bool initialized;
        public static float Gain { get; private set; } = 1;

        /// <summary>Use this instead of writing AudioListener.volume so settings remain valid during a fade.</summary>
        public static float MasterVolume
        {
            get { Initialize(); return masterVolume; }
            set { Initialize(); masterVolume = Mathf.Clamp01(value); AudioListener.volume = masterVolume * Gain; }
        }

        private static void Initialize()
        {
            if (initialized) return;
            masterVolume = AudioListener.volume;
            initialized = true;
        }

        /// <summary>Set transition attenuation without changing any AudioSource or AudioMixer setting.</summary>
        public static void SetGain(float gain)
        {
            Initialize();
            Gain = Mathf.Clamp01(gain);
            AudioListener.volume = masterVolume * Gain;
        }

        /// <summary>Fade using unscaled time; custom loaders must call Restore in their cleanup.</summary>
        public static IEnumerator FadeTo(float target, float seconds)
        {
            float from = Gain;
            target = Mathf.Clamp01(target);
            for (float elapsed = 0; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
            {
                SetGain(Mathf.Lerp(from, target, Mathf.SmoothStep(0, 1, elapsed / seconds)));
                yield return null;
            }
            SetGain(target);
        }

        /// <summary>Release transition attenuation on completion, cancellation or destruction.</summary>
        public static void Restore()
        {
            if (initialized) SetGain(1);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Restore();
            initialized = false;
        }
    }
}
