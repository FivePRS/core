using System;
using System.Linq;
using System.Threading.Tasks;
using FivePRS.Core.Civilian;
using FivePRS.Core.Config;
using FivePRS.Server.Database;

namespace FivePRS.Server.Civilian
{
    public sealed class CivilianService
    {
        private const int MaxModelLength = 40;

        private readonly CivilianStore _store;
        private readonly PhotoStore _photos;
        private readonly Func<ResourceSettings> _settings;
        private readonly Func<LicensesConfig> _licenses;
        private readonly Func<DateTime> _clock;

        public CivilianService(CivilianStore store, PhotoStore photos, Func<ResourceSettings> settings, Func<LicensesConfig> licenses,
            Func<DateTime> clock)
        {
            _store    = store;
            _photos   = photos;
            _settings = settings;
            _licenses = licenses;
            _clock    = clock;
        }

        public async Task<CivilianState> GetStateAsync(string owner)
        {
            var characters = await _store.GetCharactersAsync(owner);
            var active     = characters.OrderByDescending(c => c.LastUsedAt).ThenByDescending(c => c.Id).FirstOrDefault();

            var state = new CivilianState
            {
                Characters        = characters,
                ActiveCharacterId = active?.Id,
                MaxCharacters     = _settings().MaxCharacters,
                MaxVehicles       = _settings().MaxVehiclesPerCharacter,
            };

            var held = active is null ? null : await _store.GetLicensesAsync(active.Id);
            state.Licenses = LicenseViews.Build(active?.Id ?? 0, _licenses(), held);

            var photos = await _photos.GetManyAsync(characters.Select(c => c.Id));
            foreach (var character in characters)
                character.Photo = photos.TryGetValue(character.Id, out var photo) ? photo : null;

            if (active is not null)
            {
                state.Vehicles = await _store.GetVehiclesAsync(active.Id);
                state.Records  = await _store.GetRecordsAsync(active.Id);
            }

            return state;
        }

        public async Task<CharacterInfo?> GetActiveCharacterAsync(string owner)
        {
            var characters = await _store.GetCharactersAsync(owner);
            return characters.OrderByDescending(c => c.LastUsedAt).ThenByDescending(c => c.Id).FirstOrDefault();
        }

        public async Task<string?> CreateCharacterAsync(string owner, string? firstName, string? lastName, string? dateOfBirth, string? gender)
        {
            if (!CivilianRules.TryNormalizeName(firstName, out var first) || !CivilianRules.TryNormalizeName(lastName, out var last))
                return $"Names must be 1 to {CivilianRules.MaxNameLength} letters; spaces, hyphens and apostrophes are allowed.";

            if (!CivilianRules.TryParseDateOfBirth(dateOfBirth, _clock(), out var dob))
                return $"Enter a valid date of birth (YYYY-MM-DD). Characters must be at least {CivilianRules.MinimumAge}.";

            if (!CivilianRules.TryNormalizeGender(gender, out var normalizedGender))
                return "Choose a gender.";

            var existing = await _store.GetCharactersAsync(owner);
            if (existing.Count >= _settings().MaxCharacters)
                return $"You can have up to {_settings().MaxCharacters} characters.";

            await _store.CreateCharacterAsync(new CharacterInfo
            {
                OwnerLicense = owner,
                FirstName    = first,
                LastName     = last,
                DateOfBirth  = dob,
                Gender       = normalizedGender,
            });
            return null;
        }

        public async Task<string?> SelectCharacterAsync(string owner, int characterId)
        {
            if (await GetOwnedCharacterAsync(owner, characterId) is null) return "Character not found.";

            await _store.TouchCharacterAsync(characterId);
            return null;
        }

        public async Task<string?> DeleteCharacterAsync(string owner, int characterId)
        {
            if (await GetOwnedCharacterAsync(owner, characterId) is null) return "Character not found.";

            var summary = await _store.GetSummaryAsync(characterId);
            if (summary?.HasActiveWarrant == true) return "A character with an active warrant can't be deleted.";

            await _store.DeleteCharacterAsync(characterId);
            await _photos.DeleteAsync(characterId);
            return null;
        }

        public async Task<LicenseCard?> GetLicenseCardAsync(string owner, string? type)
        {
            var character = await GetActiveCharacterAsync(owner);
            var config    = _licenses();
            var definition = config.Find(type);
            if (character is null || definition is null) return null;

            var held = await _store.GetLicensesAsync(character.Id);
            var view = LicenseViews.Build(character.Id, config, held).FirstOrDefault(l => l.Type == definition.Id);
            if (view?.Status is null) return null;

            return new LicenseCard
            {
                FirstName   = character.FirstName,
                LastName    = character.LastName,
                DateOfBirth = character.DateOfBirth,
                Gender      = character.Gender,
                StateId     = CivilianRules.StateId(config.State.Abbreviation, character.Id),
                Photo       = await _photos.GetAsync(character.Id),
                State       = config.State,
                License     = view,
            };
        }

        public async Task<string?> SetPhotoAsync(string owner, int characterId, string? photo)
        {
            if (await GetOwnedCharacterAsync(owner, characterId) is null) return "Character not found.";
            if (!CivilianRules.TryNormalizePhoto(photo, out var normalized))
                return "That photo can't be used. Take a mugshot, or use an https image link.";

            if (normalized.Length == 0)
                await _photos.DeleteAsync(characterId);
            else
                await _photos.SetAsync(characterId, normalized);
            return null;
        }

        public async Task<string?> ApplyForLicenseAsync(string owner, string? type)
        {
            var character = await GetActiveCharacterAsync(owner);
            if (character is null) return "Create a character first.";

            var definition = _licenses().Find(type);
            if (definition is null) return "Unknown license type.";
            if (!definition.SelfService) return $"A {definition.Name} must be issued by law enforcement.";

            var current = (await _store.GetLicensesAsync(character.Id))
                .FirstOrDefault(l => string.Equals(l.Type, definition.Id, StringComparison.OrdinalIgnoreCase));

            if (current is not null)
            {
                return current.Status == LicenseStatus.Valid
                    ? $"You already hold a {definition.Name}."
                    : $"Your {definition.Name} is {current.Status.ToString().ToLowerInvariant()}. Contact law enforcement.";
            }

            await _store.SetLicenseAsync(character.Id, definition.Id, LicenseStatus.Valid);
            return null;
        }

        public async Task<string?> RegisterVehicleAsync(string owner, string? plate, string? model)
        {
            var character = await GetActiveCharacterAsync(owner);
            if (character is null) return "Create a character first.";

            if (!CivilianRules.TryNormalizePlate(plate, out var normalizedPlate))
                return "This vehicle's plate can't be registered.";

            var existing = await _store.GetVehicleByPlateAsync(normalizedPlate);
            if (existing is not null)
            {
                return existing.CharacterId == character.Id
                    ? $"{normalizedPlate} is already registered to you."
                    : $"{normalizedPlate} is already registered to someone else.";
            }

            var vehicles = await _store.GetVehiclesAsync(character.Id);
            if (vehicles.Count >= _settings().MaxVehiclesPerCharacter)
                return $"You can register up to {_settings().MaxVehiclesPerCharacter} vehicles per character.";

            var modelName = (model ?? string.Empty).Trim();
            if (modelName.Length == 0) modelName = "Unknown";
            if (modelName.Length > MaxModelLength) modelName = modelName.Substring(0, MaxModelLength);

            await _store.AddVehicleAsync(new VehicleInfo
            {
                CharacterId = character.Id,
                Plate       = normalizedPlate,
                Model       = modelName,
                Status      = VehicleStatus.Valid,
            });
            return null;
        }

        public async Task<string?> RemoveVehicleAsync(string owner, int vehicleId)
        {
            if (await GetOwnedVehicleAsync(owner, vehicleId) is null) return "Vehicle not found.";

            await _store.DeleteVehicleAsync(vehicleId);
            return null;
        }

        public async Task<string?> SetVehicleStolenAsync(string owner, int vehicleId, bool stolen)
        {
            if (await GetOwnedVehicleAsync(owner, vehicleId) is null) return "Vehicle not found.";

            await _store.SetVehicleStatusAsync(vehicleId, stolen ? VehicleStatus.Stolen : VehicleStatus.Valid);
            return null;
        }

        private async Task<CharacterInfo?> GetOwnedCharacterAsync(string owner, int characterId)
        {
            var character = await _store.GetCharacterAsync(characterId);
            return character is not null && character.OwnerLicense == owner ? character : null;
        }

        private async Task<VehicleInfo?> GetOwnedVehicleAsync(string owner, int vehicleId)
        {
            var vehicle = await _store.GetVehicleAsync(vehicleId);
            if (vehicle is null) return null;
            return await GetOwnedCharacterAsync(owner, vehicle.CharacterId) is null ? null : vehicle;
        }
    }
}
