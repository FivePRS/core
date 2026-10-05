using System;
using System.Threading.Tasks;
using CitizenFX.Core;
using FivePRS.Client.Agency;
using FivePRS.Client.Callouts;
using FivePRS.Client.Loadout;
using FivePRS.Client.VehicleSpawner;
using FivePRS.Core.Models;
using FivePRS.Police.Config;

namespace FivePRS.Police
{
    public class PoliceAgency : BaseAgency
    {
        public override Department Department => Department.Police;
        public override string AgencyName     => "Police Department";

        private readonly CalloutRegistry     _registry;
        private readonly CalloutDispatcher   _dispatcher;
        private readonly PatrolVehicleSpawner _vehicleSpawner = new();

        public PoliceAgency()
        {
            _registry = new CalloutRegistry();
            _registry.Discover(GetType().Assembly);

            _dispatcher = new CalloutDispatcher(Department.Police, _registry, OnCalloutEnded);
        }

        public override async Task OnDuty(PlayerData player)
        {
            await base.OnDuty(player);

            var loadout = PoliceLoadouts.GetForRank(player.Rank, player.Agency);
            await LoadoutManager.ApplyAsync(loadout);

            var vehicleConfig = PoliceVehicles.GetForRank(player.Rank, player.Agency);
            var vehicle       = await _vehicleSpawner.SpawnAsync(vehicleConfig);

            _registry.DiscoverAll();
            Debug.WriteLine($"[PoliceAgency] {_registry.Count} callout(s) registered.");
            _dispatcher.Start();

            var vehicleMsg = vehicle is not null
                ? "~w~ Your patrol vehicle is marked on the map."
                : "~r~Vehicle spawn failed~w~ — proceed on foot.";

            Notify(
                $"~b~{DisplayName}~w~ | ~g~ON DUTY~w~ | " +
                $"{loadout.Name} loadout applied.~n~{vehicleMsg}");
        }

        public override async Task OffDuty(PlayerData player)
        {
            await base.OffDuty(player);

            _dispatcher.Stop();
            _vehicleSpawner.Despawn();
            await LoadoutManager.StripAsync();

            Notify($"~b~{DisplayName}~w~ | ~r~OFF DUTY~w~. Loadout and vehicle removed.");
        }

        public override async Task OnCalloutReceived(CalloutData callout)
        {
            await _dispatcher.HandleOfferAsync(callout);
        }

        private void OnCalloutEnded(CalloutBase callout, CalloutResult result)
        {
            if (result == CalloutResult.Completed)
            {
                Notify("~g~CALLOUT COMPLETE~w~");
            }
            else if (result == CalloutResult.Failed)
            {
                Notify("~r~CALLOUT FAILED~w~ | No XP awarded.");
            }

            Debug.WriteLine($"[PoliceAgency] Callout '{callout.Data.Name}' ended: {result}");
        }
    }
}
