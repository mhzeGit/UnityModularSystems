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
    ///
    /// Lines without audio can still move the mouth: <see cref="Hold"/> keeps the talking state raised for
    /// as long as any holder asks (the Dialog System integration's <c>NpcDialogTalking</c> does this while
    /// the character's silent line is on screen).
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

        /// <summary>Seconds between idle rescans for voice sources added after initialization.</summary>
        private const float SourceRefreshInterval = 1f;

        [SerializeField, Tooltip("Animator bool parameter raised while the voice audio plays.")]
        private string _talkingParameter = TalkingParameterName;

        [SerializeField, Tooltip("Voice source to watch. Leave empty to watch every AudioSource under the NPC, which finds a dialog system's head-bone voice source automatically.")]
        private AudioSource _voiceSource;

        [NonSerialized] private AudioSource[] _sources;
        [NonSerialized] private float _nextSourceRefreshTime;
        [NonSerialized] private System.Collections.Generic.HashSet<object> _holders;

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
            _holders?.Clear();
        }

        /// <summary>
        /// Re-scans the AudioSources under the NPC. Called on initialization and periodically while
        /// silent, so a voice source that is added at runtime (for example one instantiated by a
        /// dialog system) is picked up; call it directly to react to a change immediately.
        /// </summary>
        public void RefreshVoiceSources()
        {
            _sources = Npc != null
                ? Npc.GetComponentsInChildren<AudioSource>(true)
                : Array.Empty<AudioSource>();
            _nextSourceRefreshTime = Time.unscaledTime + SourceRefreshInterval;
        }

        /// <summary>
        /// Keeps the talking state raised while <paramref name="talking"/> is true for
        /// <paramref name="holder"/>, whether or not any voice audio plays (a line without a voice clip).
        /// Several holders can hold at once; the state drops when none holds and no voice plays.
        /// </summary>
        public void Hold(object holder, bool talking)
        {
            if (holder == null)
            {
                return;
            }

            if (talking)
            {
                (_holders ??= new System.Collections.Generic.HashSet<object>()).Add(holder);
            }
            else
            {
                _holders?.Remove(holder);
            }
        }

        /// <summary>True while at least one <see cref="Hold"/> caller keeps the talking state up.</summary>
        public bool IsHeld => _holders != null && _holders.Count > 0;

        protected override bool EvaluateFlag()
        {
            return IsHeld || IsVoicePlaying();
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

            if (AnySourcePlaying())
            {
                return true;
            }

            // Nothing is playing, so an occasional rescan is cheap: it finds sources added after
            // initialization without paying for a hierarchy walk while a voice clip is running.
            if (Time.unscaledTime < _nextSourceRefreshTime)
            {
                return false;
            }

            RefreshVoiceSources();
            return AnySourcePlaying();
        }

        private bool AnySourcePlaying()
        {
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
