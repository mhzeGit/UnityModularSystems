using UnityEngine;
using UnityEngine.Audio;

namespace MHZE.BasicMenus
{
    /// <summary>
    /// Small pooled AudioSource manager for UI sounds. Place one in the first
    /// scene (it persists across scenes) and optionally assign a default
    /// <see cref="UISoundProfile"/> and mixer group.
    /// </summary>
    public class UIAudioManager : MonoBehaviour
    {
        /// <summary>The active manager, if one exists.</summary>
        public static UIAudioManager Instance { get; private set; }

        [Header("Profile & Mixer")]
        [SerializeField] private UISoundProfile defaultProfile;
        [SerializeField] private AudioMixerGroup uiMixerGroup;

        [Header("Pooling")]
        [SerializeField] private int poolSize = 8;
        [SerializeField] private bool dontDestroyOnLoad = true;

        private AudioSource[] _pool;
        private int _poolIndex;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);

            BuildPool();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Play the clip mapped to <paramref name="state"/> from the given profile (or the default one).</summary>
        public void Play(UISoundProfile profile, UISoundProfile.UISoundState state)
        {
            var resolvedProfile = profile != null ? profile : defaultProfile;
            if (resolvedProfile == null) return;

            if (resolvedProfile.TryGet(state, out var clip, out var volume, out var pitch))
                PlayClip(clip, volume, pitch);
        }

        /// <summary>Play a one-shot clip through the pooled sources.</summary>
        public void PlayClip(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null || _pool == null || _pool.Length == 0) return;

            var source = _pool[_poolIndex];
            _poolIndex = (_poolIndex + 1) % _pool.Length;

            source.pitch = pitch;
            source.PlayOneShot(clip, volume);
        }

        private void BuildPool()
        {
            if (poolSize < 1) poolSize = 1;

            _pool = new AudioSource[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                var sourceObject = new GameObject($"UIAudioSource_{i}");
                sourceObject.transform.SetParent(transform);

                var source = sourceObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.outputAudioMixerGroup = uiMixerGroup;

                _pool[i] = source;
            }
        }
    }
}
