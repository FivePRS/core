using System.Linq;
using CitizenFX.Core;
using FivePRS.Core.Config;
using FivePRS.Core.Models;

namespace FivePRS.Client.Stations
{
    public static class Hospitals
    {
        public static StationDef? Nearest(Vector3 position) =>
            ConfigManager.Stations.Stations
                .Where(s => s.Department == Department.EMS && s.DutyPoint.Length >= 3)
                .OrderBy(s => Vector3.DistanceSquared(position, StationService.PositionOf(s)))
                .FirstOrDefault();
    }
}
