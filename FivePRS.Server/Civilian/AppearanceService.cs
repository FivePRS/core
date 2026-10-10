using System;
using System.Threading.Tasks;
using FivePRS.Core.Civilian;
using FivePRS.Core.Config;
using FivePRS.Server.Database;
using Newtonsoft.Json;

namespace FivePRS.Server.Civilian
{
    public sealed class AppearanceService
    {
        private readonly CivilianStore _characters;
        private readonly AppearanceStore _store;
        private readonly Func<CreatorSettings> _settings;

        public AppearanceService(CivilianStore characters, AppearanceStore store, Func<CreatorSettings> settings)
        {
            _characters = characters;
            _store      = store;
            _settings   = settings;
        }

        public async Task<string?> GetAsync(int characterId)
        {
            var json = await _store.GetAsync(characterId);
            if (json is null) return null;

            var appearance = Parse(json);
            return appearance is null ? null : JsonConvert.SerializeObject(appearance);
        }

        public async Task<string?> SaveAsync(string owner, int characterId, string? json)
        {
            var character = await _characters.GetCharacterAsync(characterId);
            if (character is null || character.OwnerLicense != owner) return "Character not found.";

            var appearance = Parse(json);
            if (appearance is null) return "That appearance can't be saved.";

            await _store.SaveAsync(characterId, JsonConvert.SerializeObject(appearance));
            return null;
        }

        public Task DeleteAsync(int characterId) => _store.DeleteAsync(characterId);

        private CharacterAppearance? Parse(string? json)
        {
            if (json is null || json.Length == 0 || json.Length > AppearanceRules.MaxSerializedSize) return null;

            try
            {
                return AppearanceRules.Sanitize(JsonConvert.DeserializeObject<CharacterAppearance>(json),
                    _settings().AllowedStandardModels);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
