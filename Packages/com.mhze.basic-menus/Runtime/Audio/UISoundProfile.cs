using System.Collections.Generic;
using UnityEngine;

namespace MHZE.BasicMenus
{
    /// <summary>
    /// ScriptableObject describing which clips (with random volume/pitch ranges)
    /// are played for each UI interaction state. Create one per game or one per
    /// menu style via Assets > Create > Basic Menus > UI Sound Profile.
    /// </summary>
    [CreateAssetMenu(menuName = "Basic Menus/UI Sound Profile", fileName = "UISoundProfile")]
    public class UISoundProfile : ScriptableObject
    {
        public enum UISoundState
        {
            Click,
            Hover,
            Select,
            Press,
            Release
        }

        [System.Serializable]
        public class StateAudio
        {
            public UISoundState state;
            public bool enabled = true;
            public AudioClip[] clips;
            public Vector2 volumeRange = new Vector2(1f, 1f);
            public Vector2 pitchRange = new Vector2(1f, 1f);

            public bool TryGetRandom(out AudioClip clip, out float volume, out float pitch)
            {
                clip = null;
                volume = 1f;
                pitch = 1f;

                if (!enabled || clips == null || clips.Length == 0)
                    return false;

                clip = clips[Random.Range(0, clips.Length)];
                volume = Random.Range(volumeRange.x, volumeRange.y);
                pitch = Random.Range(pitchRange.x, pitchRange.y);
                return clip != null;
            }
        }

        [SerializeField] private List<StateAudio> stateAudio = new List<StateAudio>();

        private Dictionary<UISoundState, StateAudio> _cache;

        /// <summary>Try to resolve a random clip (with volume/pitch) for a state.</summary>
        public bool TryGet(UISoundState state, out AudioClip clip, out float volume, out float pitch)
        {
            EnsureCache();
            if (_cache.TryGetValue(state, out var audio) && audio != null)
                return audio.TryGetRandom(out clip, out volume, out pitch);

            clip = null;
            volume = 1f;
            pitch = 1f;
            return false;
        }

        private void EnsureCache()
        {
            if (_cache != null) return;

            _cache = new Dictionary<UISoundState, StateAudio>();
            for (int i = 0; i < stateAudio.Count; i++)
            {
                var entry = stateAudio[i];
                if (entry == null) continue;
                _cache[entry.state] = entry;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            for (int i = 0; i < stateAudio.Count; i++)
            {
                var entry = stateAudio[i];
                if (entry == null) continue;

                if (entry.volumeRange.x > entry.volumeRange.y)
                    entry.volumeRange = new Vector2(entry.volumeRange.y, entry.volumeRange.x);

                if (entry.pitchRange.x > entry.pitchRange.y)
                    entry.pitchRange = new Vector2(entry.pitchRange.y, entry.pitchRange.x);
            }
        }
#endif
    }
}
