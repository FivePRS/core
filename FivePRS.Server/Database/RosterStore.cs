using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FivePRS.Core.Models;

namespace FivePRS.Server.Database
{
    public sealed class RosterStore : SqlStore
    {
        public RosterStore(IDatabaseProvider db) : base(db)
        {
        }

        public Task<List<(string License, Department Department)>> GetAllAsync() =>
            QueryAsync("SELECT license, department FROM fiveprs_roster",
                row => (row.GetString("license"), (Department)row.GetInt32("department")));

        public Task GrantAsync(string license, Department department, string grantedBy) =>
            ExecuteAsync(
                $"INSERT {(IsMySql ? "IGNORE" : "OR IGNORE")} INTO fiveprs_roster (license, department, granted_by, granted_at) " +
                "VALUES (@license, @department, @by, @now)",
                ("@license", license), ("@department", (int)department), ("@by", grantedBy), ("@now", DateTime.UtcNow));

        public Task RevokeAsync(string license, Department department) =>
            ExecuteAsync("DELETE FROM fiveprs_roster WHERE license = @license AND department = @department",
                ("@license", license), ("@department", (int)department));

        public Task<List<RosterPlayer>> SearchPlayersAsync(string term, int limit) =>
            QueryAsync(
                $"SELECT license, name, department, rank_level, last_seen FROM ers_players WHERE name LIKE @term ORDER BY last_seen DESC LIMIT {limit}",
                row => new RosterPlayer
                {
                    License    = row.GetString("license"),
                    Name       = row.GetString("name"),
                    Department = (Department)row.GetInt32("department"),
                    Rank       = row.GetInt32("rank_level"),
                    LastSeen   = row.IsNull("last_seen") ? DateTime.MinValue : row.GetDateTime("last_seen"),
                },
                ("@term", $"%{term}%"));
    }
}
