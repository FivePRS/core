using System;
using System.Threading.Tasks;
using CitizenFX.Core;
using FivePRS.Client.Agency;
using FivePRS.Client.Callouts;
using FivePRS.Client.Loadout;
using FivePRS.Client.Stations;
using FivePRS.Client.VehicleSpawner;
using FivePRS.Core.Config;
using FivePRS.Core.Models;
using FivePRS.Police.Config;

namespace FivePRS.Police
{
    public class PoliceAgency : BaseAgency
    {
        public override Department Department => Department.Police;
        public override string AgencyName     => "Police Department";

        private readonly CalloutRegistry   _registry;
        private readonly CalloutDispatcher _dispatcher;

        private StationDef?          _arrivalStation;
        private PatrolVehicleConfig? _arrivalVehicle;

        public PoliceAgency()
        {
            _registry = new CalloutRegistry();
            _registry.Discover(GetType().Assembly);

            _dispatcher = new CalloutDispatcher(Department.Police, _registry, OnCalloutEnded);
        }

        public override StationCatalog? BuildCatalog(PlayerData player) => PoliceCatalog.Build(player.Agency);

        public override async Task OnDuty(PlayerData player)
        {
            await base.OnDuty(player);

            _registry.DiscoverAll();
            Debug.WriteLine($"[PoliceAgency] {_registry.Count} callout(s) registered.");
            _dispatcher.Start();

            var mode    = StationService.TakePendingStart(out var station);
            var message = ShouldEquip(mode)
                ? await EquipAsync(player, mode, station)
                : await ReportToStationAsync(mode, station);

            Notify($"~b~{DisplayName}~w~ | ~g~ON DUTY~w~~n~{message}");
        }

        private static bool ShouldEquip(DutyStartMode mode) => ConfigManager.Settings.DutyEquipment switch
        {
            DutyEquipmentMode.Always  => true,
            DutyEquipmentMode.Station => false,
            _                         => mode == DutyStartMode.Here,
        };

        private async Task<string> EquipAsync(PlayerData player, DutyStartMode mode, StationDef? station)
        {
            if (mode == DutyStartMode.Teleport && station is not null)
                await StationService.TeleportAsync(station);

            var loadout = PoliceLoadouts.GetForRank(player.Rank, player.Agency);
            await LoadoutManager.ApplyAsync(loadout);

            var vehicleConfig = PoliceVehicles.GetForRank(player.Rank, player.Agency);
            return $"{loadout.Name} loadout applied.~n~{await SpawnShiftVehicleAsync(player, mode, station, vehicleConfig)}";
        }

        private static async Task<string> ReportToStationAsync(DutyStartMode mode, StationDef? station)
        {
            if (mode == DutyStartMode.Drive && station is not null)
            {
                StationService.SetRoute(station);
                return $"Head to ~y~{station.Name}~w~ to collect your uniform, weapons and vehicle.";
            }

            if (mode == DutyStartMode.Teleport && station is not null)
            {
                await StationService.TeleportAsync(station);
                return "Collect your uniform, weapons and vehicle at the station.";
            }

            return "Visit one of your stations to collect your uniform, weapons and vehicle.";
        }

        private async Task<string> SpawnShiftVehicleAsync(PlayerData player, DutyStartMode mode, StationDef? station, PatrolVehicleConfig vehicleConfig)
        {
            if (mode == DutyStartMode.Drive && station is not null)
            {
                StationService.SetRoute(station);
                _arrivalStation = station;
                _arrivalVehicle = vehicleConfig;
                Tick += WaitForArrivalTick;
                return $"Head to ~y~{station.Name}~w~. Your patrol vehicle is waiting there.";
            }

            if (mode == DutyStartMode.Teleport && station is not null)
                return VehicleMessage(await Vehicles.SpawnAsync(vehicleConfig, StationService.ParkingSpot(station), false));

            var nearby = StationService.Nearby(Department, player.Agency);
            var vehicle = nearby is not null
                ? await Vehicles.SpawnAsync(vehicleConfig, StationService.ParkingSpot(nearby), false)
                : await Vehicles.SpawnAsync(vehicleConfig, PatrolVehicleSpawner.RoadSpawnPoint(), true);

            return VehicleMessage(vehicle);
        }

        private async Task WaitForArrivalTick()
        {
            if (_arrivalStation is null || _arrivalVehicle is null || !IsActive)
            {
                StopWaitingForArrival();
                return;
            }

            if (StationService.DistanceTo(_arrivalStation) > StationService.ArrivalRadius)
            {
                await Delay(1000);
                return;
            }

            var station = _arrivalStation;
            var config  = _arrivalVehicle;
            StopWaitingForArrival();

            var vehicle = await Vehicles.SpawnAsync(config, StationService.ParkingSpot(station), false);
            Notify($"~b~{station.Name}~w~ | {VehicleMessage(vehicle)}");
        }

        private void StopWaitingForArrival()
        {
            Tick -= WaitForArrivalTick;
            _arrivalStation = null;
            _arrivalVehicle = null;
        }

        private static string VehicleMessage(CitizenFX.Core.Vehicle? vehicle) =>
            vehicle is not null
                ? "Your patrol vehicle is marked on the map."
                : "~r~Vehicle spawn failed~w~ — proceed on foot.";

        public override async Task OffDuty(PlayerData player)
        {
            await base.OffDuty(player);

            _dispatcher.Stop();
            StopWaitingForArrival();
            Vehicles.Despawn();
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
