using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using FivePRS.Core.Config;
using FivePRS.Core.Events;
using FivePRS.Core.Models;

namespace FivePRS.Server
{
    public partial class ServerBrain
    {
        private const int PositionFlushMs = 300_000;

        private readonly Dictionary<string, SavedPosition> _positions = new();
        private readonly HashSet<string> _unsavedPositions = new();

        private void RegisterPositionEvents()
        {
            EventHandlers[EventNames.ServerReportPosition]      += new Action<Player, float, float, float, float>(OnReportPosition);
            EventHandlers[EventNames.ServerLastPositionRequest] += new Action<Player>(OnLastPositionRequest);
            Tick += FlushPositionsTickAsync;
        }

        private void OnReportPosition([FromSource] Player player, float x, float y, float z, float heading)
        {
            var license  = GetLicense(player);
            var position = SavedPosition.Create(x, y, z, heading);
            if (license is null || position is null) return;

            _positions[license] = position;
            _unsavedPositions.Add(license);
        }

        private async void OnLastPositionRequest([FromSource] Player player)
        {
            var license = GetLicense(player);
            if (license is null) return;

            SavedPosition? position = null;
            try
            {
                if (ConfigManager.Settings.Spawn.RestoreLastLocation && _db.IsReady)
                    position = _positions.TryGetValue(license, out var cached) ? cached : await _db.Positions.GetAsync(license);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FivePRS] Could not load the last position for {player.Name}: {ex.Message}");
            }

            TriggerClientEvent(player, EventNames.ClientLastPosition, position is not null,
                position?.X ?? 0f, position?.Y ?? 0f, position?.Z ?? 0f, position?.Heading ?? 0f);
        }

        private async Task SavePositionAsync(string license)
        {
            if (!_db.IsReady || !_unsavedPositions.Remove(license) || !_positions.TryGetValue(license, out var position)) return;

            try
            {
                await _db.Positions.SaveAsync(license, position);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FivePRS] Could not save a position: {ex.Message}");
            }
        }

        private async Task ForgetPositionAsync(string license)
        {
            await SavePositionAsync(license);
            _positions.Remove(license);
        }

        private async Task FlushPositionsTickAsync()
        {
            await Delay(PositionFlushMs);
            foreach (var license in _unsavedPositions.ToList())
                await SavePositionAsync(license);
        }
    }
}
