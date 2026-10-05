using FivePRS.Client.VehicleSpawner;
using FivePRS.Core.Config;

namespace FivePRS.Police.Config
{
    public static class PoliceVehicles
    {
        public static PatrolVehicleConfig GetForRank(int rank, string? agencyId)
        {
            var tier = ConfigManager.PoliceVehicles.TierFor(agencyId, PoliceLoadoutsConfig.TierForRank(rank));

            return new PatrolVehicleConfig
            {
                ModelPool      = tier.Models,
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
}
