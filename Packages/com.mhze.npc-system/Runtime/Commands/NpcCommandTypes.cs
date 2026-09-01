using System;

namespace ModularNPC
{
    public enum NpcCommandStatus
    {
        Invalid,
        Running,
        Succeeded,
        Failed,
        Cancelled,
        Interrupted,
        Expired
    }

    public enum NpcCommandInterruptPolicy
    {
        EqualOrHigherPriority,
        HigherPriorityOnly,
        Always,
        Never
    }

    public enum NpcCommandRejection
    {
        None,
        FeatureUnavailable,
        Busy,
        PriorityTooLow,
        ActiveCommandLocked,
        InvalidArgument
    }

    /// <summary>Control ownership and arbitration settings supplied by an external controller.</summary>
    public readonly struct NpcCommandRequest
    {
        public NpcCommandRequest(
            object owner,
            int priority = 0,
            NpcCommandInterruptPolicy interruptPolicy = NpcCommandInterruptPolicy.EqualOrHigherPriority)
        {
            Owner = owner;
            Priority = priority;
            InterruptPolicy = interruptPolicy;
        }

        public object Owner { get; }

        public int Priority { get; }

        /// <summary>Determines which future commands may interrupt this command.</summary>
        public NpcCommandInterruptPolicy InterruptPolicy { get; }

        public static NpcCommandRequest Default => new NpcCommandRequest(null);
    }

    internal interface INpcCommandSource
    {
        NpcCommandStatus GetCommandStatus(uint commandId);

        bool CancelCommand(uint commandId);
    }

    /// <summary>Lightweight token used to inspect or cancel a command without allocations.</summary>
    public readonly struct NpcCommandHandle : IEquatable<NpcCommandHandle>
    {
        private readonly INpcCommandSource _source;
        private readonly uint _commandId;

        internal NpcCommandHandle(INpcCommandSource source, uint commandId)
        {
            _source = source;
            _commandId = commandId;
        }

        public bool IsValid => _source != null && _commandId != 0;

        public bool IsRunning => Status == NpcCommandStatus.Running;

        public NpcCommandStatus Status => IsValid
            ? _source.GetCommandStatus(_commandId)
            : NpcCommandStatus.Invalid;

        public bool Cancel()
        {
            return IsValid && _source.CancelCommand(_commandId);
        }

        public bool Equals(NpcCommandHandle other)
        {
            return ReferenceEquals(_source, other._source) && _commandId == other._commandId;
        }

        public override bool Equals(object obj)
        {
            return obj is NpcCommandHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((_source != null ? _source.GetHashCode() : 0) * 397) ^ (int)_commandId;
            }
        }

        public static bool operator ==(NpcCommandHandle left, NpcCommandHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NpcCommandHandle left, NpcCommandHandle right)
        {
            return !left.Equals(right);
        }

        public static NpcCommandHandle Invalid => default;
    }

    public readonly struct NpcCommandStartResult
    {
        public NpcCommandStartResult(NpcCommandHandle handle, NpcCommandRejection rejection)
        {
            Handle = handle;
            Rejection = rejection;
        }

        public NpcCommandHandle Handle { get; }

        public NpcCommandRejection Rejection { get; }

        public bool Accepted => Rejection == NpcCommandRejection.None && Handle.IsValid;

        public static NpcCommandStartResult Rejected(NpcCommandRejection reason)
        {
            return new NpcCommandStartResult(NpcCommandHandle.Invalid, reason);
        }
    }
}
