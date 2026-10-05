namespace FivePRS.Core.Models
{
    public enum UnitStatus
    {
        Available = 0,
        EnRoute   = 1,
        OnScene   = 2,
        Busy      = 3
    }

    public class UnitInfo
    {
        public int        ServerId   { get; set; }
        public string     Name       { get; set; } = string.Empty;
        public string     Callsign   { get; set; } = string.Empty;
        public Department Department { get; set; }
        public string     Agency     { get; set; } = string.Empty;
        public string?    Territory  { get; set; }
        public int        Rank       { get; set; }
        public UnitStatus Status     { get; set; }
        public string?    CallId     { get; set; }
        public float      X          { get; set; }
        public float      Y          { get; set; }
        public float      Z          { get; set; }
    }
}
