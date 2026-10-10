using System;
using System.Threading.Tasks;
using FivePRS.Core.Models;

namespace FivePRS.Server.Database
{
    public sealed class PositionStore : SqlStore
    {
        public PositionStore(IDatabaseProvider db) : base(db)
        {
        }

        public async Task<SavedPosition?> GetAsync(string license)
        {
            var rows = await QueryAsync("SELECT x, y, z, heading FROM fiveprs_positions WHERE license = @license",
                reader => new SavedPosition
                {
                    X       = Convert.ToSingle(reader.GetValue(0)),
                    Y       = Convert.ToSingle(reader.GetValue(1)),
                    Z       = Convert.ToSingle(reader.GetValue(2)),
                    Heading = Convert.ToSingle(reader.GetValue(3)),
                },
                ("@license", license));
            return rows.Count > 0 ? rows[0] : null;
        }

        public Task SaveAsync(string license, SavedPosition position) =>
            ExecuteAsync(
                "INSERT INTO fiveprs_positions (license, x, y, z, heading, updated_at) VALUES (@license, @x, @y, @z, @heading, @now) " +
                (IsMySql
                    ? "ON DUPLICATE KEY UPDATE x = VALUES(x), y = VALUES(y), z = VALUES(z), heading = VALUES(heading), updated_at = VALUES(updated_at)"
                    : "ON CONFLICT(license) DO UPDATE SET x = excluded.x, y = excluded.y, z = excluded.z, heading = excluded.heading, updated_at = excluded.updated_at"),
                ("@license", license), ("@x", (double)position.X), ("@y", (double)position.Y), ("@z", (double)position.Z),
                ("@heading", (double)position.Heading), ("@now", DateTime.UtcNow));
    }
}
