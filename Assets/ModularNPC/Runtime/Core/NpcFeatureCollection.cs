using System;
using System.Collections.Generic;

namespace ModularNPC
{
    /// <summary>
    /// Allocation-free-after-warmup lookup table for the features owned by one NPC.
    /// Interfaces and base classes can be queried as capabilities.
    /// </summary>
    public sealed class NpcFeatureCollection
    {
        private readonly Npc _owner;
        private readonly List<NpcFeature> _features = new List<NpcFeature>(8);
        private readonly Dictionary<Type, NpcFeature> _firstMatchCache = new Dictionary<Type, NpcFeature>(16);
        private int _version;

        internal NpcFeatureCollection(Npc owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public int Count => _features.Count;

        /// <summary>Changes whenever a feature is added, removed, or the registry is rebuilt.</summary>
        public int Version => _version;

        public NpcFeature this[int index] => _features[index];

        public bool TryGet<TCapability>(out TCapability capability) where TCapability : class
        {
            if (TryGet(typeof(TCapability), false, out NpcFeature feature))
            {
                capability = feature as TCapability;
                return capability != null;
            }

            capability = null;
            return false;
        }

        public bool TryGetOperational<TCapability>(out TCapability capability) where TCapability : class
        {
            if (TryGet(typeof(TCapability), true, out NpcFeature feature))
            {
                capability = feature as TCapability;
                return capability != null;
            }

            capability = null;
            return false;
        }

        public TCapability Get<TCapability>() where TCapability : class
        {
            if (TryGet(out TCapability capability))
            {
                return capability;
            }

            throw new InvalidOperationException(
                $"NPC '{_owner.name}' does not contain a feature implementing {typeof(TCapability).FullName}.");
        }

        public bool Contains(Type capabilityType, bool operationalOnly = false)
        {
            return TryGet(capabilityType, operationalOnly, out _);
        }

        /// <summary>Adds every matching feature to a caller-owned list without allocating.</summary>
        public int GetAll<TCapability>(List<TCapability> results, bool operationalOnly = false)
            where TCapability : class
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            int initialCount = results.Count;
            for (int i = 0; i < _features.Count; i++)
            {
                NpcFeature feature = _features[i];
                if (feature == null || (operationalOnly && !feature.IsOperational))
                {
                    continue;
                }

                if (feature is TCapability match)
                {
                    results.Add(match);
                }
            }

            return results.Count - initialCount;
        }

        internal bool Register(NpcFeature feature)
        {
            if (feature == null || _features.Contains(feature))
            {
                return false;
            }

            _features.Add(feature);
            Invalidate();
            return true;
        }

        internal bool Unregister(NpcFeature feature)
        {
            if (feature == null || !_features.Remove(feature))
            {
                return false;
            }

            Invalidate();
            return true;
        }

        internal void ReplaceWith(List<NpcFeature> features)
        {
            _features.Clear();
            for (int i = 0; i < features.Count; i++)
            {
                NpcFeature feature = features[i];
                if (feature != null && !_features.Contains(feature))
                {
                    _features.Add(feature);
                }
            }

            Invalidate();
        }

        internal void InvalidateOperationalCache()
        {
            // The cache only stores unrestricted queries, but clearing it keeps dynamic
            // component/interface changes predictable for custom feature implementations.
            _firstMatchCache.Clear();
        }

        private bool TryGet(Type capabilityType, bool operationalOnly, out NpcFeature result)
        {
            if (capabilityType == null)
            {
                throw new ArgumentNullException(nameof(capabilityType));
            }

            if (!operationalOnly && _firstMatchCache.TryGetValue(capabilityType, out result))
            {
                if (result != null)
                {
                    return true;
                }

                _firstMatchCache.Remove(capabilityType);
            }

            for (int i = 0; i < _features.Count; i++)
            {
                NpcFeature feature = _features[i];
                if (feature == null || (operationalOnly && !feature.IsOperational))
                {
                    continue;
                }

                if (!capabilityType.IsInstanceOfType(feature))
                {
                    continue;
                }

                result = feature;
                if (!operationalOnly)
                {
                    _firstMatchCache[capabilityType] = feature;
                }

                return true;
            }

            result = null;
            return false;
        }

        private void Invalidate()
        {
            unchecked
            {
                _version++;
            }

            _firstMatchCache.Clear();
        }
    }
}
