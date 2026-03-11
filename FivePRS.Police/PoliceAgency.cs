using System;
using System.Threading.Tasks;
using CitizenFX.Core;
using FivePRS.Client.Agency;
using FivePRS.Client.Callouts;
using FivePRS.Client.Loadout;
using FivePRS.Client.VehicleSpawner;
using FivePRS.Core.Events;
using FivePRS.Core.Models;
using FivePRS.Police.Config;

namespace FivePRS.Police
{
    /// <summary>
    /// Police department implementation.
    ///
    /// Adding new callouts requires zero changes here — decorate a CalloutBase subclass
    /// with [CalloutInfo(Department.Police, ...)] anywhere in this assembly and it is
    /// auto-discovered by the registry on startup.
    /// </summary>
    public class PoliceAgency : BaseAgency
    {
        public override Department Department => Department.Police;
        public override string AgencyName     => "Los Santos Police Department";

        private readonly CalloutRegistry     _registry;
        private readonly CalloutDispatcher   _dispatcher;
        private readonly PatrolVehicleSpawner _vehicleSpawner = new();

        public PoliceAgency()
        {
            _registry = new CalloutRegistry();
            _registry.Discover(GetType().Assembly);
            _registry.DiscoverAll();
            Debug.WriteLine($"[PoliceAgency] {_registry.Count} callout(s) registered.");

            var intervalMs = FivePRS.Core.Config.ConfigManager.Settings.DispatchIntervalMinutes * 60_000;
            _dispatcher = new CalloutDispatcher(
                Department.Police,
                _registry,
                intervalMs,
                OnCalloutEnded);
        }

        public override async Task OnDuty(PlayerData player)
        {
            await base.OnDuty(player);

            var loadout = PoliceLoadouts.GetForRank(player.Rank);
            await LoadoutManager.ApplyAsync(loadout);

            var vehicleConfig = PoliceVehicles.GetForRank(player.Rank);
            var vehicle       = await _vehicleSpawner.SpawnAsync(vehicleConfig);

            _dispatcher.Start();

            var vehicleMsg = vehicle is not null
                ? "~w~ Your patrol vehicle is marked on the map."
                : "~r~Vehicle spawn failed~w~ — proceed on foot.";

            Notify(
                $"~b~{AgencyName}~w~ | ~g~ON DUTY~w~ | " +
                $"{loadout.Name} loadout applied.~n~{vehicleMsg}");
        }

        public override async Task OffDuty(PlayerData player)
        {
            await base.OffDuty(player);

            _dispatcher.Stop();
            _vehicleSpawner.Despawn();
            LoadoutManager.Strip();

            Notify($"~b~{AgencyName}~w~ | ~r~OFF DUTY~w~. Loadout and vehicle removed.");
        }

        public override async Task OnCalloutReceived(CalloutData callout)
        {
            await _dispatcher.HandleServerCalloutAsync(callout);
        }

        private void OnCalloutEnded(CalloutBase callout, CalloutResult result)
        {
            if (result == CalloutResult.Completed)
            {
                TriggerServerEvent(
                    EventNames.ServerCalloutCompleted,
                    callout.Data.Id,
                    callout.Data.XPReward);

                Notify($"~g~CALLOUT COMPLETE~w~ | ~y~+{callout.Data.XPReward} XP");
            }
            else if (result == CalloutResult.Failed)
            {
                Notify("~r~CALLOUT FAILED~w~ | No XP awarded.");
            }

            Debug.WriteLine($"[PoliceAgency] Callout '{callout.Data.Name}' ended: {result}");
        }
    }
}
