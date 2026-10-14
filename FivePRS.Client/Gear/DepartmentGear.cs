using System;
using System.Linq;
using CitizenFX.Core.Native;
using FivePRS.Client.Loadout;
using FivePRS.Client.Stations;
using FivePRS.Client.VehicleSpawner;
using FivePRS.Core.Config;
using FivePRS.Core.Models;
using FivePRS.Core.Text;

namespace FivePRS.Client.Gear
{
    public sealed class DepartmentGear
    {
        private static readonly LoadoutTier[] Tiers =
            { LoadoutTier.Recruit, LoadoutTier.Officer, LoadoutTier.Senior, LoadoutTier.Command };

        private static readonly LoadoutTier[] VehicleTiers =
            { LoadoutTier.Recruit, LoadoutTier.Senior, LoadoutTier.Command };

        private readonly Department _department;
        private readonly Func<LoadoutTier, string> _tierName;
        private readonly string _uniformName;

        public DepartmentGear(Department department, Func<LoadoutTier, string> tierName, string uniformName)
        {
            _department  = department;
            _tierName    = tierName;
            _uniformName = uniformName;
        }

        private DepartmentLoadoutsConfig Loadouts => ConfigManager.LoadoutsFor(_department);

        private DepartmentVehiclesConfig Vehicles => ConfigManager.VehiclesFor(_department);

        private RankLadder Ranks => ConfigManager.RanksFor(_department);

        public LoadoutDefinition LoadoutForRank(int rank, string? agencyId)
        {
            var tier   = Ranks.TierFor(rank);
            var outfit = ToOutfit(Loadouts.UniformFor(agencyId, tier));

            outfit.Name    = _tierName(tier);
            outfit.Weapons = Loadouts.WeaponsFor(agencyId, tier).Weapons.Select(ToEntry).ToArray();
            return outfit;
        }

        public PatrolVehicleConfig VehicleForRank(int rank, string? agencyId) =>
            ToConfig(Vehicles.TierFor(agencyId, Ranks.TierFor(rank)));

        public StationCatalog BuildCatalog(string? agencyId)
        {
            var catalog = new StationCatalog();
            AddWeapons(catalog, agencyId);
            AddUniforms(catalog, agencyId);
            AddVehicles(catalog, agencyId);
            return catalog;
        }

        private void AddWeapons(StationCatalog catalog, string? agencyId)
        {
            foreach (var tier in Tiers)
            {
                var weapons = Loadouts.WeaponsFor(agencyId, tier).Weapons;
                var minRank = Ranks.MinRankFor(tier);

                if (catalog.Kits.All(kit => kit.Name != _tierName(tier)))
                {
                    catalog.Kits.Add(new GearKit
                    {
                        Name    = _tierName(tier),
                        MinRank = minRank,
                        Weapons = weapons.Select(ToEntry).ToArray(),
                    });
                }

                foreach (var weapon in weapons)
                {
                    var label = WeaponLabels.For(weapon.Name, weapon.Label);
                    if (catalog.Weapons.Any(w => string.Equals(w.Label, label, StringComparison.Ordinal)))
                        continue;

                    var entry = ToEntry(weapon);
                    entry.SetAsCurrent = false;
                    catalog.Weapons.Add(new GearWeapon { Label = label, MinRank = minRank, Weapon = entry });
                }
            }
        }

        private void AddUniforms(StationCatalog catalog, string? agencyId)
        {
            var standard = Loadouts.UniformFor(agencyId, LoadoutTier.Recruit);
            var command  = Loadouts.UniformFor(agencyId, LoadoutTier.Command);

            catalog.Uniforms.Add(new GearUniform { Name = _uniformName, Outfit = ToOutfit(standard) });

            if (!ReferenceEquals(standard, command))
            {
                catalog.Uniforms.Add(new GearUniform
                {
                    Name    = "Command uniform",
                    MinRank = Ranks.MinRankFor(LoadoutTier.Command),
                    Outfit  = ToOutfit(command),
                });
            }
        }

        private void AddVehicles(StationCatalog catalog, string? agencyId)
        {
            foreach (var tier in VehicleTiers)
            {
                var vehicles = Vehicles.TierFor(agencyId, tier);

                foreach (var model in vehicles.Models)
                {
                    if (catalog.Vehicles.Any(v => string.Equals(v.Config.ModelPool[0], model, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    catalog.Vehicles.Add(new GearVehicle
                    {
                        Label   = VehicleLabel(model),
                        MinRank = Ranks.MinRankFor(tier),
                        Config  = ToConfig(vehicles, model),
                    });
                }
            }

            foreach (var group in catalog.Vehicles.GroupBy(v => v.Label).Where(g => g.Count() > 1))
            {
                foreach (var vehicle in group)
                    vehicle.Label = $"{vehicle.Label} ({vehicle.Config.ModelPool[0]})";
            }
        }

        private LoadoutDefinition ToOutfit(UniformDef uniform) => new()
        {
            Components       = ToComponents(uniform.Components),
            Props            = ToProps(uniform.Props),
            FemaleComponents = uniform.Female is null ? null : ToComponents(uniform.Female.Components),
            FemaleProps      = uniform.Female is null ? null : ToProps(uniform.Female.Props),
            MalePedModel           = NullIfEmpty(uniform.PedModels?.Male),
            FemalePedModel         = NullIfEmpty(uniform.PedModels?.Female),
            FallbackMalePedModel   = NullIfEmpty(Loadouts.FallbackPedModels.Male),
            FallbackFemalePedModel = NullIfEmpty(Loadouts.FallbackPedModels.Female),
        };

        private static ComponentEntry[] ToComponents(ClothingDef[] components) => components
            .Select(c => new ComponentEntry { ComponentId = c.Slot, DrawableId = c.Drawable, TextureId = c.Texture })
            .ToArray();

        private static PropEntry[] ToProps(ClothingDef[] props) => props
            .Select(p => new PropEntry { PropId = p.Slot, DrawableId = p.Drawable, TextureId = p.Texture })
            .ToArray();

        private static WeaponEntry ToEntry(WeaponDef weapon) => new()
        {
            Hash         = (uint)API.GetHashKey(weapon.Name),
            Ammo         = weapon.Ammo,
            SetAsCurrent = weapon.SetCurrent,
        };

        private static PatrolVehicleConfig ToConfig(VehicleTierDef tier, string? model = null) => new()
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

        private static string VehicleLabel(string model)
        {
            var label = API.GetLabelText(API.GetDisplayNameFromVehicleModel((uint)API.GetHashKey(model)));
            return string.IsNullOrEmpty(label) || label == "NULL" ? model : label;
        }

        private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
