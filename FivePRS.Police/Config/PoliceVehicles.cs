using FivePRS.Client.VehicleSpawner;
using FivePRS.Core.Config;

namespace FivePRS.Police.Config
{
    public static class PoliceVehicles
    {
        public static PatrolVehicleConfig GetForRank(int rank, string? agencyId) =>
            ToConfig(ConfigManager.PoliceVehicles.TierFor(agencyId, PoliceLoadoutsConfig.TierForRank(rank)));

        public static PatrolVehicleConfig ToConfig(VehicleTierDef tier, string? model = null) => new()
        {
            ModelPool      = model is null ? tier.Models : new[] { model },
            PrimaryColor   = tier.PrimaryColor,
            SecondaryColor = tier.SecondaryColor,
            DirtLevel      = tier.DirtLevel,
            Livery         = tier.Livery,
            PlateText      = tier.PlateText,
            ForcedExtras   = tier.ForcedExtras,
            DisabledExtras = tier.DisabledExtras,
        };
    }
}
