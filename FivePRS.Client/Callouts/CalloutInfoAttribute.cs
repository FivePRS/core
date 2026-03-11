using System;
using FivePRS.Core.Models;

namespace FivePRS.Client.Callouts
{
    /// <summary>
    /// Marks a <see cref="CalloutBase"/> subclass as a dispatchable callout.
    /// The <see cref="CalloutRegistry"/> scans loaded assemblies for this attribute —
    /// no manual registration in any central list is ever required.
    ///
    /// Usage:
    /// <code>
    /// [CalloutInfo("Shoplifting", Department.Police, CalloutPriority.Low,
    ///              weight: 20, cooldownSeconds: 300, xpReward: 75)]
    /// public class ShopliftingCallout : CalloutBase { ... }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class CalloutInfoAttribute : Attribute
    {
        public string Name { get; }

        public Department Department { get; }

        public CalloutPriority Priority { get; }

        public int Weight { get; }

        public int CooldownSeconds { get; }

        public int XPReward { get; }

        public CalloutInfoAttribute(
            string name,
            Department department,
            CalloutPriority priority,
            int weight          = 10,
            int cooldownSeconds = 600,
            int xpReward        = 75)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Callout name cannot be empty.", nameof(name));
            if (weight < 1)
                throw new ArgumentOutOfRangeException(nameof(weight), "Weight must be at least 1.");

            Name            = name;
            Department      = department;
            Priority        = priority;
            Weight          = weight;
            CooldownSeconds = cooldownSeconds;
            XPReward        = xpReward;
        }
    }
}
