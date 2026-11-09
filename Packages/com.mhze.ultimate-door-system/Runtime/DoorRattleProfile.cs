using UnityEngine;

namespace MHZE.UltimateDoorSystem
{
    /// <summary>
    /// Editable profile for the small shake a locked door plays when someone tries it: a curve of
    /// the offset over the shake (-1..1, ending at 0), its duration, the peak angle or slide
    /// distance, and the locked-sound variation. Assign it on <see cref="UltimateDoorController"/>;
    /// a door without one uses <see cref="DoorRattleProfile.Default"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "MHZE/Ultimate Door System/Door Rattle Profile", fileName = "DoorRattleProfile")]
    public class DoorRattleProfile : ScriptableObject
    {
        [Tooltip("Offset over the shake: x = 0..1 of the duration, y = -1..1 of the peak. Must end at 0.")]
        public AnimationCurve offsetCurve = DampedSwings(3, 0.45f);
        [Tooltip("Total shake time in seconds.")]
        public float duration = 0.5f;
        [Tooltip("Peak swing in degrees for a rotating door (times the door's own amplitude).")]
        public float peakDegrees = 2.2f;
        [Tooltip("Peak nudge in metres for a sliding door (times the door's own amplitude).")]
        public float peakSlide = 0.03f;
        [Tooltip("Minimum seconds between two locked sounds, so click-spam does not stack them.")]
        public float soundCooldown = 0.35f;
        [Tooltip("Random pitch spread around 1 for the locked sound.")]
        public float pitchVariation = 0.08f;
        [Tooltip("Random volume spread below 1 for the locked sound.")]
        public float volumeVariation = 0.15f;

        private static DoorRattleProfile _default;

        /// <summary>Runtime fallback with the defaults above.</summary>
        public static DoorRattleProfile Default
        {
            get
            {
                if (_default == null)
                {
                    _default = CreateInstance<DoorRattleProfile>();
                    _default.hideFlags = HideFlags.HideAndDontSave;
                }
                return _default;
            }
        }

        /// <summary>A sine with the given swing count that fades to 0 at the end.</summary>
        public static AnimationCurve DampedSwings(int swings, float damping)
        {
            const int samples = 7;
            int count = swings * samples + 1;
            var keys = new Keyframe[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                float v = Mathf.Sin(2f * Mathf.PI * swings * t) * (1f - damping * t);
                keys[i] = new Keyframe(t, v);
            }
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < count; i++) curve.SmoothTangents(i, 0f);
            return curve;
        }
    }
}
