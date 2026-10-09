using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Client.Menu;
using FivePRS.Core.Config;
using FivePRS.Core.Events;
using FivePRS.Core.Models;
using FivePRS.Core.Text;

namespace FivePRS.Client.Stations
{
    public class StationMarkers : BaseScript
    {
        private const float DrawDistance = 25f;
        private const float InteractDistance = 1.5f;
        private const int ScanIntervalMs = 500;
        private const int MarkerType = 1;

        private readonly List<int> _blips = new();
        private static readonly StationPoint[] Points =
            { StationPoint.Duty, StationPoint.Armory, StationPoint.Locker, StationPoint.Garage };

        private StationDef? _nearest;
        private StationPoint _nearestPoint;
        private float _nearestDistance;
        private int _nextScan;
        private bool _blipsCreated;

        public StationMarkers()
        {
            EventHandlers[EventNames.LocalDutyChanged] += new Action<bool, int>((_, __) => RefreshBlips());
            EventHandlers["onClientResourceStop"]      += new Action<string>(OnResourceStop);

            API.RegisterCommand(KeyCommands.Interact, new Action<int, List<object>, string>((_, __, ___) => Interact()), false);
            API.RegisterKeyMapping(KeyCommands.Interact, "FivePRS: Interact", "keyboard", "E");

            Tick += OnTick;
        }

        private async Task OnTick()
        {
            if (!_blipsCreated)
            {
                _blipsCreated = true;
                RefreshBlips();
            }

            var now = API.GetGameTimer();
            if (now >= _nextScan)
            {
                Scan();
                _nextScan = now + ScanIntervalMs;
            }

            if (_nearest is null)
            {
                await Delay(ScanIntervalMs);
                return;
            }

            DrawPoint(_nearest, _nearestPoint);

            if (_nearestDistance <= InteractDistance && !CompactMenu.IsOpen)
                ClientBrain.ShowHelp($"{Binds.Interact} {Prompt(_nearest, _nearestPoint)}", 0);
        }

        private static string Prompt(StationDef station, StationPoint point) => point switch
        {
            StationPoint.Armory => "Open the armory",
            StationPoint.Locker => "Open the locker",
            StationPoint.Garage => "Open the garage",
            _                   => StationMenu.CanUse(station) ? "Open the station menu" : "Open the duty menu",
        };

        private void Scan()
        {
            var ped = Game.PlayerPed;
            if (ped is null || !ped.Exists() || ped.IsInVehicle())
            {
                _nearest = null;
                return;
            }

            _nearest = null;
            _nearestDistance = DrawDistance;

            var position = ped.Position;

            foreach (var station in ConfigManager.Stations.Valid)
            {
                var canUse = StationMenu.CanUse(station);

                foreach (var point in Points)
                {
                    if (point != StationPoint.Duty && (!canUse || !StationService.HasOwnPoint(station, point))) continue;

                    var distance = Vector3.Distance(position, StationService.PositionOf(station, point));
                    if (distance < _nearestDistance)
                    {
                        _nearest = station;
                        _nearestPoint = point;
                        _nearestDistance = distance;
                    }
                }
            }
        }

        private void Interact()
        {
            if (_nearest is null || CompactMenu.IsOpen) return;

            Scan();
            if (_nearest is not null && _nearestDistance <= InteractDistance)
                StationMenu.Open(_nearest, _nearestPoint);
        }

        private static void DrawPoint(StationDef station, StationPoint point)
        {
            var position = StationService.PositionOf(station, point);
            var color    = MarkerColor(station.Department);

            API.DrawMarker(MarkerType, position.X, position.Y, position.Z - 0.98f,
                0f, 0f, 0f, 0f, 0f, 0f, 1.2f, 1.2f, 0.5f,
                color[0], color[1], color[2], 140, false, false, 2, false, null, null, false);
        }

        private static int[] MarkerColor(Department department) => department switch
        {
            Department.Fire => new[] { 220, 60, 50 },
            Department.EMS  => new[] { 60, 200, 120 },
            _               => new[] { 60, 130, 230 },
        };

        private void RefreshBlips()
        {
            ClearBlips();

            var config = ConfigManager.Stations;
            if (config.Blips == StationBlipMode.Off) return;

            var player   = ClientBrain.LocalPlayerData;
            var stations = config.Blips == StationBlipMode.OnDuty
                ? (player.IsOnDuty ? config.For(player.Department, player.Agency) : Enumerable.Empty<StationDef>())
                : config.Valid;

            foreach (var station in stations)
                _blips.Add(CreateBlip(station));
        }

        private static int CreateBlip(StationDef station)
        {
            var position = StationService.PositionOf(station);
            var style    = station.Blip ?? StationsConfig.DefaultBlip(station.Department);
            var blip     = API.AddBlipForCoord(position.X, position.Y, position.Z);

            API.SetBlipSprite(blip, style.Sprite);
            API.SetBlipColour(blip, style.Color);
            API.SetBlipScale(blip, 0.8f);
            API.SetBlipAsShortRange(blip, true);
            API.BeginTextCommandSetBlipName("STRING");
            API.AddTextComponentSubstringPlayerName(station.Name);
            API.EndTextCommandSetBlipName(blip);

            return blip;
        }

        private void ClearBlips()
        {
            foreach (var handle in _blips)
            {
                var blip = handle;
                if (API.DoesBlipExist(blip))
                    API.RemoveBlip(ref blip);
            }

            _blips.Clear();
        }

        private void OnResourceStop(string resourceName)
        {
            if (resourceName == API.GetCurrentResourceName())
                ClearBlips();
        }
    }
}
