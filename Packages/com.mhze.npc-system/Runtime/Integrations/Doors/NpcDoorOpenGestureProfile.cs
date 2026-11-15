using UnityEngine;

namespace ModularNPC
{
    /// <summary>
    /// How an NPC opens a door with a gesture: the clip it plays (raise a hand, tap the door, lower it),
    /// the moment in that clip the door is told to open, and the curve that blends the clip in over the
    /// NPC's own animation and back out again.
    ///
    /// <see cref="Weight"/> is one curve over the whole gesture. Its x axis is normalized clip time
    /// (0 = first frame, 1 = last frame, so the curve's duration is the clip's length divided by
    /// <see cref="Speed"/>); its y axis is how much the clip overrides the Animator controller (0 = the
    /// controller's pose only, 1 = the clip only). Values outside 0..1 are clamped.
    /// <see cref="ContactPoint"/> is the normalized clip time the hand touches the door; the door opens
    /// then. The NPC walks on once the door is open and the gesture has finished.
    /// </summary>
    [CreateAssetMenu(menuName = "Modular NPC/Door Open Gesture Profile", fileName = "NpcDoorOpenGestureProfile")]
    public sealed class NpcDoorOpenGestureProfile : ScriptableObject
    {
        [Tooltip("The gesture clip, played once over the NPC's Animator. It should start and end in the NPC's idle pose and keep its feet still.")]
        [SerializeField] private AnimationClip _clip;

        [Tooltip("Normalized clip time (0 = first frame, 1 = last frame) at which the hand touches the door. The door is told to open at this moment.")]
        [SerializeField, Range(0f, 1f)] private float _contactPoint = 0.4286f;

        [Tooltip("Playback speed of the clip. 1 plays it as authored; the gesture lasts the clip length divided by this.")]
        [SerializeField, Min(0.05f)] private float _speed = 1f;

        [Tooltip("Clip weight over the NPC's own animation across the gesture. X: normalized clip time 0..1. Y: 0 = the Animator controller only, 1 = the clip only. Keep it 0 at both ends so the gesture blends in and out without a pop.")]
        [SerializeField] private AnimationCurve _weight = DefaultCurve();

        public AnimationClip Clip => _clip;
        public float ContactPoint => _contactPoint;
        public float Speed => _speed;
        public AnimationCurve Weight => _weight;

        /// <summary>Seconds of real time the whole gesture takes, 0 without a clip.</summary>
        public float Duration => _clip != null ? _clip.length / Mathf.Max(0.05f, _speed) : 0f;

        /// <summary>Seconds from the start of the gesture to the contact.</summary>
        public float ContactTime => Duration * _contactPoint;

        /// <summary>
        /// Blends in over the first 12% of the clip (the clip's own first frames are still the idle
        /// pose), holds the clip fully, and blends back out over the last 15% while the arm settles.
        /// </summary>
        public static AnimationCurve DefaultCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 0f),
                new Keyframe(0.12f, 1f, 0f, 0f),
                new Keyframe(0.85f, 1f, 0f, 0f),
                new Keyframe(1f, 0f, 0f, 0f));
        }

        /// <summary>Clip weight at a normalized gesture time, clamped to 0..1.</summary>
        public float WeightAt(float normalizedTime)
        {
            AnimationCurve curve = _weight != null && _weight.length > 0 ? _weight : DefaultCurve();
            return Mathf.Clamp01(curve.Evaluate(Mathf.Clamp01(normalizedTime)));
        }
    }
}
