using System;
using FivePRS.Core.Models;

namespace FivePRS.Client.Callouts
{
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
