using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModularNPC
{
    /// <summary>
    /// The only NPC component placed on a GameObject. It stores, coordinates, and exposes
    /// serializable feature modules without containing feature-specific gameplay logic.
    /// </summary>
    [AddComponentMenu("Modular NPC/NPC")]
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class Npc : MonoBehaviour, INpcValidatable, ISerializationCallbackReceiver
    {
        [SerializeReference, HideInInspector]
        private List<NpcFeature> _featureModules = new List<NpcFeature>(8);

        private readonly List<NpcFeature> _moduleBuffer = new List<NpcFeature>(8);
        private readonly List<NpcFeature> _previousFeatures = new List<NpcFeature>(8);
        private NpcFeatureCollection _features;
        private bool _initialized;
        private bool _ownerAvailable;
        private bool _isShuttingDown;
        private bool _registryDirty = true;

        public event Action<Npc> Initialized;

        public event Action<Npc> ShuttingDown;

        public NpcFeatureCollection Features
        {
            get
            {
                EnsureRegistrySynchronized();
                return EnsureFeatureCollection();
            }
        }

        public bool IsInitialized => _initialized;

        public bool IsOperational => _initialized && _ownerAvailable;

        private void Awake()
        {
            RefreshFeatures();
            Initialize();
        }

        private void OnEnable()
        {
            if (Application.isPlaying && !_initialized)
            {
                Initialize();
            }

            SetOwnerAvailable(Application.isPlaying && isActiveAndEnabled);
        }

        private void OnDisable()
        {
            SetOwnerAvailable(false);
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        private void OnValidate()
        {
            RefreshRegistryOnly();
            for (int i = 0; i < _moduleBuffer.Count; i++)
            {
                _moduleBuffer[i].HandleValidate(this);
            }
        }

        /// <summary>Initializes all internal features. Calling it repeatedly is safe.</summary>
        public void Initialize()
        {
            if (_initialized || _isShuttingDown || !Application.isPlaying)
            {
                return;
            }

            RefreshFeatures();
            _initialized = true;

            NpcFeatureCollection collection = EnsureFeatureCollection();
            for (int i = 0; i < collection.Count; i++)
            {
                collection[i].HandleInitialize(this);
            }

            SetOwnerAvailable(isActiveAndEnabled);
            Initialized?.Invoke(this);
        }

        /// <summary>Stops all active work and shuts features down in reverse order.</summary>
        public void Shutdown()
        {
            if (!_initialized || _isShuttingDown)
            {
                return;
            }

            _isShuttingDown = true;
            SetOwnerAvailable(false);
            ShuttingDown?.Invoke(this);

            NpcFeatureCollection collection = EnsureFeatureCollection();
            for (int i = collection.Count - 1; i >= 0; i--)
            {
                collection[i]?.HandleShutdown();
            }

            _initialized = false;
            _isShuttingDown = false;
        }

        /// <summary>Rebuilds the registry after internal features are changed at runtime.</summary>
        public void RefreshFeatures()
        {
            NpcFeatureCollection collection = EnsureFeatureCollection();

            _previousFeatures.Clear();
            for (int i = 0; i < collection.Count; i++)
            {
                NpcFeature previous = collection[i];
                if (previous != null)
                {
                    _previousFeatures.Add(previous);
                }
            }

            RefreshRegistryOnly();

            for (int i = 0; i < _previousFeatures.Count; i++)
            {
                NpcFeature previous = _previousFeatures[i];
                if (!_moduleBuffer.Contains(previous))
                {
                    previous.HandleShutdown();
                }
            }

            if (!_initialized || !Application.isPlaying)
            {
                return;
            }

            for (int i = 0; i < _moduleBuffer.Count; i++)
            {
                NpcFeature feature = _moduleBuffer[i];
                feature.HandleInitialize(this);
                feature.HandleOwnerAvailabilityChanged(_ownerAvailable);
            }
        }

        /// <summary>Adds a serializable feature module. No component is added to the GameObject.</summary>
        public NpcFeature AddFeature(Type featureType)
        {
            if (featureType == null)
            {
                throw new ArgumentNullException(nameof(featureType));
            }

            if (featureType.IsAbstract || !typeof(NpcFeature).IsAssignableFrom(featureType))
            {
                throw new ArgumentException($"{featureType.FullName} is not a concrete NPC feature type.", nameof(featureType));
            }

            NpcFeatureAttribute definition = (NpcFeatureAttribute)Attribute.GetCustomAttribute(
                featureType,
                typeof(NpcFeatureAttribute),
                false);
            if ((definition == null || !definition.AllowMultiple) && HasExactFeature(featureType))
            {
                throw new InvalidOperationException($"NPC already contains {featureType.Name}.");
            }

            NpcFeature feature = (NpcFeature)Activator.CreateInstance(featureType);
            _featureModules.Add(feature);
            feature.HandleAttach(this);
            _registryDirty = true;
            RefreshFeatures();
            return feature;
        }

        public TFeature AddFeature<TFeature>() where TFeature : NpcFeature, new()
        {
            return (TFeature)AddFeature(typeof(TFeature));
        }

        public bool RemoveFeature(NpcFeature feature)
        {
            if (feature == null || !_featureModules.Remove(feature))
            {
                return false;
            }

            _registryDirty = true;
            RefreshFeatures();
            return true;
        }

        public bool HasExactFeature(Type featureType)
        {
            if (featureType == null)
            {
                return false;
            }

            for (int i = 0; i < _featureModules.Count; i++)
            {
                NpcFeature feature = _featureModules[i];
                if (feature != null && feature.GetType() == featureType)
                {
                    return true;
                }
            }

            return false;
        }

        public void CollectValidationIssues(List<NpcValidationIssue> issues)
        {
            if (issues == null)
            {
                throw new ArgumentNullException(nameof(issues));
            }

            RefreshRegistryOnly();
            NpcFeatureCollection collection = EnsureFeatureCollection();

            for (int i = 0; i < collection.Count; i++)
            {
                NpcFeature feature = collection[i];
                if (feature == null)
                {
                    continue;
                }

                Type featureType = feature.GetType();
                object[] requirements = featureType.GetCustomAttributes(typeof(NpcRequiresFeatureAttribute), true);
                for (int requirementIndex = 0; requirementIndex < requirements.Length; requirementIndex++)
                {
                    NpcRequiresFeatureAttribute requirement = (NpcRequiresFeatureAttribute)requirements[requirementIndex];
                    if (!collection.Contains(requirement.CapabilityType))
                    {
                        issues.Add(new NpcValidationIssue(
                            NpcValidationSeverity.Error,
                            $"{featureType.Name} requires a feature implementing {requirement.CapabilityType.Name}.",
                            this,
                            requirement.CapabilityType));
                    }
                }

                object[] conflicts = featureType.GetCustomAttributes(typeof(NpcConflictsWithFeatureAttribute), true);
                for (int conflictIndex = 0; conflictIndex < conflicts.Length; conflictIndex++)
                {
                    NpcConflictsWithFeatureAttribute conflict = (NpcConflictsWithFeatureAttribute)conflicts[conflictIndex];
                    if (ContainsOtherMatch(collection, feature, conflict.CapabilityType))
                    {
                        issues.Add(new NpcValidationIssue(
                            NpcValidationSeverity.Error,
                            $"{featureType.Name} conflicts with {conflict.CapabilityType.Name}.",
                            this));
                    }
                }

                NpcFeatureAttribute definition = (NpcFeatureAttribute)Attribute.GetCustomAttribute(
                    featureType,
                    typeof(NpcFeatureAttribute),
                    false);
                if (definition != null && !definition.AllowMultiple && CountExactType(collection, featureType) > 1)
                {
                    issues.Add(new NpcValidationIssue(
                        NpcValidationSeverity.Error,
                        $"Only one {definition.DisplayName} feature is allowed per NPC.",
                        this));
                }

                feature.CollectValidationIssues(issues);
            }
        }

        internal void NotifyFeatureOperationalStateChanged()
        {
            EnsureFeatureCollection().InvalidateOperationalCache();
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            _registryDirty = true;
        }

        private NpcFeatureCollection EnsureFeatureCollection()
        {
            if (_features == null)
            {
                _features = new NpcFeatureCollection(this);
            }

            return _features;
        }

        private void EnsureRegistrySynchronized()
        {
            if (_registryDirty)
            {
                RefreshRegistryOnly();
            }
        }

        private void RefreshRegistryOnly()
        {
            _moduleBuffer.Clear();
            if (_featureModules == null)
            {
                _featureModules = new List<NpcFeature>(8);
            }

            for (int i = 0; i < _featureModules.Count; i++)
            {
                NpcFeature feature = _featureModules[i];
                if (feature == null || _moduleBuffer.Contains(feature))
                {
                    continue;
                }

                feature.HandleAttach(this);
                _moduleBuffer.Add(feature);
            }

            EnsureFeatureCollection().ReplaceWith(_moduleBuffer);
            _registryDirty = false;
        }

        private void SetOwnerAvailable(bool available)
        {
            if (_ownerAvailable == available)
            {
                return;
            }

            _ownerAvailable = available;
            NpcFeatureCollection collection = EnsureFeatureCollection();
            for (int i = 0; i < collection.Count; i++)
            {
                collection[i]?.HandleOwnerAvailabilityChanged(available);
            }
        }

        private static bool ContainsOtherMatch(
            NpcFeatureCollection collection,
            NpcFeature source,
            Type capabilityType)
        {
            for (int i = 0; i < collection.Count; i++)
            {
                NpcFeature candidate = collection[i];
                if (candidate != null && candidate != source && capabilityType.IsInstanceOfType(candidate))
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountExactType(NpcFeatureCollection collection, Type featureType)
        {
            int count = 0;
            for (int i = 0; i < collection.Count; i++)
            {
                NpcFeature feature = collection[i];
                if (feature != null && feature.GetType() == featureType)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
