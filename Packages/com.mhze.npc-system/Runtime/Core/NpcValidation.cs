using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModularNPC
{
    public enum NpcValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    /// <summary>A setup issue that can be shown by tooling or consumed by custom validators.</summary>
    public readonly struct NpcValidationIssue
    {
        public NpcValidationIssue(
            NpcValidationSeverity severity,
            string message,
            UnityEngine.Object context = null,
            Type suggestedFeatureType = null)
        {
            Severity = severity;
            Message = message ?? string.Empty;
            Context = context;
            SuggestedFeatureType = suggestedFeatureType;
        }

        public NpcValidationSeverity Severity { get; }

        public string Message { get; }

        public UnityEngine.Object Context { get; }

        public Type SuggestedFeatureType { get; }
    }

    public interface INpcValidatable
    {
        void CollectValidationIssues(List<NpcValidationIssue> issues);
    }
}
