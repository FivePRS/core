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

        private const int FadeMs = 400;
        private const int CollisionTimeoutMs = 5000;
        private const float ParkingClearance = 3f;

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

        public static Vector3 PositionOf(StationDef station, StationPoint point)
        {
            var position = station.PointFor(point) ?? station.DutyPoint;
            return new Vector3(position[0], position[1], position[2]);
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

        public static async Task TeleportAsync(StationDef station)
        {
            var ped      = Game.PlayerPed.Handle;
            var position = PositionOf(station);

            API.DoScreenFadeOut(FadeMs);
            await BaseScript.Delay(FadeMs);

            API.RequestCollisionAtCoord(position.X, position.Y, position.Z);
            API.SetEntityCoords(ped, position.X, position.Y, position.Z, false, false, false, false);
            API.SetEntityHeading(ped, station.Heading);

            var until = API.GetGameTimer() + CollisionTimeoutMs;
            while (!API.HasCollisionLoadedAroundEntity(ped) && API.GetGameTimer() < until)
                await BaseScript.Delay(0);

            API.DoScreenFadeIn(FadeMs);
        }

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
