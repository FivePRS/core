using Newtonsoft.Json;

namespace FivePRS.Core.Config
{
    public sealed class WeaponDef
    {
        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("ammo")]
        public int Ammo { get; set; } = 0;

        [JsonProperty("setCurrent")]
        public bool SetCurrent { get; set; } = false;
    }

    public sealed class WeaponTierDef
    {
        [JsonProperty("weapons")]
        public WeaponDef[] Weapons { get; set; } = new WeaponDef[0];
    }

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
