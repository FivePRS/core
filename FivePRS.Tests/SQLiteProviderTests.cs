using System;
using System.IO;
using System.Threading.Tasks;
using FivePRS.Core.Models;
using FivePRS.Server.Database;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FivePRS.Tests
{
    public sealed class SQLiteProviderTests : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"fiveprs-test-{Guid.NewGuid():N}.db");

        [Fact]
        public async Task Migrate_ExistingDatabaseWithoutCallsign_AddsColumnAndKeepsPlayers()
        {
            using (var conn = new SqliteConnection($"Data Source={_path}"))
            {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE ers_players (
                        license TEXT NOT NULL PRIMARY KEY, name TEXT NOT NULL, department INTEGER DEFAULT 0,
                        is_on_duty INTEGER DEFAULT 0, xp INTEGER DEFAULT 0, rank_level INTEGER DEFAULT 1,
                        last_seen TEXT DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ', 'now')));
                    INSERT INTO ers_players (license, name, department, xp, rank_level) VALUES ('license:old', 'Veteran', 1, 40, 3);";
                await cmd.ExecuteNonQueryAsync();
            }

            var provider = new SQLiteProvider(_path);
            await provider.InitializeAsync();
            await new Migrator(provider).MigrateAsync();

            var player = await provider.GetPlayerAsync("license:old");
            Assert.NotNull(player);
            Assert.Equal("Veteran", player!.Name);
            Assert.Equal(3, player.Rank);
            Assert.Equal(string.Empty, player.Callsign);
        }

        [Fact]
        public async Task SavePlayerAsync_Callsign_RoundTrips()
        {
            var provider = new SQLiteProvider(_path);
            await provider.InitializeAsync();
            await new Migrator(provider).MigrateAsync();

            await provider.SavePlayerAsync(new PlayerData
            {
                License    = "license:new",
                Name       = "Rookie",
                Department = Department.Police,
                Agency     = "lspd",
                Callsign   = "1-ADAM-12",
            });

            var player = await provider.GetPlayerAsync("license:new");
            Assert.Equal("1-ADAM-12", player!.Callsign);
            Assert.Equal("lspd", player.Agency);
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            foreach (var file in new[] { _path, _path + "-wal", _path + "-shm" })
            {
                if (File.Exists(file)) File.Delete(file);
            }
        }
    }
}
