using System;
using System.Threading;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Client;
using FivePRS.Client.Arrest;
using FivePRS.Client.Callouts;
using FivePRS.Client.Tasks;
using FivePRS.Core.Models;

namespace FivePRS.Police.Callouts
{
    [CalloutInfo(
        name:            "Shoplifting",
        department:      Department.Police,
        priority:        CalloutPriority.Low,
        weight:          20,
        cooldownSeconds: 300,
        xpReward:        75)]
    public sealed class ShopliftingCallout : CalloutBase
    {

        private static readonly Vector3[] StoreLocations =
        {
            new( 25.7f,  -1344.4f, 29.5f),
            new(-47.1f,  -1757.5f, 29.4f),
            new(1163.2f,  -323.4f, 69.2f),
            new(-706.3f,  -913.3f, 19.2f),
            new( 545.0f,  2656.9f, 42.0f),
            new(-3040.3f, 584.2f,  7.9f),
        };

        private static readonly string[] SuspectModels =
        {
            "a_m_y_hipster_01",
            "a_f_y_hipster_01",
            "a_m_m_business_01",
            "a_f_m_business_02",
            "a_m_y_skater_01",
            "a_f_y_scdressy_01",
        };

        private readonly Vector3 _storePosition;
        private readonly string  _suspectModelName;

        private Ped? _suspect;

        public ShopliftingCallout()
        {
            var rng           = new Random();
            _storePosition    = StoreLocations[rng.Next(StoreLocations.Length)];
            _suspectModelName = SuspectModels[rng.Next(SuspectModels.Length)];

            Data.Description = "Report of a shoplifter fleeing on foot from a convenience store. Suspect is considered non-violent.";
        }

        public override Vector3 GetDispatchLocation() => _storePosition;

        public override bool CanBeDispatched()
        {
            var hour = World.CurrentDayTime.Hours;
            return hour is >= 6 and <= 23;
        }

        public override async Task OnCalloutAccepted(CancellationToken ct)
        {
            var blip           = TrackBlip(World.CreateBlip(_storePosition));
            blip.Sprite        = BlipSprite.Store;
            blip.Color         = BlipColor.Red;
            blip.Name          = "Shoplifting — Suspect Last Seen";
            blip.IsShortRange  = false;
            blip.ShowRoute     = true;

            var model = new Model(_suspectModelName);
            if (!await model.Request(7_000))
            {
                Debug.WriteLine($"[ShopliftingCallout] Model '{_suspectModelName}' failed to load.");
                CalloutFailed();
                return;
            }

            var offset  = new Vector3(new Random().Next(-5, 5) + 0.5f, new Random().Next(-5, 5) + 0.5f, 0f);
            _suspect    = TrackEntity(await World.CreatePed(model, _storePosition + offset));
            model.MarkAsNoLongerNeeded();

            if (_suspect is null || !_suspect.Exists())
            {
                Debug.WriteLine("[ShopliftingCallout] Ped creation failed.");
                CalloutFailed();
                return;
            }

            _suspect.IsInvincible = false;
            _suspect.RelationshipGroup = (uint)Game.GenerateHash("WANTED_PLAYER");

            API.SetPedFleeAttributes(_suspect.Handle, 0,   false);
            API.SetPedCombatAttributes(_suspect.Handle, 17, true);
            await TaskManager.AssignTaskAsync(_suspect, PedTaskType.FleeFromPlayer);

            ClientBrain.ShowNotification(
                $"~b~{Data.Name}~w~ | Suspect spotted — ~r~pursue on foot~w~.");

            Debug.WriteLine($"[ShopliftingCallout] Suspect spawned at {_storePosition}.");

            const float ArrestRadiusM    = 3.0f;
            const float EscapeRadiusM    = 300.0f;
            const int   LoopIntervalMs   = 500;

            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(LoopIntervalMs, ct).ConfigureAwait(false);

                if (_suspect is null || !_suspect.Exists() || _suspect.IsDead)
                {
                    ClientBrain.ShowNotification("~g~Suspect down~w~ | Callout complete.");
                    CalloutCompleted();
                    return;
                }

                var distToSuspect = Vector3.Distance(Game.PlayerPed.Position, _suspect.Position);

                if (distToSuspect <= ArrestRadiusM)
                {
                    await PerformArrestSequenceAsync(ct);
                    return;
                }

                if (distToSuspect > EscapeRadiusM)
                {
                    ClientBrain.ShowNotification("~r~Suspect escaped~w~ | Callout failed.");
                    CalloutFailed();
                    return;
                }
            }
        }

        private async Task PerformArrestSequenceAsync(CancellationToken ct)
        {
            if (_suspect is null) return;

            _suspect.Task.StandStill(-1);
            await TaskManager.AssignTaskAsync(_suspect, PedTaskType.PutHandsUp);

            ArrestManager.RegisterSuspect(_suspect);

            ClientBrain.ShowNotification(
                "~g~Suspect cornered~w~ | Type ~b~/er_cuff~w~ to handcuff them.");

            const int PollMs = 250;
            while (!ct.IsCancellationRequested)
            {
                API.BeginTextCommandDisplayHelp("STRING");
                API.AddTextComponentSubstringPlayerName(
                    "Type ~b~/er_cuff~w~ to arrest the suspect");
                API.EndTextCommandDisplayHelp(0, false, false, PollMs + 50);

                if (ArrestManager.IsCuffed &&
                    ArrestManager.CuffedPed?.Handle == _suspect.Handle)
                {
                    ClientBrain.ShowNotification(
                        "~g~Suspect cuffed~w~ | Return to station or press ~b~/er_end_callout~w~.");
                    CalloutCompleted();
                    return;
                }

                if (!_suspect.Exists() || _suspect.IsDead)
                {
                    ArrestManager.UnregisterSuspect(_suspect);
                    ClientBrain.ShowNotification("~g~Suspect down~w~ | Callout complete.");
                    CalloutCompleted();
                    return;
                }

                await Task.Delay(PollMs, ct).ConfigureAwait(false);
            }

            ArrestManager.UnregisterSuspect(_suspect);
        }

        public override void OnUpdate()
        {
            if (_suspect is null || !_suspect.Exists()) return;

            var dist = Vector3.Distance(Game.PlayerPed.Position, _suspect.Position);
            if (dist is > 5f and < 80f)
            {
                API.BeginTextCommandDisplayHelp("STRING");
                API.AddTextComponentSubstringPlayerName($"~w~Suspect ~r~{dist:F0}m~w~ away");
                API.EndTextCommandDisplayHelp(0, false, false, 3_000);
            }
        }

        public override void OnCalloutDeclined()
            => ClientBrain.ShowNotification("~r~[ DISPATCH ]~w~ Shoplifting callout declined.");

        public override void OnCalloutFailed()
            => ClientBrain.ShowNotification("~r~[ DISPATCH ]~w~ Shoplifting callout cancelled.");
    }
}
