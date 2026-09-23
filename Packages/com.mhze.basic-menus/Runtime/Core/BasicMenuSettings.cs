using UnityEngine;
using UnityEngine.Audio;

namespace MHZE.BasicMenus
{
    /// <summary>
    /// Applies saved options at game start, before the first scene loads.
    /// Only values that were actually saved are applied, so games that never
    /// used the options screen keep their project settings untouched.
    ///
    /// Video settings are applied automatically. Audio settings need either the
    /// options screen's mixer reference or an explicit call to
    /// <see cref="ApplySavedAudioSettings"/> from the game's audio bootstrap.
    /// </summary>
    public static class BasicMenuSettings
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplySavedSettingsOnStartup()
        {
            ApplySavedVideoSettings();
        }

        /// <summary>Apply saved quality, window mode, resolution, VSync and frame rate settings.</summary>
        public static void ApplySavedVideoSettings()
        {
            if (PlayerPrefs.HasKey(OptionsScreen.PrefQuality))
                QualitySettings.SetQualityLevel(PlayerPrefs.GetInt(OptionsScreen.PrefQuality));

            if (PlayerPrefs.HasKey(OptionsScreen.PrefWindowMode))
                Screen.fullScreenMode = (FullScreenMode)PlayerPrefs.GetInt(OptionsScreen.PrefWindowMode);

            if (PlayerPrefs.HasKey(OptionsScreen.PrefResolutionWidth) && PlayerPrefs.HasKey(OptionsScreen.PrefResolutionHeight))
            {
                Screen.SetResolution(
                    PlayerPrefs.GetInt(OptionsScreen.PrefResolutionWidth),
                    PlayerPrefs.GetInt(OptionsScreen.PrefResolutionHeight),
                    Screen.fullScreenMode);
            }

            if (PlayerPrefs.HasKey(OptionsScreen.PrefVSync))
                QualitySettings.vSyncCount = PlayerPrefs.GetInt(OptionsScreen.PrefVSync) == 1 ? 1 : 0;

            if (PlayerPrefs.HasKey(OptionsScreen.PrefFrameRate))
                Application.targetFrameRate = PlayerPrefs.GetInt(OptionsScreen.PrefFrameRate);
        }

        /// <summary>
        /// Apply saved volume settings. Pass the game's AudioMixer (with exposed
        /// parameters MasterVolume / MusicVolume / SFXVolume / DialogVolume) or
        /// null to apply master volume through AudioListener.volume.
        /// </summary>
        public static void ApplySavedAudioSettings(AudioMixer audioMixer = null)
        {
            ApplySavedVolume(OptionsScreen.PrefVolumeMaster, OptionsScreen.MixerParamMaster, audioMixer);
            ApplySavedVolume(OptionsScreen.PrefVolumeMusic, OptionsScreen.MixerParamMusic, audioMixer);
            ApplySavedVolume(OptionsScreen.PrefVolumeSFX, OptionsScreen.MixerParamSFX, audioMixer);
            ApplySavedVolume(OptionsScreen.PrefVolumeDialog, OptionsScreen.MixerParamDialog, audioMixer);
        }

        private static void ApplySavedVolume(string prefKey, string mixerParam, AudioMixer audioMixer)
        {
            if (!PlayerPrefs.HasKey(prefKey)) return;

            float linear = PlayerPrefs.GetFloat(prefKey);

            if (audioMixer != null)
            {
                float decibels = linear <= 0.0001f ? -80f : Mathf.Log10(Mathf.Clamp(linear, 0.0001f, 1f)) * 20f;
                audioMixer.SetFloat(mixerParam, decibels);
            }
            else if (mixerParam == OptionsScreen.MixerParamMaster)
            {
                AudioListener.volume = linear;
            }
        }
    }
}
