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
        private const int HandledTimeoutMs = 2_000;
        private const float MinimumMove = 5f;
        private const float RestoreDistance = 15f;

        private bool _requested;
        private bool _restored;
        private int _requestedAt;
        private int _nextReport;
        private Vector3 _lastReported;
        private TaskCompletionSource<bool>? _handled;

        public LastLocation()
        {
            EventHandlers[EventNames.ClientLastPosition] += new Action<bool, float, float, float, float, string>(OnLastPosition);
            EventHandlers[EventNames.LocalSpawnHandled]  += new Action<bool>(handled => _handled?.TrySetResult(handled));
            Tick += OnTick;
        }

        private async Task OnTick()
        {
            var ped = Game.PlayerPed;
            var now = API.GetGameTimer();

            if (!_requested)
            {
                if (DutyPanel.IsLoaded)
                {
                    _requested   = true;
                    _requestedAt = now;
                    ClientEvents.TriggerServer(EventNames.ServerLastPositionRequest);
                }

                await Delay(250);
                return;
            }

            if (!_restored)
            {
                if (now - _requestedAt > ReplyTimeoutMs) _restored = true;
                await Delay(500);
                return;
            }

            var settings = ConfigManager.Settings.Spawn;
            if (settings.RestoreLastLocation && now >= _nextReport)
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

        private async void OnLastPosition(bool found, float x, float y, float z, float heading, string model)
        {
            try
            {
                _handled = new TaskCompletionSource<bool>();
                ClientEvents.TriggerLocal(EventNames.LocalSpawnTarget, found, x, y, z, heading, model ?? string.Empty);
                var replied = await Task.WhenAny(_handled.Task, Delay(HandledTimeoutMs));
                var handled = replied == _handled.Task && _handled.Task.Result;

                if (!handled)
                {
                    await WaitForSpawnAsync();
                    var ped = Game.PlayerPed;
                    var target = new Vector3(x, y, z);
                    if (found && ped.Exists() && !ped.IsDead && !ped.IsInVehicle() && Vector3.Distance(ped.Position, target) > RestoreDistance)
                        await Teleporter.ToAsync(target, heading);
                }

                await WaitForSpawnAsync();
                if (AppearanceManager.Current is null && AppearanceManager.CharacterId != 0)
                    ClientBrain.ShowNotification("Your character doesn't have a look yet. Open the Characters app and choose Edit appearance.", "Characters");
            }
            finally
            {
                _lastReported = Game.PlayerPed.Position;
                _restored = true;
            }
        }

        private static async Task WaitForSpawnAsync()
        {
            var deadline = API.GetGameTimer() + ReplyTimeoutMs * 2;
            while ((!Game.PlayerPed.Exists() || !API.IsScreenFadedIn() || API.IsPlayerSwitchInProgress()) && API.GetGameTimer() < deadline)
                await Delay(250);
        }
    }
}
