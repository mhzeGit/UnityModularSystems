using System;

namespace ModularNPC
{
    /// <summary>
    /// Base for features that own one externally-issued command at a time. It provides
    /// priority arbitration, interruption, status polling, and deterministic cancellation.
    /// </summary>
    [Serializable]
    public abstract class NpcCommandFeature : NpcFeature, INpcCommandSource
    {
        private uint _nextCommandId;
        private uint _activeCommandId;
        private NpcCommandRequest _activeRequest;
        private uint _lastCompletedCommandId;
        private NpcCommandStatus _lastCompletedStatus;

        public bool HasActiveCommand => _activeCommandId != 0;

        public NpcCommandHandle ActiveCommand => _activeCommandId == 0
            ? NpcCommandHandle.Invalid
            : new NpcCommandHandle(this, _activeCommandId);

        protected NpcCommandRequest ActiveCommandRequest => _activeRequest;

        protected NpcCommandStartResult BeginCommand(in NpcCommandRequest request)
        {
            if (!IsOperational)
            {
                return NpcCommandStartResult.Rejected(NpcCommandRejection.FeatureUnavailable);
            }

            if (_activeCommandId != 0)
            {
                NpcCommandRejection rejection = GetInterruptionRejection(request);
                if (rejection != NpcCommandRejection.None)
                {
                    return NpcCommandStartResult.Rejected(rejection);
                }

                FinishActiveCommand(NpcCommandStatus.Interrupted);
            }

            unchecked
            {
                _nextCommandId++;
                if (_nextCommandId == 0)
                {
                    _nextCommandId = 1;
                }
            }

            _activeCommandId = _nextCommandId;
            _activeRequest = request;
            NpcCommandHandle handle = new NpcCommandHandle(this, _activeCommandId);
            OnCommandStarted(handle);
            return new NpcCommandStartResult(handle, NpcCommandRejection.None);
        }

        protected bool SucceedActiveCommand()
        {
            return FinishActiveCommand(NpcCommandStatus.Succeeded);
        }

        protected bool FailActiveCommand()
        {
            return FinishActiveCommand(NpcCommandStatus.Failed);
        }

        protected bool InterruptActiveCommand()
        {
            return FinishActiveCommand(NpcCommandStatus.Interrupted);
        }

        protected bool CancelActiveCommand()
        {
            return FinishActiveCommand(NpcCommandStatus.Cancelled);
        }

        protected bool IsCurrentCommand(NpcCommandHandle handle)
        {
            return handle.IsValid && handle == ActiveCommand;
        }

        protected virtual void OnCommandStarted(NpcCommandHandle handle)
        {
        }

        protected virtual void OnCommandFinished(NpcCommandHandle handle, NpcCommandStatus status)
        {
        }

        protected sealed override void OnFeatureDeactivated()
        {
            CancelActiveCommand();
            SetTicking(false);
            OnCommandFeatureDeactivated();
        }

        protected sealed override void OnFeatureShutdown()
        {
            CancelActiveCommand();
            SetTicking(false);
            OnCommandFeatureShutdown();
        }

        protected virtual void OnCommandFeatureDeactivated()
        {
        }

        protected virtual void OnCommandFeatureShutdown()
        {
        }

        NpcCommandStatus INpcCommandSource.GetCommandStatus(uint commandId)
        {
            if (commandId == 0)
            {
                return NpcCommandStatus.Invalid;
            }

            if (commandId == _activeCommandId)
            {
                return NpcCommandStatus.Running;
            }

            if (commandId == _lastCompletedCommandId)
            {
                return _lastCompletedStatus;
            }

            return commandId <= _nextCommandId
                ? NpcCommandStatus.Expired
                : NpcCommandStatus.Invalid;
        }

        bool INpcCommandSource.CancelCommand(uint commandId)
        {
            return commandId != 0 && commandId == _activeCommandId && CancelActiveCommand();
        }

        private bool FinishActiveCommand(NpcCommandStatus status)
        {
            if (_activeCommandId == 0)
            {
                return false;
            }

            NpcCommandHandle handle = new NpcCommandHandle(this, _activeCommandId);
            _lastCompletedCommandId = _activeCommandId;
            _lastCompletedStatus = status;
            _activeCommandId = 0;
            _activeRequest = default;
            OnCommandFinished(handle, status);
            return true;
        }

        private NpcCommandRejection GetInterruptionRejection(in NpcCommandRequest incoming)
        {
            if (_activeRequest.Owner != null && ReferenceEquals(_activeRequest.Owner, incoming.Owner))
            {
                return NpcCommandRejection.None;
            }

            switch (_activeRequest.InterruptPolicy)
            {
                case NpcCommandInterruptPolicy.Always:
                    return NpcCommandRejection.None;

                case NpcCommandInterruptPolicy.Never:
                    return NpcCommandRejection.ActiveCommandLocked;

                case NpcCommandInterruptPolicy.HigherPriorityOnly:
                    return incoming.Priority > _activeRequest.Priority
                        ? NpcCommandRejection.None
                        : NpcCommandRejection.PriorityTooLow;

                default:
                    return incoming.Priority >= _activeRequest.Priority
                        ? NpcCommandRejection.None
                        : NpcCommandRejection.PriorityTooLow;
            }
        }
    }
}
