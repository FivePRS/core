using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using FivePRS.Core.Civilian;
using FivePRS.Server.Database;

namespace FivePRS.Server.Framework
{
    public sealed class ExternalIdentity
    {
        public string ExternalId { get; set; } = string.Empty;
        public string? OwnerLicense { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? DateOfBirth { get; set; }
        public string? Gender { get; set; }
    }

    public sealed class ExternalVehicle
    {
        public string Plate { get; set; } = string.Empty;
        public string? Model { get; set; }
    }

    public sealed class CharacterLinker
    {
        public const string UnknownName = "Unknown";
        public const string UnknownModel = "Unknown";
        public const string PlaceholderOwner = "framework:";

        private const int MaxModelLength = 40;

        private readonly CivilianStore _store;

        public CharacterLinker(CivilianStore store) => _store = store;

        public async Task<int> LinkAsync(ExternalIdentity identity)
        {
            var existing = await _store.FindByExternalIdAsync(identity.ExternalId);
            var owner = string.IsNullOrWhiteSpace(identity.OwnerLicense)
                ? existing?.OwnerLicense ?? PlaceholderOwner + identity.ExternalId
                : identity.OwnerLicense!;

            var character = new CharacterInfo
            {
                OwnerLicense = owner,
                FirstName    = CleanName(identity.FirstName),
                LastName     = CleanName(identity.LastName),
                DateOfBirth  = CleanDate(identity.DateOfBirth),
                Gender       = CivilianRules.TryNormalizeGender(identity.Gender, out var gender) ? gender : "Male",
                ExternalId   = identity.ExternalId,
            };

            if (existing is null)
                return await _store.CreateCharacterAsync(character);

            character.Id = existing.Id;
            if (existing.OwnerLicense != character.OwnerLicense || existing.FirstName != character.FirstName ||
                existing.LastName != character.LastName || existing.DateOfBirth != character.DateOfBirth || existing.Gender != character.Gender)
            {
                await _store.UpdateLinkedCharacterAsync(character);
            }

            return existing.Id;
        }

        public async Task<bool> LinkVehicleAsync(int characterId, ExternalVehicle vehicle)
        {
            if (!CivilianRules.TryNormalizePlate(vehicle.Plate, out var plate)) return false;

            var model = CleanModel(vehicle.Model);
            var existing = await _store.GetVehicleByPlateAsync(plate);
            if (existing is null)
            {
                await _store.AddVehicleAsync(new VehicleInfo { CharacterId = characterId, Plate = plate, Model = model, Status = VehicleStatus.Valid });
                return true;
            }

            if (existing.CharacterId != characterId || existing.Model != model)
                await _store.MoveVehicleAsync(existing.Id, characterId, model);
            return true;
        }

        public async Task SyncVehiclesAsync(int characterId, IEnumerable<ExternalVehicle> vehicles)
        {
            var owned = new HashSet<string>();
            foreach (var vehicle in vehicles)
            {
                if (await LinkVehicleAsync(characterId, vehicle) && CivilianRules.TryNormalizePlate(vehicle.Plate, out var plate))
                    owned.Add(plate);
            }

            foreach (var stale in (await _store.GetVehiclesAsync(characterId)).Where(v => !owned.Contains(v.Plate)))
                await _store.DeleteVehicleAsync(stale.Id);
        }

        private static string CleanName(string? name)
        {
            var text = (name ?? string.Empty).Trim();
            if (text.Length == 0) return UnknownName;
            return text.Length > CivilianRules.MaxNameLength ? text.Substring(0, CivilianRules.MaxNameLength).TrimEnd() : text;
        }

        private static string CleanDate(string? date) =>
            System.DateTime.TryParseExact((date ?? string.Empty).Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : string.Empty;

        private static string CleanModel(string? model)
        {
            var text = (model ?? string.Empty).Trim();
            if (text.Length == 0) return UnknownModel;
            return text.Length > MaxModelLength ? text.Substring(0, MaxModelLength) : text;
        }
    }
}
