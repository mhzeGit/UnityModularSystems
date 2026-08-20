using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModularNPC
{
    /// <summary>
    /// Serializable, non-component NPC capability. Features live as managed references inside
    /// one Npc component and receive lifecycle/ticking through the root coordinator.
    /// </summary>
    [Serializable]
    public abstract class NpcFeature : INpcValidatable
    {
        [SerializeField, HideInInspector] private bool _enabled = true;

        [NonSerialized] private Npc _npc;
        [NonSerialized] private bool _initialized;
        [NonSerialized] private bool _ownerAvailable;
        [NonSerialized] private bool _operational;
        [NonSerialized] private bool _tickRequested;

        public Npc Npc => _npc;

        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value)
                {
                    return;
                }

                _enabled = value;
                RefreshOperationalState();
            }
        }

        public bool IsInitialized => _initialized;

        public bool IsOperational => _operational;

        protected Transform Transform => _npc != null ? _npc.transform : null;

        protected GameObject GameObject => _npc != null ? _npc.gameObject : null;

        public virtual void CollectValidationIssues(List<NpcValidationIssue> issues)
        {
        }

        protected virtual void OnFeatureInitialized()
        {
        }

        protected virtual void OnFeatureShutdown()
        {
        }

        protected virtual void OnFeatureActivated()
        {
        }

        protected virtual void OnFeatureDeactivated()
        {
        }

        protected virtual void OnFeatureValidate()
        {
        }

        protected bool TryGetFeature<TCapability>(out TCapability capability) where TCapability : class
        {
            capability = null;
            return _npc != null && _npc.Features.TryGet(out capability);
        }

        protected TCapability GetRequiredFeature<TCapability>() where TCapability : class
        {
            if (_npc == null)
            {
                throw new InvalidOperationException($"{GetType().Name} is not attached to an NPC.");
            }

            return _npc.Features.Get<TCapability>();
        }

        protected TComponent GetComponent<TComponent>() where TComponent : Component
        {
            return _npc != null ? _npc.GetComponent<TComponent>() : null;
        }

        /// <summary>Activates or deactivates centralized ticking for this feature.</summary>
        protected void SetTicking(bool active)
        {
            _tickRequested = active;
            INpcTickable tickable = this as INpcTickable;
            if (tickable == null)
            {
                if (active)
                {
                    throw new InvalidOperationException(
                        $"{GetType().Name} requested ticking but does not implement {nameof(INpcTickable)}.");
                }

                return;
            }

            NpcScheduler.SetActive(tickable, active && _operational);
        }

        /// <summary>Re-reads TickSettings after a feature changes phase or interval.</summary>
        protected void RefreshTickSchedule()
        {
            if (_tickRequested && _operational && this is INpcTickable tickable)
            {
                NpcScheduler.Refresh(tickable);
            }
        }

        internal void HandleAttach(Npc owner)
        {
            if (_npc == owner)
            {
                return;
            }

            if (_initialized)
            {
                HandleShutdown();
            }

            _npc = owner;
        }

        internal void HandleInitialize(Npc owner)
        {
            HandleAttach(owner);
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            OnFeatureInitialized();
            RefreshOperationalState();
        }

        internal void HandleShutdown()
        {
            if (!_initialized)
            {
                return;
            }

            _ownerAvailable = false;
            RefreshOperationalState();
            OnFeatureShutdown();
            _initialized = false;
            _tickRequested = false;
            NpcScheduler.Release(this as INpcTickable);
        }

        internal void HandleOwnerAvailabilityChanged(bool available)
        {
            _ownerAvailable = available;
            RefreshOperationalState();
        }

        internal void HandleValidate(Npc owner)
        {
            HandleAttach(owner);
            OnFeatureValidate();
        }

        private void RefreshOperationalState()
        {
            bool shouldBeOperational = _initialized && _ownerAvailable && _enabled;
            if (_operational == shouldBeOperational)
            {
                return;
            }

            _operational = shouldBeOperational;
            if (_operational)
            {
                OnFeatureActivated();
                if (_tickRequested && this is INpcTickable tickable)
                {
                    NpcScheduler.SetActive(tickable, true);
                }
            }
            else
            {
                OnFeatureDeactivated();
                if (this is INpcTickable tickable)
                {
                    NpcScheduler.SetActive(tickable, false);
                }
            }

            if (_npc != null)
            {
                _npc.NotifyFeatureOperationalStateChanged();
            }
        }
    }
}
