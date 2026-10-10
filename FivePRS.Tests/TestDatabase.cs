using System;
using System.IO;
using System.Threading.Tasks;
using FivePRS.Server.Database;
using Microsoft.Data.Sqlite;
using MySqlConnector;

namespace FivePRS.Tests
{
    public sealed class TestDatabase : IDisposable
    {
        public const string Collection = "Database";

        private readonly string _path = Path.Combine(Path.GetTempPath(), $"fiveprs-test-{Guid.NewGuid():N}.db");

        public CivilianStore Store { get; private set; } = null!;

        public RosterStore Roster { get; private set; } = null!;

        public AppearanceStore Appearances { get; private set; } = null!;

        public PreferencesStore Preferences { get; private set; } = null!;

        public PositionStore Positions { get; private set; } = null!;

        public IDatabaseProvider Provider { get; private set; } = null!;

        public static async Task<TestDatabase> CreateAsync()
        {
            var database = new TestDatabase();
            var mySql = Environment.GetEnvironmentVariable("FIVEPRS_TEST_MYSQL");

            IDatabaseProvider provider;
            if (string.IsNullOrEmpty(mySql))
            {
                provider = new SQLiteProvider(database._path);
            }
            else
            {
                await DropTablesAsync(mySql);
                provider = new MySqlProvider(mySql);
            }

            await provider.InitializeAsync();
            await new Migrator(provider).MigrateAsync();
            database.Provider = provider;
            database.Store = new CivilianStore(provider);
            database.Roster = new RosterStore(provider);
            database.Appearances = new AppearanceStore(provider);
            database.Preferences = new PreferencesStore(provider);
            database.Positions = new PositionStore(provider);
            return database;
        }

        private static async Task DropTablesAsync(string connectionString)
        {
            using var conn = new MySqlConnection(connectionString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DROP TABLE IF EXISTS fiveprs_records, fiveprs_licenses, fiveprs_vehicles, fiveprs_characters, fiveprs_roster, fiveprs_appearances, fiveprs_preferences, fiveprs_positions, fiveprs_audit, fiveprs_schema, ers_players;";
            await cmd.ExecuteNonQueryAsync();
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
