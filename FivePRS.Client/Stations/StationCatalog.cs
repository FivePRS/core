using System.Collections.Generic;
using FivePRS.Client.Loadout;
using FivePRS.Client.VehicleSpawner;

namespace FivePRS.Client.Stations
{
    public sealed class GearKit
    {
        public string Name { get; set; } = string.Empty;
        public int MinRank { get; set; } = 1;
        public IReadOnlyList<WeaponEntry> Weapons { get; set; } = new List<WeaponEntry>();
    }

    public sealed class GearWeapon
    {
        public string Label { get; set; } = string.Empty;
        public int MinRank { get; set; } = 1;
        public WeaponEntry Weapon { get; set; } = new();
    }

    public sealed class GearUniform
    {
        public string Name { get; set; } = string.Empty;
        public int MinRank { get; set; } = 1;
        public LoadoutDefinition Outfit { get; set; } = new();
    }

    public sealed class GearVehicle
    {
        public string Label { get; set; } = string.Empty;
        public int MinRank { get; set; } = 1;
        public PatrolVehicleConfig Config { get; set; } = new();
    }

    public sealed class StationCatalog
    {
        public List<GearKit> Kits { get; } = new();
        public List<GearWeapon> Weapons { get; } = new();
        public List<GearUniform> Uniforms { get; } = new();
        public List<GearVehicle> Vehicles { get; } = new();
    }
}
