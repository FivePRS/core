using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using FivePRS.Core.Civilian;

namespace FivePRS.Server.Database
{
    public sealed class CivilianStore : SqlStore
    {
        private const string CharacterColumns =
            "id, owner_license, first_name, last_name, date_of_birth, gender, created_at, last_used_at";

        private const string VehicleColumns = "id, character_id, plate, model, status, registered_at";

        private const string RecordColumns =
            "id, character_id, type, description, fine, officer_name, officer_callsign, created_at, active, resolution";

        private const string ActiveWarrantExists =
            "EXISTS (SELECT 1 FROM fiveprs_records r WHERE r.character_id = c.id AND r.type = 3 AND r.active = 1)";

        public CivilianStore(IDatabaseProvider db) : base(db)
        {
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
            var upsert = IsMySql
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
            var fullName = IsMySql ? "CONCAT(c.first_name, ' ', c.last_name)" : "(c.first_name || ' ' || c.last_name)";
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
    }
}
