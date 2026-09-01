using System.Collections.Generic;
using UnityEngine;

namespace ModularNPC
{
    /// <summary>
    /// One project-wide runner for active feature work. It is created lazily, so completely
    /// idle NPCs add no MonoBehaviour Update calls of their own.
    /// </summary>
    [DefaultExecutionOrder(-9000)]
    internal sealed class NpcScheduler : MonoBehaviour
    {
        private sealed class Entry
        {
            public INpcTickable Target;
            public NpcTickSettings Settings;
            public float LastTickTime;
            public float NextTickTime;
            public int Index = -1;
            public bool Active;
        }

        private static NpcScheduler _instance;

        private readonly Dictionary<INpcTickable, Entry> _entries =
            new Dictionary<INpcTickable, Entry>(64);

        private readonly List<Entry> _updateEntries = new List<Entry>(64);
        private readonly List<Entry> _fixedEntries = new List<Entry>(32);
        private readonly List<Entry> _lateEntries = new List<Entry>(32);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }

        internal static void SetActive(INpcTickable target, bool active)
        {
            if (target == null)
            {
                return;
            }

            if (active)
            {
                EnsureInstance().Activate(target);
            }
            else if (_instance != null)
            {
                _instance.Deactivate(target);
            }
        }

        internal static void Refresh(INpcTickable target)
        {
            if (target != null && _instance != null)
            {
                _instance.RefreshEntry(target);
            }
        }

        internal static void Release(INpcTickable target)
        {
            if (target != null && _instance != null)
            {
                _instance.ReleaseEntry(target);
            }
        }

        private static NpcScheduler EnsureInstance()
        {
            if (_instance != null)
            {
                return _instance;
            }

            GameObject schedulerObject = new GameObject("[Modular NPC] Scheduler")
            {
                hideFlags = HideFlags.HideInHierarchy
            };
            DontDestroyOnLoad(schedulerObject);
            _instance = schedulerObject.AddComponent<NpcScheduler>();
            return _instance;
        }

        private void Update()
        {
            Process(_updateEntries, NpcTickPhase.Update, Time.deltaTime, Time.unscaledDeltaTime);
        }

        private void FixedUpdate()
        {
            Process(_fixedEntries, NpcTickPhase.FixedUpdate, Time.fixedDeltaTime, Time.fixedUnscaledDeltaTime);
        }

        private void LateUpdate()
        {
            Process(_lateEntries, NpcTickPhase.LateUpdate, Time.deltaTime, Time.unscaledDeltaTime);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Activate(INpcTickable target)
        {
            if (!_entries.TryGetValue(target, out Entry entry))
            {
                entry = new Entry { Target = target };
                _entries.Add(target, entry);
            }

            if (entry.Active)
            {
                RefreshEntry(target);
                return;
            }

            entry.Settings = target.TickSettings;
            entry.Active = true;
            AddToPhase(entry);
            ResetTiming(entry);
        }

        private void Deactivate(INpcTickable target)
        {
            if (!_entries.TryGetValue(target, out Entry entry) || !entry.Active)
            {
                return;
            }

            RemoveFromPhase(entry);
            entry.Active = false;
        }

        private void RefreshEntry(INpcTickable target)
        {
            if (!_entries.TryGetValue(target, out Entry entry))
            {
                return;
            }

            NpcTickSettings settings = target.TickSettings;
            if (entry.Active && entry.Settings.Phase != settings.Phase)
            {
                RemoveFromPhase(entry);
                entry.Settings = settings;
                AddToPhase(entry);
            }
            else
            {
                entry.Settings = settings;
            }

            ResetTiming(entry);
        }

        private void ReleaseEntry(INpcTickable target)
        {
            if (!_entries.TryGetValue(target, out Entry entry))
            {
                return;
            }

            if (entry.Active)
            {
                RemoveFromPhase(entry);
            }

            _entries.Remove(target);
        }

        private void Process(
            List<Entry> entries,
            NpcTickPhase phase,
            float scaledFrameDelta,
            float unscaledFrameDelta)
        {
            int index = 0;
            while (index < entries.Count)
            {
                Entry entry = entries[index];
                if (!entry.Active || IsDestroyedUnityObject(entry.Target))
                {
                    ReleaseEntry(entry.Target);
                    continue;
                }

                float now = CurrentTime(entry.Settings.UseUnscaledTime, phase);
                if (entry.Settings.Interval <= 0f || now >= entry.NextTickTime)
                {
                    float deltaTime = entry.Settings.Interval <= 0f
                        ? (entry.Settings.UseUnscaledTime ? unscaledFrameDelta : scaledFrameDelta)
                        : Mathf.Max(0f, now - entry.LastTickTime);

                    entry.LastTickTime = now;
                    entry.NextTickTime = entry.Settings.Interval <= 0f
                        ? now
                        : now + entry.Settings.Interval;

                    entry.Target.Tick(deltaTime);
                }

                // A tick may deactivate itself, which swap-removes this list element.
                if (index < entries.Count && ReferenceEquals(entries[index], entry))
                {
                    index++;
                }
            }
        }

        private void AddToPhase(Entry entry)
        {
            List<Entry> list = GetPhaseList(entry.Settings.Phase);
            entry.Index = list.Count;
            list.Add(entry);
        }

        private void RemoveFromPhase(Entry entry)
        {
            List<Entry> list = GetPhaseList(entry.Settings.Phase);
            int index = entry.Index;
            int lastIndex = list.Count - 1;
            if (index < 0 || index > lastIndex || !ReferenceEquals(list[index], entry))
            {
                entry.Index = -1;
                return;
            }

            Entry last = list[lastIndex];
            list[index] = last;
            last.Index = index;
            list.RemoveAt(lastIndex);
            entry.Index = -1;
        }

        private List<Entry> GetPhaseList(NpcTickPhase phase)
        {
            switch (phase)
            {
                case NpcTickPhase.FixedUpdate:
                    return _fixedEntries;
                case NpcTickPhase.LateUpdate:
                    return _lateEntries;
                default:
                    return _updateEntries;
            }
        }

        private static void ResetTiming(Entry entry)
        {
            float now = CurrentTime(entry.Settings.UseUnscaledTime, entry.Settings.Phase);
            entry.LastTickTime = now;
            entry.NextTickTime = entry.Settings.Interval <= 0f ? now : now + entry.Settings.Interval;
        }

        private static float CurrentTime(bool unscaled, NpcTickPhase phase)
        {
            if (unscaled)
            {
                return Time.unscaledTime;
            }

            return phase == NpcTickPhase.FixedUpdate ? Time.fixedTime : Time.time;
        }

        private static bool IsDestroyedUnityObject(INpcTickable target)
        {
            return target is UnityEngine.Object unityObject && unityObject == null;
        }
    }
}
