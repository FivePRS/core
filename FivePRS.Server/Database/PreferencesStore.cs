using System;
using System.Threading.Tasks;

namespace FivePRS.Server.Database
{
    public sealed class PreferencesStore : SqlStore
    {
        private const string SqliteSchema = @"CREATE TABLE IF NOT EXISTS fiveprs_preferences (
                license    TEXT PRIMARY KEY,
                wallpaper  TEXT NOT NULL,
                updated_at TEXT NOT NULL)";

        private const string MySqlSchema = @"CREATE TABLE IF NOT EXISTS `fiveprs_preferences` (
                `license`    VARCHAR(60)  NOT NULL,
                `wallpaper`  VARCHAR(600) NOT NULL,
                `updated_at` DATETIME(6)  NOT NULL,
                PRIMARY KEY (`license`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci";

        public PreferencesStore(IDatabaseProvider db) : base(db)
        {
        }

        public Task InitializeAsync() => ExecuteAsync(IsMySql ? MySqlSchema : SqliteSchema);

        public async Task<string> GetWallpaperAsync(string license)
        {
            var rows = await QueryAsync("SELECT wallpaper FROM fiveprs_preferences WHERE license = @license",
                reader => reader.GetString(0), ("@license", license));
            return rows.Count > 0 ? rows[0] : string.Empty;
        }

        public Task SetWallpaperAsync(string license, string wallpaper) =>
            ExecuteAsync(
                "INSERT INTO fiveprs_preferences (license, wallpaper, updated_at) VALUES (@license, @wallpaper, @now) " +
                (IsMySql
                    ? "ON DUPLICATE KEY UPDATE wallpaper = VALUES(wallpaper), updated_at = VALUES(updated_at)"
                    : "ON CONFLICT(license) DO UPDATE SET wallpaper = excluded.wallpaper, updated_at = excluded.updated_at"),
                ("@license", license), ("@wallpaper", wallpaper), ("@now", DateTime.UtcNow));
    }
}
