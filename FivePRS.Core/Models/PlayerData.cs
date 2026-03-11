using System;

namespace FivePRS.Core.Models
{
    /// <summary>
    /// Authoritative player profile — mirrored from the database and cached server-side.
    /// A serialised copy is sent to the owning client on connect and on state changes.
    /// </summary>
    public class PlayerData
    {
        public string License     { get; set; } = string.Empty;
        public string Name        { get; set; } = string.Empty;
        public Department Department { get; set; } = Department.None;
        public bool   IsOnDuty   { get; set; } = false;
        public int    XP         { get; set; } = 0;
        public int    Rank       { get; set; } = 1;
        public DateTime LastSeen { get; set; } = DateTime.UtcNow;

        public int XPToNextRank => Rank * Rank * 100;

        public bool TryRankUp()
        {
            if (XP < XPToNextRank) return false;
            XP -= XPToNextRank;
            Rank++;
            return true;
        }
    }
}
