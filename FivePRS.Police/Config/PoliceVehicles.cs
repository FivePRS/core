using FivePRS.Client.VehicleSpawner;
using FivePRS.Core.Config;

namespace FivePRS.Police.Config
{
    public static class PoliceVehicles
    {
        public static PatrolVehicleConfig GetForRank(int rank)
        {
            var cfg  = ConfigManager.PoliceVehicles;
            var tier = rank >= 8 ? cfg.Command : rank >= 5 ? cfg.Senior : cfg.Patrol;
            return ToConfig(tier);
        }

        private static PatrolVehicleConfig ToConfig(VehicleTierDef t) => new()
        {
            ModelPool      = t.Models,
            PrimaryColor   = t.PrimaryColor,
            SecondaryColor = t.SecondaryColor,
            DirtLevel      = t.DirtLevel,
            Livery         = t.Livery,
            PlateText      = t.PlateText,
            ForcedExtras   = t.ForcedExtras,
            DisabledExtras = t.DisabledExtras,
        };
    }
}
