using System;
using Newtonsoft.Json;

namespace FivePRS.Core.Config
{
    /// <summary>
    /// One weapon entry inside a config/police_loadouts.json tier.
    /// Uses the full weapon name (e.g. "WEAPON_PISTOL") so server owners don't need to know Jenkins hashes.
    /// The client converts the name to a hash at runtime via API.GetHashKey().
    /// </summary>
    public sealed class WeaponDef
    {
        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("ammo")]
        public int Ammo { get; set; } = 0;

        [JsonProperty("setCurrent")]
        public bool SetCurrent { get; set; } = false;
    }

    /// <summary>Weapon list for one loadout tier (recruit / officer / senior / command).</summary>
    public sealed class WeaponTierDef
    {
        [JsonProperty("weapons")]
        public WeaponDef[] Weapons { get; set; } = Array.Empty<WeaponDef>();
    }

    /// <summary>
    /// Loaded from config/police_loadouts.json.
    /// Defines weapon sets for each Police rank tier. Uniform components are still controlled
    /// by PoliceLoadouts.cs since they depend on the server's ped models.
    /// </summary>
    public sealed class PoliceLoadoutsConfig
    {
        [JsonProperty("recruit")]
        public WeaponTierDef Recruit { get; set; } = new()
        {
            Weapons = new[]
            {
                new WeaponDef { Name = "WEAPON_NIGHTSTICK",  Ammo = 1   },
                new WeaponDef { Name = "WEAPON_FLASHLIGHT",  Ammo = 1   },
                new WeaponDef { Name = "WEAPON_STUNGUN",     Ammo = 5   },
                new WeaponDef { Name = "WEAPON_PISTOL",      Ammo = 250, SetCurrent = true },
            }
        };

        [JsonProperty("officer")]
        public WeaponTierDef Officer { get; set; } = new()
        {
            Weapons = new[]
            {
                new WeaponDef { Name = "WEAPON_NIGHTSTICK",   Ammo = 1   },
                new WeaponDef { Name = "WEAPON_FLASHLIGHT",   Ammo = 1   },
                new WeaponDef { Name = "WEAPON_STUNGUN",      Ammo = 10  },
                new WeaponDef { Name = "WEAPON_PUMPSHOTGUN",  Ammo = 50  },
                new WeaponDef { Name = "WEAPON_PISTOL",       Ammo = 250, SetCurrent = true },
            }
        };

        [JsonProperty("senior")]
        public WeaponTierDef Senior { get; set; } = new()
        {
            Weapons = new[]
            {
                new WeaponDef { Name = "WEAPON_NIGHTSTICK",   Ammo = 1   },
                new WeaponDef { Name = "WEAPON_FLASHLIGHT",   Ammo = 1   },
                new WeaponDef { Name = "WEAPON_STUNGUN",      Ammo = 15  },
                new WeaponDef { Name = "WEAPON_PUMPSHOTGUN",  Ammo = 75  },
                new WeaponDef { Name = "WEAPON_CARBINERIFLE", Ammo = 200 },
                new WeaponDef { Name = "WEAPON_PISTOL",       Ammo = 500, SetCurrent = true },
            }
        };

        [JsonProperty("command")]
        public WeaponTierDef Command { get; set; } = new()
        {
            Weapons = new[]
            {
                new WeaponDef { Name = "WEAPON_NIGHTSTICK",   Ammo = 1   },
                new WeaponDef { Name = "WEAPON_FLASHLIGHT",   Ammo = 1   },
                new WeaponDef { Name = "WEAPON_STUNGUN",      Ammo = 15  },
                new WeaponDef { Name = "WEAPON_PUMPSHOTGUN",  Ammo = 75  },
                new WeaponDef { Name = "WEAPON_CARBINERIFLE", Ammo = 200 },
                new WeaponDef { Name = "WEAPON_PISTOL",       Ammo = 500, SetCurrent = true },
            }
        };
    }
}
