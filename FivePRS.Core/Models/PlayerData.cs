using System;

namespace FivePRS.Core.Models
{
    public class PlayerData
    {
        public string     License    { get; set; } = string.Empty;
        public string     Name       { get; set; } = string.Empty;
        public Department Department { get; set; } = Department.None;
        public string     Agency     { get; set; } = string.Empty;
        public string     Callsign   { get; set; } = string.Empty;
        public bool       IsOnDuty   { get; set; }
        public int        XP         { get; set; }
        public int        Rank       { get; set; } = 1;
        public DateTime   LastSeen   { get; set; } = DateTime.UtcNow;

        public int XPToNextRank => Rank * Rank * 100;

        public bool AddXP(int amount)
        {
            XP += Math.Max(0, amount);

            var rankedUp = false;
            while (XP >= XPToNextRank)
            {
                XP -= XPToNextRank;
                Rank++;
                rankedUp = true;
            }
            return rankedUp;
        }
    }
}
