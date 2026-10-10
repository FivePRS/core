using System;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Client.App;
using FivePRS.Client.Appearance;
using FivePRS.Core.Config;
using FivePRS.Core.Events;

namespace FivePRS.Client
{
    public class LastLocation : BaseScript
    {
        private const int ReplyTimeoutMs = 15_000;
        private const float MinimumMove = 5f;
        private const float RestoreDistance = 15f;

        private bool _requested;
        private bool _restored;
        private int _requestedAt;
        private int _nextReport;
        private Vector3 _lastReported;

        public LastLocation()
        {
            EventHandlers[EventNames.ClientLastPosition] += new Action<bool, float, float, float, float>(OnLastPosition);
            Tick += OnTick;
        }

        private async Task OnTick()
        {
            var settings = ConfigManager.Settings.Spawn;
            if (!settings.RestoreLastLocation)
            {
                await Delay(5000);
                return;
            }

            var ped = Game.PlayerPed;
            var now = API.GetGameTimer();

            if (!_requested)
            {
                if (ped.Exists() && DutyPanel.IsLoaded && API.IsScreenFadedIn() && !API.IsPlayerSwitchInProgress())
                {
                    _requested   = true;
                    _requestedAt = now;
                    ClientEvents.TriggerServer(EventNames.ServerLastPositionRequest);
                }

                await Delay(1000);
                return;
            }

            if (!_restored)
            {
                if (now - _requestedAt > ReplyTimeoutMs) _restored = true;
                await Delay(500);
                return;
            }

            if (now >= _nextReport)
            {
                _nextReport = now + Math.Max(5, settings.SaveIntervalSeconds) * 1000;
                Report(ped);
            }

            await Delay(1000);
        }

        private void Report(Ped ped)
        {
            if (!ped.Exists() || ped.IsDead || CharacterCreator.IsOpen) return;

            var position = ped.Position;
            if (Vector3.Distance(position, _lastReported) < MinimumMove) return;

            _lastReported = position;
            ClientEvents.TriggerServer(EventNames.ServerReportPosition, position.X, position.Y, position.Z, ped.Heading);
        }

        private async void OnLastPosition(bool found, float x, float y, float z, float heading)
        {
            try
            {
                var ped = Game.PlayerPed;
                var target = new Vector3(x, y, z);

                if (found && ped.Exists() && !ped.IsDead && !ped.IsInVehicle() &&
                    Vector3.Distance(ped.Position, target) > RestoreDistance)
                {
                    await Teleporter.ToAsync(target, heading);
                    ClientBrain.ShowNotification("Welcome back. You're where you left off.", "FivePRS");
                }
            }
            finally
            {
                _lastReported = Game.PlayerPed.Position;
                _restored = true;
            }
        }
    }
}
