using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FivePRS.Core.Models;
using FivePRS.Server.Database;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FivePRS.Tests
{
    [Collection(TestDatabase.Collection)]
    public sealed class MigratorTests : IAsyncLifetime
    {
        private TestDatabase _database = null!;

        public async Task InitializeAsync() => _database = await TestDatabase.CreateAsync();

        public Task DisposeAsync()
        {
            _database.Dispose();
            return Task.CompletedTask;
        }

        [Fact]
        public async Task Migrate_FreshDatabase_ReachesLatestVersion()
        {
            var migrator = new Migrator(_database.Provider);

            Assert.Equal(migrator.LatestVersion, await migrator.CurrentVersionAsync());
        }

        [Fact]
        public async Task Migrate_UpToDate_AppliesNothing()
        {
            var result = await new Migrator(_database.Provider).MigrateAsync();

            Assert.Empty(result.Applied);
            Assert.Equal(result.LatestVersion, result.ToVersion);
            Assert.False(result.DatabaseIsNewer);
        }

        [Fact]
        public async Task Migrate_NewMigration_AppliesOnlyThatOneAndCallsBackupFirst()
        {
            var next = new Migrator(_database.Provider).LatestVersion + 1;
            var migrations = Migrations.All.Concat(new[]
            {
                new Migration(next, "Add note", new AddColumnStep("fiveprs_positions", "note", "TEXT NOT NULL DEFAULT ''", "VARCHAR(40) NOT NULL DEFAULT ''")),
            }).ToList();

            int? backedUpFrom = null;
            var result = await new Migrator(_database.Provider, migrations).MigrateAsync(version =>
            {
                backedUpFrom = version;
                return Task.CompletedTask;
            });

            Assert.Equal(next, Assert.Single(result.Applied).Version);
            Assert.Equal(next - 1, backedUpFrom);
            Assert.Equal(next, result.ToVersion);

            await _database.Positions.SaveAsync("license:a", new SavedPosition { X = 1, Y = 2, Z = 3, Heading = 90 });
            Assert.NotNull(await _database.Positions.GetAsync("license:a"));
        }

        [Fact]
        public async Task Migrate_AddColumnThatAlreadyExists_Succeeds()
        {
            var next = new Migrator(_database.Provider).LatestVersion + 1;
            var migrations = Migrations.All.Concat(new[]
            {
                new Migration(next, "Add callsign again", new AddColumnStep("ers_players", "callsign", "TEXT NOT NULL DEFAULT ''", "VARCHAR(16) NOT NULL DEFAULT ''")),
            }).ToList();

            var result = await new Migrator(_database.Provider, migrations).MigrateAsync();

            Assert.Single(result.Applied);
        }

        [Fact]
        public async Task Migrate_DatabaseNewerThanBuild_ReportsItAndAppliesNothing()
        {
            var ahead = new Migrator(_database.Provider).LatestVersion + 5;
            await new Migrator(_database.Provider, Migrations.All.Concat(new[] { new Migration(ahead, "Future") }).ToList()).MigrateAsync();

            var result = await new Migrator(_database.Provider).MigrateAsync();

            Assert.True(result.DatabaseIsNewer);
            Assert.Empty(result.Applied);
            Assert.Equal(ahead, result.FromVersion);
        }

        [Fact]
        public void Constructor_VersionsOutOfOrder_Throws()
        {
            var migrations = new[] { new Migration(2, "Second"), new Migration(1, "First") };

            Assert.Throws<ArgumentException>(() => new Migrator(_database.Provider, migrations));
        }

        [Fact]
        public void All_VersionsStartAtOneAndIncrease()
        {
            Assert.Equal(1, Migrations.All[0].Version);
            Assert.Equal(Migrations.All.Select(m => m.Version).OrderBy(v => v), Migrations.All.Select(m => m.Version));
            Assert.Equal(Migrations.All.Count, Migrations.All.Select(m => m.Version).Distinct().Count());
        }
    }

    public sealed class SQLiteMigrationTests : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"fiveprs-test-{Guid.NewGuid():N}.db");
        private readonly string _backup = Path.Combine(Path.GetTempPath(), $"fiveprs-backup-{Guid.NewGuid():N}.db");

        [Fact]
        public async Task Migrate_FailingMigration_RollsBackAndKeepsVersion()
        {
            var provider = new SQLiteProvider(_path);
            await provider.InitializeAsync();
            await new Migrator(provider).MigrateAsync();

            var next = Migrations.All[Migrations.All.Count - 1].Version + 1;
            var broken = Migrations.All.Concat(new[]
            {
                new Migration(next, "Broken",
                    new SqlStep("CREATE TABLE fiveprs_half (id INTEGER)", "CREATE TABLE fiveprs_half (id INT)"),
                    new SqlStep("NOT VALID SQL", "NOT VALID SQL")),
            }).ToList();

            await Assert.ThrowsAnyAsync<Exception>(() => new Migrator(provider, broken).MigrateAsync());

            Assert.Equal(next - 1, await new Migrator(provider).CurrentVersionAsync());
            using var conn = new SqliteConnection($"Data Source={_path}");
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name = 'fiveprs_half'";
            Assert.Equal(0L, (long)(await cmd.ExecuteScalarAsync())!);
        }

        [Fact]
        public async Task Backup_CopiesDatabaseWithData()
        {
            var provider = new SQLiteProvider(_path);
            await provider.InitializeAsync();
            await new Migrator(provider).MigrateAsync();
            await provider.SavePlayerAsync(new PlayerData { License = "license:kept", Name = "Kept" });

            await provider.BackupAsync(_backup);

            var copy = new SQLiteProvider(_backup);
            Assert.Equal("Kept", (await copy.GetPlayerAsync("license:kept"))!.Name);
            Assert.Equal(Migrations.All[Migrations.All.Count - 1].Version, await new Migrator(copy).CurrentVersionAsync());
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            foreach (var file in new[] { _path, _path + "-wal", _path + "-shm", _backup })
            {
                if (File.Exists(file)) File.Delete(file);
            }
        }
    }
}
