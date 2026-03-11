using System.Collections.Generic;

namespace FivePRS.Core.Models
{
    public enum CalloutPriority
    {
        Low      = 1,
        Medium   = 2,
        High     = 3,
        Critical = 4
    }

    /// <summary>
    /// Describes a dispatched callout. Produced server-side and sent to the relevant
    /// client(s) as a JSON blob so that the appropriate agency module can react.
    /// </summary>
    public class CalloutData
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;
        public CalloutPriority Priority { get; set; } = CalloutPriority.Medium;
        public Department RequiredDepartment { get; set; } = Department.Police;

        public float LocationX { get; set; }
        public float LocationY { get; set; }
        public float LocationZ { get; set; }

        public Dictionary<string, string> Metadata { get; set; } = new();

        public int XPReward { get; set; } = 50;
    }
}
