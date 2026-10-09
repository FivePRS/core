using System;
using System.Linq;
using CitizenFX.Core.Native;
using FivePRS.Client.Stations;
using FivePRS.Core.Config;
using FivePRS.Core.Text;

namespace FivePRS.Police.Config
{
    public static class PoliceCatalog
    {
        private static readonly LoadoutTier[] Tiers =
            { LoadoutTier.Recruit, LoadoutTier.Officer, LoadoutTier.Senior, LoadoutTier.Command };

        private static readonly LoadoutTier[] VehicleTiers =
            { LoadoutTier.Recruit, LoadoutTier.Senior, LoadoutTier.Command };

        public static StationCatalog Build(string? agencyId)
        {
            var catalog = new StationCatalog();
            AddWeapons(catalog, agencyId);
            AddUniforms(catalog, agencyId);
            AddVehicles(catalog, agencyId);
            return catalog;
        }

        private static void AddWeapons(StationCatalog catalog, string? agencyId)
        {
            var loadouts = ConfigManager.PoliceLoadouts;

            foreach (var tier in Tiers)
            {
                var weapons = loadouts.WeaponsFor(agencyId, tier).Weapons;
                var minRank = PoliceLoadoutsConfig.MinRankFor(tier);

                catalog.Kits.Add(new GearKit
                {
                    Name    = PoliceLoadouts.TierName(tier),
                    MinRank = minRank,
                    Weapons = weapons.Select(PoliceLoadouts.ToEntry).ToArray(),
                });

                foreach (var weapon in weapons)
                {
                    if (catalog.Weapons.Any(w => string.Equals(w.Label, WeaponLabels.For(weapon.Name, weapon.Label), StringComparison.Ordinal)))
                        continue;

                    var entry = PoliceLoadouts.ToEntry(weapon);
                    entry.SetAsCurrent = false;

                    catalog.Weapons.Add(new GearWeapon
                    {
                        Label   = WeaponLabels.For(weapon.Name, weapon.Label),
                        MinRank = minRank,
                        Weapon  = entry,
                    });
                }
            }
        }

        private static void AddUniforms(StationCatalog catalog, string? agencyId)
        {
            var loadouts = ConfigManager.PoliceLoadouts;
            var patrol   = loadouts.UniformFor(agencyId, LoadoutTier.Recruit);
            var command  = loadouts.UniformFor(agencyId, LoadoutTier.Command);

            catalog.Uniforms.Add(new GearUniform { Name = "Patrol uniform", Outfit = PoliceLoadouts.ToOutfit(patrol) });

            if (!ReferenceEquals(patrol, command))
            {
                catalog.Uniforms.Add(new GearUniform
                {
                    Name    = "Command uniform",
                    MinRank = PoliceLoadoutsConfig.MinRankFor(LoadoutTier.Command),
                    Outfit  = PoliceLoadouts.ToOutfit(command),
                });
            }
        }

        private static void AddVehicles(StationCatalog catalog, string? agencyId)
        {
            foreach (var tier in VehicleTiers)
            {
                var vehicles = ConfigManager.PoliceVehicles.TierFor(agencyId, tier);

                foreach (var model in vehicles.Models)
                {
                    if (catalog.Vehicles.Any(v => string.Equals(v.Config.ModelPool[0], model, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    catalog.Vehicles.Add(new GearVehicle
                    {
                        Label   = VehicleLabel(model),
                        MinRank = PoliceLoadoutsConfig.MinRankFor(tier),
                        Config  = PoliceVehicles.ToConfig(vehicles, model),
                    });
                }
            }

            foreach (var group in catalog.Vehicles.GroupBy(v => v.Label).Where(g => g.Count() > 1))
            {
                foreach (var vehicle in group)
                    vehicle.Label = $"{vehicle.Label} ({vehicle.Config.ModelPool[0]})";
            }
        }

        private static string VehicleLabel(string model)
        {
            var label = API.GetLabelText(API.GetDisplayNameFromVehicleModel((uint)API.GetHashKey(model)));
            return string.IsNullOrEmpty(label) || label == "NULL" ? model : label;
        }
    }
}
