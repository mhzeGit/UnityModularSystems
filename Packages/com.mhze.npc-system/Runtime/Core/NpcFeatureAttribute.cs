using System;

namespace ModularNPC
{
    /// <summary>
    /// Supplies editor-facing metadata for an NPC feature. Custom features do not need
    /// this attribute, but adding it makes them discoverable in the categorized feature menu.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class NpcFeatureAttribute : Attribute
    {
        public NpcFeatureAttribute(string displayName, string category = "Custom")
        {
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Unnamed Feature" : displayName;
            Category = string.IsNullOrWhiteSpace(category) ? "Custom" : category;
        }

        public string DisplayName { get; }

        public string Category { get; }

        public string Description { get; set; } = string.Empty;

        public int Order { get; set; }

        public bool AllowMultiple { get; set; }
    }

    /// <summary>Declares a required feature class or capability interface.</summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public sealed class NpcRequiresFeatureAttribute : Attribute
    {
        public NpcRequiresFeatureAttribute(Type capabilityType)
        {
            CapabilityType = capabilityType ?? throw new ArgumentNullException(nameof(capabilityType));
        }

        public Type CapabilityType { get; }
    }

    /// <summary>Declares a feature class or capability interface that cannot coexist with this feature.</summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public sealed class NpcConflictsWithFeatureAttribute : Attribute
    {
        public NpcConflictsWithFeatureAttribute(Type capabilityType)
        {
            CapabilityType = capabilityType ?? throw new ArgumentNullException(nameof(capabilityType));
        }

        public Type CapabilityType { get; }
    }
}
