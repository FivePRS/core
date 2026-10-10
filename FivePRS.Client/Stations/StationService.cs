using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Client.VehicleSpawner;
using FivePRS.Core.Config;
using FivePRS.Core.Models;

namespace FivePRS.Client.Stations
{
    public enum DutyStartMode
    {
        Here,
        Teleport,
        Drive
    }

    public static class StationService
    {
        public const float NearbyRadius = 75f;
        public const float ArrivalRadius = 150f;

        private const float ParkingClearance = 3f;

        private const float DeriveRadius = 200f;

        private static readonly Dictionary<string, Vector3> DerivedGarages = new();

        private static DutyStartMode _pendingMode;
        private static string? _pendingStationId;

        public static void SetPendingStart(DutyStartMode mode, string? stationId)
        {
            _pendingMode      = mode;
            _pendingStationId = stationId;
        }

        public static DutyStartMode TakePendingStart(out StationDef? station)
        {
            var mode = _pendingMode;
            station = ConfigManager.Stations.Find(_pendingStationId);

            _pendingMode      = DutyStartMode.Here;
            _pendingStationId = null;

            return station is null ? DutyStartMode.Here : mode;
        }

        public static Vector3 PositionOf(StationDef station) =>
            new(station.DutyPoint[0], station.DutyPoint[1], station.DutyPoint[2]);

        public static Vector3 PositionOf(StationDef station, StationPoint point) =>
            PointPosition(station, point) ?? PositionOf(station);

        public static bool HasOwnPoint(StationDef station, StationPoint point) =>
            point != StationPoint.Duty && PointPosition(station, point) is not null;

        public static Vector3? PointPosition(StationDef station, StationPoint point)
        {
            var configured = station.PointFor(point);
            if (configured is not null) return new Vector3(configured[0], configured[1], configured[2]);
            if (point != StationPoint.Garage) return null;

            var spot = station.Parking.FirstOrDefault(p => p.Length >= 3);
            if (spot is not null) return new Vector3(spot[0], spot[1], spot[2]);

            return DerivedGarage(station);
        }

        private static Vector3? DerivedGarage(StationDef station)
        {
            if (DerivedGarages.TryGetValue(station.Id, out var cached)) return cached;
            if (DistanceTo(station) > DeriveRadius) return null;

            var duty    = PositionOf(station);
            var node    = Vector3.Zero;
            var heading = 0f;
            if (!API.GetClosestVehicleNodeWithHeading(duty.X, duty.Y, duty.Z, ref node, ref heading, 1, 3f, 0)) return null;

            var kerb = Vector3.Zero;
            var position = API.GetSafeCoordForPed(node.X, node.Y, node.Z, true, ref kerb, 16) ? kerb : node;

            DerivedGarages[station.Id] = position;
            return position;
        }

        public static float DistanceTo(StationDef station) =>
            Vector3.Distance(Game.PlayerPed.Position, PositionOf(station));

        public static StationDef? Nearby(Department department, string? agencyId) =>
            ConfigManager.Stations.For(department, agencyId)
                .Select(s => new { Station = s, Distance = DistanceTo(s) })
                .Where(s => s.Distance <= NearbyRadius)
                .OrderBy(s => s.Distance)
                .Select(s => s.Station)
                .FirstOrDefault();

        public static void SetRoute(StationDef station)
        {
            var position = PositionOf(station);
            API.SetNewWaypoint(position.X, position.Y);
        }

        public static Task TeleportAsync(StationDef station) => Teleporter.ToAsync(PositionOf(station), station.Heading);

        public static SpawnPoint ParkingSpot(StationDef station)
        {
            foreach (var spot in station.Parking.Where(p => p.Length >= 4))
            {
                if (!API.IsPositionOccupied(spot[0], spot[1], spot[2], ParkingClearance, false, true, false, false, false, 0, false))
                    return new SpawnPoint(new Vector3(spot[0], spot[1], spot[2]), spot[3]);
            }

            var position = PositionOf(station, StationPoint.Garage);
            var node     = Vector3.Zero;
            var heading  = 0f;

            return API.GetClosestVehicleNodeWithHeading(position.X, position.Y, position.Z, ref node, ref heading, 1, 3f, 0)
                ? new SpawnPoint(node, heading)
                : new SpawnPoint(position, station.Heading);
        }
    }
}
