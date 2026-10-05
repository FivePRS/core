using System.Collections.Generic;

namespace FivePRS.Core.Models
{
    public enum CalloutResult
    {
        Completed = 0,
        Failed    = 1,
        Declined  = 2
    }

    public enum OfferResponse
    {
        Accepted    = 0,
        Declined    = 1,
        Unavailable = 2
    }

    public class CallInfo
    {
        public string          Id          { get; set; } = string.Empty;
        public string          Name        { get; set; } = string.Empty;
        public Department      Department  { get; set; }
        public CalloutPriority Priority    { get; set; }
        public string?         Territory   { get; set; }
        public int             PrimaryUnit { get; set; }
        public List<int>       Units       { get; set; } = new();
        public float           X           { get; set; }
        public float           Y           { get; set; }
        public float           Z           { get; set; }
    }

    public class DispatchSnapshot
    {
        public List<UnitInfo> Units { get; set; } = new();
        public List<CallInfo> Calls { get; set; } = new();
    }

    public class CalloutDefinition
    {
        public string          Name            { get; set; } = string.Empty;
        public Department      Department      { get; set; }
        public CalloutPriority Priority        { get; set; }
        public int             Weight          { get; set; } = 10;
        public int             CooldownSeconds { get; set; }
        public int             XPReward        { get; set; }
    }
}
