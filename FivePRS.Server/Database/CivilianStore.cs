using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using FivePRS.Core.Civilian;

namespace FivePRS.Server.Database
{
    public sealed class CivilianStore
    {
        private static readonly string[] SqliteSchema =
        {
            @"CREATE TABLE IF NOT EXISTS fiveprs_characters (
                id            INTEGER PRIMARY KEY AUTOINCREMENT,
                owner_license TEXT    NOT NULL,
                first_name    TEXT    NOT NULL,
                last_name     TEXT    NOT NULL,
                date_of_birth TEXT    NOT NULL,
                gender        TEXT    NOT NULL,
                created_at    TEXT    NOT NULL,
                last_used_at  TEXT    NOT NULL)",
            "CREATE INDEX IF NOT EXISTS idx_fiveprs_characters_owner ON fiveprs_characters(owner_license)",
            "CREATE INDEX IF NOT EXISTS idx_fiveprs_characters_name ON fiveprs_characters(last_name, first_name)",
            @"CREATE TABLE IF NOT EXISTS fiveprs_licenses (
                character_id INTEGER NOT NULL,
                type         TEXT    NOT NULL,
                status       INTEGER NOT NULL DEFAULT 0,
                issued_at    TEXT    NOT NULL,
                PRIMARY KEY (character_id, type))",
            @"CREATE TABLE IF NOT EXISTS fiveprs_vehicles (
                id            INTEGER PRIMARY KEY AUTOINCREMENT,
                character_id  INTEGER NOT NULL,
                plate         TEXT    NOT NULL UNIQUE,
                model         TEXT    NOT NULL,
                status        INTEGER NOT NULL DEFAULT 0,
                registered_at TEXT    NOT NULL)",
            "CREATE INDEX IF NOT EXISTS idx_fiveprs_vehicles_character ON fiveprs_vehicles(character_id)",
            @"CREATE TABLE IF NOT EXISTS fiveprs_records (
                id               INTEGER PRIMARY KEY AUTOINCREMENT,
                character_id     INTEGER NOT NULL,
                type             INTEGER NOT NULL,
                description      TEXT    NOT NULL,
                fine             INTEGER NOT NULL DEFAULT 0,
                officer_license  TEXT    NOT NULL,
                officer_name     TEXT    NOT NULL,
                officer_callsign TEXT    NOT NULL,
                created_at       TEXT    NOT NULL,
                active           INTEGER NOT NULL DEFAULT 0,
                resolution       TEXT    NULL)",
            "CREATE INDEX IF NOT EXISTS idx_fiveprs_records_character ON fiveprs_records(character_id)",
        };

        private static readonly string[] MySqlSchema =
        {
            @"CREATE TABLE IF NOT EXISTS `fiveprs_characters` (
                `id`            INT UNSIGNED NOT NULL AUTO_INCREMENT,
                `owner_license` VARCHAR(60)  NOT NULL,
                `first_name`    VARCHAR(32)  NOT NULL,
                `last_name`     VARCHAR(32)  NOT NULL,
                `date_of_birth` CHAR(10)     NOT NULL,
                `gender`        VARCHAR(16)  NOT NULL,
                `created_at`    DATETIME(6)  NOT NULL,
                `last_used_at`  DATETIME(6)  NOT NULL,
                PRIMARY KEY (`id`),
                KEY `idx_fiveprs_characters_owner` (`owner_license`),
                KEY `idx_fiveprs_characters_name` (`last_name`, `first_name`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci",
            @"CREATE TABLE IF NOT EXISTS `fiveprs_licenses` (
                `character_id` INT UNSIGNED NOT NULL,
                `type`         VARCHAR(32)  NOT NULL,
                `status`       TINYINT UNSIGNED NOT NULL DEFAULT 0,
                `issued_at`    DATETIME(6)  NOT NULL,
                PRIMARY KEY (`character_id`, `type`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci",
            @"CREATE TABLE IF NOT EXISTS `fiveprs_vehicles` (
                `id`            INT UNSIGNED NOT NULL AUTO_INCREMENT,
                `character_id`  INT UNSIGNED NOT NULL,
                `plate`         VARCHAR(8)   NOT NULL,
                `model`         VARCHAR(40)  NOT NULL,
                `status`        TINYINT UNSIGNED NOT NULL DEFAULT 0,
                `registered_at` DATETIME(6)  NOT NULL,
                PRIMARY KEY (`id`),
                UNIQUE KEY `uq_fiveprs_vehicles_plate` (`plate`),
                KEY `idx_fiveprs_vehicles_character` (`character_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci",
            @"CREATE TABLE IF NOT EXISTS `fiveprs_records` (
                `id`               INT UNSIGNED NOT NULL AUTO_INCREMENT,
                `character_id`     INT UNSIGNED NOT NULL,
                `type`             TINYINT UNSIGNED NOT NULL,
                `description`      VARCHAR(500) NOT NULL,
                `fine`             INT UNSIGNED NOT NULL DEFAULT 0,
                `officer_license`  VARCHAR(60)  NOT NULL,
                `officer_name`     VARCHAR(100) NOT NULL,
                `officer_callsign` VARCHAR(16)  NOT NULL,
                `created_at`       DATETIME(6)  NOT NULL,
                `active`           TINYINT(1)   NOT NULL DEFAULT 0,
                `resolution`       VARCHAR(120) NULL,
                PRIMARY KEY (`id`),
                KEY `idx_fiveprs_records_character` (`character_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci",
        };

        private const string CharacterColumns =
            "id, owner_license, first_name, last_name, date_of_birth, gender, created_at, last_used_at";

        private const string VehicleColumns = "id, character_id, plate, model, status, registered_at";

        private const string RecordColumns =
            "id, character_id, type, description, fine, officer_name, officer_callsign, created_at, active, resolution";

        private const string ActiveWarrantExists =
            "EXISTS (SELECT 1 FROM fiveprs_records r WHERE r.character_id = c.id AND r.type = 3 AND r.active = 1)";

        private readonly IDatabaseProvider _db;

        public CivilianStore(IDatabaseProvider db) => _db = db;

        private string LastInsertId => _db.Dialect == SqlDialect.MySql ? "SELECT LAST_INSERT_ID()" : "SELECT last_insert_rowid()";

        public async Task InitializeAsync()
        {
            foreach (var statement in _db.Dialect == SqlDialect.MySql ? MySqlSchema : SqliteSchema)
                await ExecuteAsync(statement);
        }

        public Task<List<CharacterInfo>> GetCharactersAsync(string ownerLicense) =>
            QueryAsync($"SELECT {CharacterColumns} FROM fiveprs_characters WHERE owner_license = @owner ORDER BY created_at, id",
                ReadCharacter, ("@owner", ownerLicense));

        public async Task<CharacterInfo?> GetCharacterAsync(int id)
        {
            var rows = await QueryAsync($"SELECT {CharacterColumns} FROM fiveprs_characters WHERE id = @id",
                ReadCharacter, ("@id", id));
            return rows.Count > 0 ? rows[0] : null;
        }

        public async Task<int> CreateCharacterAsync(CharacterInfo character)
        {
            var now = DateTime.UtcNow;
            var id = await ScalarAsync(
                "INSERT INTO fiveprs_characters (owner_license, first_name, last_name, date_of_birth, gender, created_at, last_used_at) " +
                $"VALUES (@owner, @first, @last, @dob, @gender, @now, @now); {LastInsertId};",
                ("@owner", character.OwnerLicense), ("@first", character.FirstName), ("@last", character.LastName),
                ("@dob", character.DateOfBirth), ("@gender", character.Gender), ("@now", now));
            return Convert.ToInt32(id);
        }

        public Task TouchCharacterAsync(int id) =>
            ExecuteAsync("UPDATE fiveprs_characters SET last_used_at = @now WHERE id = @id",
                ("@now", DateTime.UtcNow), ("@id", id));

        public Task DeleteCharacterAsync(int id) =>
            ExecuteAsync(
                "DELETE FROM fiveprs_records WHERE character_id = @id; " +
                "DELETE FROM fiveprs_licenses WHERE character_id = @id; " +
                "DELETE FROM fiveprs_vehicles WHERE character_id = @id; " +
                "DELETE FROM fiveprs_characters WHERE id = @id;",
                ("@id", id));

        public Task<List<LicenseInfo>> GetLicensesAsync(int characterId) =>
            QueryAsync("SELECT character_id, type, status, issued_at FROM fiveprs_licenses WHERE character_id = @id ORDER BY type",
                reader => new LicenseInfo
                {
                    CharacterId = Convert.ToInt32(reader.GetValue(0)),
                    Type        = reader.GetString(1),
                    Status      = (LicenseStatus)Convert.ToInt32(reader.GetValue(2)),
                    IssuedAt    = reader.GetDateTime(3),
                },
                ("@id", characterId));

        public Task SetLicenseAsync(int characterId, string type, LicenseStatus status)
        {
            var upsert = _db.Dialect == SqlDialect.MySql
                ? "ON DUPLICATE KEY UPDATE status = VALUES(status)"
                : "ON CONFLICT(character_id, type) DO UPDATE SET status = excluded.status";

            return ExecuteAsync(
                $"INSERT INTO fiveprs_licenses (character_id, type, status, issued_at) VALUES (@id, @type, @status, @now) {upsert}",
                ("@id", characterId), ("@type", type), ("@status", (int)status), ("@now", DateTime.UtcNow));
        }

        public Task<List<VehicleInfo>> GetVehiclesAsync(int characterId) =>
            QueryAsync($"SELECT {VehicleColumns} FROM fiveprs_vehicles WHERE character_id = @id ORDER BY registered_at, id",
                ReadVehicle, ("@id", characterId));

        public async Task<VehicleInfo?> GetVehicleAsync(int id)
        {
            var rows = await QueryAsync($"SELECT {VehicleColumns} FROM fiveprs_vehicles WHERE id = @id", ReadVehicle, ("@id", id));
            return rows.Count > 0 ? rows[0] : null;
        }

        public async Task<VehicleInfo?> GetVehicleByPlateAsync(string plate)
        {
            var rows = await QueryAsync($"SELECT {VehicleColumns} FROM fiveprs_vehicles WHERE plate = @plate", ReadVehicle, ("@plate", plate));
            return rows.Count > 0 ? rows[0] : null;
        }

        public async Task<int> AddVehicleAsync(VehicleInfo vehicle)
        {
            var id = await ScalarAsync(
                "INSERT INTO fiveprs_vehicles (character_id, plate, model, status, registered_at) " +
                $"VALUES (@character, @plate, @model, @status, @now); {LastInsertId};",
                ("@character", vehicle.CharacterId), ("@plate", vehicle.Plate), ("@model", vehicle.Model),
                ("@status", (int)vehicle.Status), ("@now", DateTime.UtcNow));
            return Convert.ToInt32(id);
        }

        public Task DeleteVehicleAsync(int id) =>
            ExecuteAsync("DELETE FROM fiveprs_vehicles WHERE id = @id", ("@id", id));

        public Task SetVehicleStatusAsync(int id, VehicleStatus status) =>
            ExecuteAsync("UPDATE fiveprs_vehicles SET status = @status WHERE id = @id", ("@status", (int)status), ("@id", id));

        public Task<List<CharacterSummary>> SearchCharactersAsync(string term, int limit)
        {
            var fullName = _db.Dialect == SqlDialect.MySql ? "CONCAT(c.first_name, ' ', c.last_name)" : "(c.first_name || ' ' || c.last_name)";
            return QueryAsync(
                $"SELECT c.id, c.first_name, c.last_name, c.date_of_birth, c.gender, {ActiveWarrantExists} " +
                $"FROM fiveprs_characters c WHERE c.first_name LIKE @term OR c.last_name LIKE @term OR {fullName} LIKE @term " +
                $"ORDER BY c.last_name, c.first_name LIMIT {limit}",
                ReadSummary, ("@term", $"%{term}%"));
        }

        public async Task<CharacterSummary?> GetSummaryAsync(int characterId)
        {
            var rows = await QueryAsync(
                $"SELECT c.id, c.first_name, c.last_name, c.date_of_birth, c.gender, {ActiveWarrantExists} FROM fiveprs_characters c WHERE c.id = @id",
                ReadSummary, ("@id", characterId));
            return rows.Count > 0 ? rows[0] : null;
        }

        public Task<List<RecordInfo>> GetRecordsAsync(int characterId) =>
            QueryAsync($"SELECT {RecordColumns} FROM fiveprs_records WHERE character_id = @id ORDER BY created_at DESC, id DESC",
                ReadRecord, ("@id", characterId));

        public async Task<RecordInfo?> GetRecordAsync(int id)
        {
            var rows = await QueryAsync($"SELECT {RecordColumns} FROM fiveprs_records WHERE id = @id", ReadRecord, ("@id", id));
            return rows.Count > 0 ? rows[0] : null;
        }

        public async Task<int> AddRecordAsync(RecordInfo record, string officerLicense)
        {
            var id = await ScalarAsync(
                "INSERT INTO fiveprs_records (character_id, type, description, fine, officer_license, officer_name, officer_callsign, created_at, active) " +
                $"VALUES (@character, @type, @description, @fine, @officerLicense, @officerName, @callsign, @now, @active); {LastInsertId};",
                ("@character", record.CharacterId), ("@type", (int)record.Type), ("@description", record.Description),
                ("@fine", record.Fine), ("@officerLicense", officerLicense), ("@officerName", record.OfficerName),
                ("@callsign", record.OfficerCallsign), ("@now", DateTime.UtcNow), ("@active", record.Active ? 1 : 0));
            return Convert.ToInt32(id);
        }

        public Task ResolveRecordAsync(int id, string resolution) =>
            ExecuteAsync("UPDATE fiveprs_records SET active = 0, resolution = @resolution WHERE id = @id",
                ("@resolution", resolution), ("@id", id));

        private static CharacterSummary ReadSummary(DbDataReader reader) => new()
        {
            Id               = Convert.ToInt32(reader.GetValue(0)),
            FirstName        = reader.GetString(1),
            LastName         = reader.GetString(2),
            DateOfBirth      = reader.GetString(3),
            Gender           = reader.GetString(4),
            HasActiveWarrant = Convert.ToInt32(reader.GetValue(5)) != 0,
        };

        private static RecordInfo ReadRecord(DbDataReader reader) => new()
        {
            Id              = Convert.ToInt32(reader.GetValue(0)),
            CharacterId     = Convert.ToInt32(reader.GetValue(1)),
            Type            = (RecordType)Convert.ToInt32(reader.GetValue(2)),
            Description     = reader.GetString(3),
            Fine            = Convert.ToInt32(reader.GetValue(4)),
            OfficerName     = reader.GetString(5),
            OfficerCallsign = reader.GetString(6),
            CreatedAt       = reader.GetDateTime(7),
            Active          = Convert.ToInt32(reader.GetValue(8)) != 0,
            Resolution      = reader.IsDBNull(9) ? null : reader.GetString(9),
        };

        private static CharacterInfo ReadCharacter(DbDataReader reader) => new()
        {
            Id           = Convert.ToInt32(reader.GetValue(0)),
            OwnerLicense = reader.GetString(1),
            FirstName    = reader.GetString(2),
            LastName     = reader.GetString(3),
            DateOfBirth  = reader.GetString(4),
            Gender       = reader.GetString(5),
            CreatedAt    = reader.GetDateTime(6),
            LastUsedAt   = reader.GetDateTime(7),
        };

        private static VehicleInfo ReadVehicle(DbDataReader reader) => new()
        {
            Id           = Convert.ToInt32(reader.GetValue(0)),
            CharacterId  = Convert.ToInt32(reader.GetValue(1)),
            Plate        = reader.GetString(2),
            Model        = reader.GetString(3),
            Status       = (VehicleStatus)Convert.ToInt32(reader.GetValue(4)),
            RegisteredAt = reader.GetDateTime(5),
        };

        private async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
        {
            using var conn = _db.CreateConnection();
            await conn.OpenAsync();
            using var cmd = CreateCommand(conn, sql, parameters);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task<object?> ScalarAsync(string sql, params (string Name, object Value)[] parameters)
        {
            using var conn = _db.CreateConnection();
            await conn.OpenAsync();
            using var cmd = CreateCommand(conn, sql, parameters);
            return await cmd.ExecuteScalarAsync();
        }

        private async Task<List<T>> QueryAsync<T>(string sql, Func<DbDataReader, T> read, params (string Name, object Value)[] parameters)
        {
            using var conn = _db.CreateConnection();
            await conn.OpenAsync();
            using var cmd = CreateCommand(conn, sql, parameters);
            using var reader = await cmd.ExecuteReaderAsync();

            var rows = new List<T>();
            while (await reader.ReadAsync())
                rows.Add(read(reader));
            return rows;
        }

        private static DbCommand CreateCommand(DbConnection conn, string sql, (string Name, object Value)[] parameters)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (name, value) in parameters)
            {
                var parameter = cmd.CreateParameter();
                parameter.ParameterName = name;
                parameter.Value = value;
                cmd.Parameters.Add(parameter);
            }
            return cmd;
        }
    }
}
