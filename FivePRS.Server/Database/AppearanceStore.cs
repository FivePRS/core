using System;
using System.Threading.Tasks;

namespace FivePRS.Server.Database
{
    public sealed class AppearanceStore : SqlStore
    {
        private const string SqliteSchema = @"CREATE TABLE IF NOT EXISTS fiveprs_appearances (
                character_id INTEGER PRIMARY KEY,
                data         TEXT    NOT NULL,
                updated_at   TEXT    NOT NULL)";

        private const string MySqlSchema = @"CREATE TABLE IF NOT EXISTS `fiveprs_appearances` (
                `character_id` INT UNSIGNED NOT NULL,
                `data`         MEDIUMTEXT   NOT NULL,
                `updated_at`   DATETIME(6)  NOT NULL,
                PRIMARY KEY (`character_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci";

        public AppearanceStore(IDatabaseProvider db) : base(db)
        {
        }

        public Task InitializeAsync() => ExecuteAsync(IsMySql ? MySqlSchema : SqliteSchema);

        public async Task<string?> GetAsync(int characterId)
        {
            var rows = await QueryAsync("SELECT data FROM fiveprs_appearances WHERE character_id = @id",
                reader => reader.GetString(0), ("@id", characterId));
            return rows.Count > 0 ? rows[0] : null;
        }

        public Task SaveAsync(int characterId, string data) =>
            ExecuteAsync(
                "INSERT INTO fiveprs_appearances (character_id, data, updated_at) VALUES (@id, @data, @now) " +
                (IsMySql
                    ? "ON DUPLICATE KEY UPDATE data = VALUES(data), updated_at = VALUES(updated_at)"
                    : "ON CONFLICT(character_id) DO UPDATE SET data = excluded.data, updated_at = excluded.updated_at"),
                ("@id", characterId), ("@data", data), ("@now", DateTime.UtcNow));

        public Task DeleteAsync(int characterId) =>
            ExecuteAsync("DELETE FROM fiveprs_appearances WHERE character_id = @id", ("@id", characterId));
    }
}
