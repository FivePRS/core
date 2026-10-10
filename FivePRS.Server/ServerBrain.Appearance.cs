using System;
using System.Threading.Tasks;
using CitizenFX.Core;
using FivePRS.Core.Events;
using FivePRS.Server.Civilian;

namespace FivePRS.Server
{
    public partial class ServerBrain
    {
        private AppearanceService? _appearances;

        private void RegisterAppearanceEvents()
        {
            EventHandlers[EventNames.ServerAppearanceSave] += new Action<Player, int, string>(OnAppearanceSave);
        }

        private void OnAppearanceSave([FromSource] Player player, int characterId, string json) =>
            RunCivilian(player, (_, license) => _appearances is null
                ? Task.FromResult<string?>("Appearances are not available yet.")
                : _appearances.SaveAsync(license, characterId, json));

        private async Task SendAppearanceAsync(Player player, string license)
        {
            if (_civilians is null || _appearances is null) return;

            var active = await _civilians.GetActiveCharacterAsync(license);
            var json   = active is null ? null : await _appearances.GetAsync(active.Id);
            TriggerClientEvent(player, EventNames.ClientAppearance, active?.Id ?? 0, json ?? string.Empty);
        }
    }
}
