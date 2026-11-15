using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ModularNPC
{
    /// <summary>
    /// A gesture <see cref="NpcDoorNavigation"/> plays before it opens a door: for example raise a hand,
    /// tap the door, lower the hand. Door navigation starts it once the NPC stands at the door, opens
    /// the door when <see cref="HasReachedContact"/> turns true, and walks on once the door is open and
    /// the gesture has finished. Without one, the door opens with no gesture.
    /// </summary>
    public interface INpcDoorOpenGesture
    {
        /// <summary>True when a gesture is set up and can be played now.</summary>
        bool CanPlay { get; }

        /// <summary>True from <see cref="Begin"/> until the gesture has finished or was cancelled.</summary>
        bool IsPlaying { get; }

        /// <summary>True once the gesture has reached the moment the door should open (stays true until the next Begin).</summary>
        bool HasReachedContact { get; }

        /// <summary>Starts the gesture toward a door. Returns false (and plays nothing) when it cannot play.</summary>
        bool Begin(Transform door);

        /// <summary>Stops the gesture at once and hands the pose back to the NPC's own animation.</summary>
        void Cancel();
    }

    /// <summary>
    /// Plays a door-opening gesture clip (see <see cref="NpcDoorOpenGestureProfile"/>) over the NPC's
    /// Animator when <see cref="NpcDoorNavigation"/> reaches a closed door, and tells it when the hand
    /// touches the door. The clip runs in its own playable graph on top of the Animator controller,
    /// blended in and out by the profile's weight curve, so the controller asset is never changed and
    /// the NPC's other animation resumes seamlessly. Add it only to NPCs that should gesture; every
    /// other NPC keeps opening doors without one.
    /// </summary>
    [Serializable]
    [NpcFeature(
        "Door Open Gesture",
        "Presentation",
        Description = "Plays a gesture clip (for example a light tap on the door) before Door Navigation opens a closed door; the door opens at the clip's contact moment. Clip, contact and blend curve come from a Door Open Gesture Profile asset.")]
    public sealed class NpcDoorOpenGesture : NpcFeature, INpcDoorOpenGesture, INpcTickable
    {
        [Tooltip("Clip, contact moment and blend curve of the gesture. Without a profile (or with a profile that has no clip) the NPC opens doors without a gesture.")]
        [SerializeField] private NpcDoorOpenGestureProfile _profile;

        [Tooltip("Animator the clip plays on. Empty: the Animation feature's Animator, else the first Animator under the NPC.")]
        [SerializeField] private Animator _animator;

        [NonSerialized] private PlayableGraph _graph;
        [NonSerialized] private AnimationClipPlayable _clipPlayable;
        [NonSerialized] private AnimationPlayableOutput _output;
        [NonSerialized] private NpcDoorOpenGestureProfile _playingProfile;
        [NonSerialized] private float _elapsed;
        [NonSerialized] private float _duration;
        [NonSerialized] private float _contactTime;
        [NonSerialized] private bool _playing;
        [NonSerialized] private bool _contactReached;

        public NpcTickSettings TickSettings => NpcTickSettings.EveryUpdate;

        public NpcDoorOpenGestureProfile Profile
        {
            get => _profile;
            set => _profile = value;
        }

        public bool CanPlay => IsOperational && _profile != null && _profile.Clip != null && ResolveAnimator() != null;

        public bool IsPlaying => _playing;

        public bool HasReachedContact => _contactReached;

        /// <summary>Seconds since the gesture started, 0 when idle.</summary>
        public float Elapsed => _playing ? _elapsed : 0f;

        public bool Begin(Transform door)
        {
            Cancel();
            _contactReached = false;
            if (!CanPlay)
                return false;

            // The profile is read once per gesture: edits apply from the next door.
            _playingProfile = _profile;
            AnimationClip clip = _playingProfile.Clip;
            _duration = _playingProfile.Duration;
            _contactTime = _playingProfile.ContactTime;
            _elapsed = 0f;

            // A game-time graph whose clip is frozen and positioned by hand every tick: the engine's
            // own animator pass then evaluates it together with the controller, in output order.
            _graph = PlayableGraph.Create("NpcDoorOpenGesture " + clip.name);
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            _clipPlayable = AnimationClipPlayable.Create(_graph, clip);
            _clipPlayable.SetApplyFootIK(false);
            _clipPlayable.SetSpeed(0d);
            _clipPlayable.SetTime(0d);
            _output = AnimationPlayableOutput.Create(_graph, "DoorOpenGesture", ResolveAnimator());
            _output.SetSourcePlayable(_clipPlayable);
            _output.SetWeight(_playingProfile.WeightAt(0f));
            _graph.Play();
            _playing = true;
            SetTicking(true);
            return true;
        }

        public void Cancel()
        {
            _playing = false;
            _playingProfile = null;
            if (_graph.IsValid())
                _graph.Destroy();
            if (IsInitialized)
                SetTicking(false);
        }

        public void Tick(float deltaTime)
        {
            if (!_playing)
                return;

            if (!_graph.IsValid() || _playingProfile == null)
            {
                Cancel();
                return;
            }

            _elapsed += Mathf.Max(0f, deltaTime);
            if (_elapsed >= _contactTime)
                _contactReached = true;

            if (_elapsed >= _duration)
            {
                Cancel();
                return;
            }

            float normalized = _duration > 0f ? _elapsed / _duration : 1f;
            _clipPlayable.SetTime(normalized * _playingProfile.Clip.length);
            _output.SetWeight(_playingProfile.WeightAt(normalized));
        }

        public override void CollectValidationIssues(List<NpcValidationIssue> issues)
        {
            if (_profile == null)
            {
                issues.Add(new NpcValidationIssue(NpcValidationSeverity.Warning,
                    "Door Open Gesture has no profile, so the NPC opens doors without a gesture."));
            }
            else if (_profile.Clip == null)
            {
                issues.Add(new NpcValidationIssue(NpcValidationSeverity.Warning,
                    $"Door Open Gesture profile '{_profile.name}' has no clip, so the NPC opens doors without a gesture.", _profile));
            }

            if (Npc != null && ResolveAnimator() == null)
            {
                issues.Add(new NpcValidationIssue(NpcValidationSeverity.Warning,
                    "Door Open Gesture found no Animator on this NPC to play its clip on."));
            }
        }

        protected override void OnFeatureDeactivated() => Cancel();
        protected override void OnFeatureShutdown() => Cancel();

        private Animator ResolveAnimator()
        {
            if (_animator != null)
                return _animator;
            if (Npc == null)
                return null;
            if (TryGetFeature(out NpcAnimation animation) && animation.Animator != null)
                return animation.Animator;
            return Npc.GetComponentInChildren<Animator>(true);
        }
    }
}
