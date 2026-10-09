using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace FivePRS.Core.Config
{
    public sealed class WeaponDef
    {
        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("ammo")]
        public int Ammo { get; set; }

        [JsonProperty("setCurrent", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool SetCurrent { get; set; }

        [JsonProperty("label", NullValueHandling = NullValueHandling.Ignore)]
        public string? Label { get; set; }
    }

    public sealed class WeaponTierDef
    {
        [JsonProperty("weapons")]
        public WeaponDef[] Weapons { get; set; } = new WeaponDef[0];
    }

    public sealed class ClothingDef
    {
        [JsonProperty("slot")]
        public int Slot { get; set; }

        [JsonProperty("drawable")]
        public int Drawable { get; set; }

        [JsonProperty("texture")]
        public int Texture { get; set; }
    }

    public sealed class PedModelsDef
    {
        [JsonProperty("male")]
        public string Male { get; set; } = string.Empty;

        [JsonProperty("female")]
        public string Female { get; set; } = string.Empty;
    }

    public sealed class UniformDef
    {
        [JsonProperty("components")]
        public ClothingDef[] Components { get; set; } = new ClothingDef[0];

        [JsonProperty("props")]
        public ClothingDef[] Props { get; set; } = new ClothingDef[0];

        [JsonProperty("pedModels", NullValueHandling = NullValueHandling.Ignore)]
        public PedModelsDef? PedModels { get; set; }
    }

    public sealed class AgencyLoadoutDef
    {
        [JsonProperty("recruit", NullValueHandling = NullValueHandling.Ignore)]
        public WeaponTierDef? Recruit { get; set; }

        [JsonProperty("officer", NullValueHandling = NullValueHandling.Ignore)]
        public WeaponTierDef? Officer { get; set; }

        [JsonProperty("senior", NullValueHandling = NullValueHandling.Ignore)]
        public WeaponTierDef? Senior { get; set; }

        [JsonProperty("command", NullValueHandling = NullValueHandling.Ignore)]
        public WeaponTierDef? Command { get; set; }

        [JsonProperty("uniform", NullValueHandling = NullValueHandling.Ignore)]
        public UniformDef? Uniform { get; set; }

        [JsonProperty("commandUniform", NullValueHandling = NullValueHandling.Ignore)]
        public UniformDef? CommandUniform { get; set; }
    }

    public enum LoadoutTier
    {
        Recruit,
        Officer,
        Senior,
        Command
    }

    public sealed class PoliceLoadoutsConfig
    {
        [JsonProperty("recruit")]
        public WeaponTierDef Recruit { get; set; } = Tier(
            Weapon("WEAPON_NIGHTSTICK", 1),
            Weapon("WEAPON_FLASHLIGHT", 1),
            Weapon("WEAPON_STUNGUN", 5),
            Weapon("WEAPON_PISTOL", 250, setCurrent: true));

        [JsonProperty("officer")]
        public WeaponTierDef Officer { get; set; } = Tier(
            Weapon("WEAPON_NIGHTSTICK", 1),
            Weapon("WEAPON_FLASHLIGHT", 1),
            Weapon("WEAPON_STUNGUN", 10),
            Weapon("WEAPON_PUMPSHOTGUN", 50),
            Weapon("WEAPON_PISTOL", 250, setCurrent: true));

        [JsonProperty("senior")]
        public WeaponTierDef Senior { get; set; } = Tier(
            Weapon("WEAPON_NIGHTSTICK", 1),
            Weapon("WEAPON_FLASHLIGHT", 1),
            Weapon("WEAPON_STUNGUN", 15),
            Weapon("WEAPON_PUMPSHOTGUN", 75),
            Weapon("WEAPON_CARBINERIFLE", 200),
            Weapon("WEAPON_PISTOL", 500, setCurrent: true));

        [JsonProperty("command")]
        public WeaponTierDef Command { get; set; } = Tier(
            Weapon("WEAPON_NIGHTSTICK", 1),
            Weapon("WEAPON_FLASHLIGHT", 1),
            Weapon("WEAPON_STUNGUN", 15),
            Weapon("WEAPON_PUMPSHOTGUN", 75),
            Weapon("WEAPON_CARBINERIFLE", 200),
            Weapon("WEAPON_PISTOL", 500, setCurrent: true));

        [JsonProperty("uniform")]
        public UniformDef Uniform { get; set; } = new()
        {
            Components = new[]
            {
                Clothing(3, 4), Clothing(4, 24), Clothing(6, 24),
                Clothing(8, 58), Clothing(9, 0), Clothing(11, 55),
            },
            Props = new[] { Clothing(0, 46), Clothing(1, -1, -1) },
        };

        [JsonProperty("commandUniform")]
        public UniformDef CommandUniform { get; set; } = new()
        {
            Components = new[]
            {
                Clothing(3, 4), Clothing(4, 24), Clothing(6, 24),
                Clothing(8, 58), Clothing(9, 0), Clothing(11, 48),
            },
            Props = new[] { Clothing(0, -1, -1), Clothing(1, -1, -1) },
        };

        [JsonProperty("fallbackPedModels")]
        public PedModelsDef FallbackPedModels { get; set; } = new() { Male = "s_m_y_cop_01", Female = "s_f_y_cop_01" };

        [JsonProperty("agencies", ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public Dictionary<string, AgencyLoadoutDef> Agencies { get; set; } = new();

        public static LoadoutTier TierForRank(int rank) =>
            rank >= 8 ? LoadoutTier.Command :
            rank >= 5 ? LoadoutTier.Senior  :
            rank >= 3 ? LoadoutTier.Officer : LoadoutTier.Recruit;

        public static int MinRankFor(LoadoutTier tier) => tier switch
        {
            LoadoutTier.Command => 8,
            LoadoutTier.Senior  => 5,
            LoadoutTier.Officer => 3,
            _                   => 1,
        };

        public WeaponTierDef WeaponsFor(string? agencyId, LoadoutTier tier)
        {
            var agency = FindAgency(agencyId);
            return tier switch
            {
                LoadoutTier.Command => agency?.Command ?? Command,
                LoadoutTier.Senior  => agency?.Senior  ?? Senior,
                LoadoutTier.Officer => agency?.Officer ?? Officer,
                _                   => agency?.Recruit ?? Recruit,
            };
        }

        public UniformDef UniformFor(string? agencyId, LoadoutTier tier)
        {
            var agency = FindAgency(agencyId);
            return tier == LoadoutTier.Command
                ? agency?.CommandUniform ?? agency?.Uniform ?? CommandUniform
                : agency?.Uniform ?? Uniform;
        }

        private AgencyLoadoutDef? FindAgency(string? agencyId) =>
            agencyId is null
                ? null
                : Agencies.FirstOrDefault(a => string.Equals(a.Key, agencyId, StringComparison.OrdinalIgnoreCase)).Value;

        private static WeaponTierDef Tier(params WeaponDef[] weapons) => new() { Weapons = weapons };

        private static WeaponDef Weapon(string name, int ammo, bool setCurrent = false) =>
            new() { Name = name, Ammo = ammo, SetCurrent = setCurrent };

        private static ClothingDef Clothing(int slot, int drawable, int texture = 0) =>
            new() { Slot = slot, Drawable = drawable, Texture = texture };
    }
}
