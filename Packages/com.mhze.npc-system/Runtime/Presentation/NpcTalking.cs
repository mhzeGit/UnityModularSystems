using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace ModularNPC
{
    /// <summary>
    /// Plays the NPC's talking animation for exactly as long as its voice audio is playing.
    ///
    /// The controller contract is one boolean animator parameter, <see cref="TalkingParameterName"/>
    /// (default "Talking"): the feature raises it while a voice <see cref="AudioSource"/> under the NPC
    /// plays a clip and clears it the moment the clip stops, so the animation duration always matches
    /// the spoken audio instead of the on-screen text duration. A character without a talking state
    /// simply keeps the parameter and plays its normal state.
    ///
    /// The watched source defaults to any <see cref="AudioSource"/> under the NPC, which finds a dialog
    /// system's voice source on the character's head bone automatically. Assign
    /// <see cref="VoiceSource"/> to pin one source explicitly.
    /// </summary>
    [Serializable]
    [MovedFrom(true, "BlahBlahFamily.Gameplay", "Assembly-CSharp", null)]
    [NpcFeature(
        "Talking",
        "Presentation",
        Description = "Raises a boolean animator talking state while the NPC's voice AudioSource is playing, so the animation lasts exactly as long as the spoken line's audio.")]
    public sealed class NpcTalking : NpcAnimatorFlagFeature
    {
        /// <summary>Canonical animator bool parameter. Controller generators should write the same name.</summary>
        public const string TalkingParameterName = "Talking";

        [SerializeField, Tooltip("Animator bool parameter raised while the voice audio plays.")]
        private string _talkingParameter = TalkingParameterName;

        [SerializeField, Tooltip("Voice source to watch. Leave empty to watch every AudioSource under the NPC, which finds a dialog system's head-bone voice source automatically.")]
        private AudioSource _voiceSource;

        [NonSerialized] private AudioSource[] _sources;

        /// <summary>True while the talking animation is playing.</summary>
        public bool IsTalking => IsFlagRaised;

        /// <summary>Animator bool parameter this feature drives.</summary>
        public string TalkingParameter =>
            string.IsNullOrEmpty(_talkingParameter) ? TalkingParameterName : _talkingParameter;

        /// <summary>Voice source to watch, or null to watch every source under the NPC.</summary>
        public AudioSource VoiceSource
        {
            get => _voiceSource;
            set => _voiceSource = value;
        }

        protected override string ParameterName => TalkingParameter;

        protected override void OnFeatureInitialized()
        {
            RefreshVoiceSources();
            base.OnFeatureInitialized();
        }

        protected override void OnFeatureShutdown()
        {
            base.OnFeatureShutdown();
            _sources = null;
        }

        /// <summary>Re-scans the AudioSources under the NPC after voice sources change at runtime.</summary>
        public void RefreshVoiceSources()
        {
            _sources = Npc != null
                ? Npc.GetComponentsInChildren<AudioSource>(true)
                : Array.Empty<AudioSource>();
        }

        protected override bool EvaluateFlag()
        {
            return IsVoicePlaying();
        }

        private bool IsVoicePlaying()
        {
            if (_voiceSource != null)
            {
                return _voiceSource.isPlaying;
            }

            if (_sources == null)
            {
                RefreshVoiceSources();
            }

            for (int i = 0; i < _sources.Length; i++)
            {
                AudioSource source = _sources[i];
                if (source != null && source.isPlaying)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
