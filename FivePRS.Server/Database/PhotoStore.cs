using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FivePRS.Server.Database
{
    public sealed class PhotoStore : SqlStore
    {
        public PhotoStore(IDatabaseProvider db) : base(db)
        {
        }

        public async Task<string?> GetAsync(int characterId)
        {
            var rows = await QueryAsync("SELECT data FROM fiveprs_photos WHERE character_id = @id",
                row => row.GetString("data"), ("@id", characterId));
            return rows.Count > 0 ? rows[0] : null;
        }

        public async Task<Dictionary<int, string>> GetManyAsync(IEnumerable<int> characterIds)
        {
            var ids = characterIds.Distinct().ToList();
            var photos = new Dictionary<int, string>();
            if (ids.Count == 0) return photos;

            var parameters = ids.Select((id, index) => ($"@id{index}", (object?)id)).ToArray();
            var rows = await QueryAsync(
                $"SELECT character_id, data FROM fiveprs_photos WHERE character_id IN ({string.Join(", ", parameters.Select(p => p.Item1))})",
                row => (Id: row.GetInt32("character_id"), Data: row.GetString("data")),
                parameters);

            foreach (var row in rows)
                photos[row.Id] = row.Data;
            return photos;
        }

        public Task SetAsync(int characterId, string data) =>
            ExecuteAsync(
                "INSERT INTO fiveprs_photos (character_id, data, updated_at) VALUES (@id, @data, @now) " +
                (IsMySql
                    ? "ON DUPLICATE KEY UPDATE data = VALUES(data), updated_at = VALUES(updated_at)"
                    : "ON CONFLICT(character_id) DO UPDATE SET data = excluded.data, updated_at = excluded.updated_at"),
                ("@id", characterId), ("@data", data), ("@now", DateTime.UtcNow));

        public Task DeleteAsync(int characterId) =>
            ExecuteAsync("DELETE FROM fiveprs_photos WHERE character_id = @id", ("@id", characterId));
    }
}
