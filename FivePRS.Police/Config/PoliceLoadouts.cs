using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Client.Loadout;
using FivePRS.Core.Config;
using System.Linq;

namespace FivePRS.Police.Config
{
    /// <summary>
    /// Rank-based loadout definitions for the Police department.
    ///
    /// Component values target the default FiveM freemode ped (mp_m_freemode_01 / mp_f_freemode_01).
    /// If your server uses custom ped models, update the drawable/texture IDs to match.
    ///
    /// Weapon names are loaded from config/police_loadouts.json (e.g. "WEAPON_PISTOL").
    /// They are resolved to hashes at runtime via API.GetHashKey().
    ///
    /// GTA V component slot reference:
    ///   3=Torso  4=Legs  6=Feet  8=Undershirt  9=BodyArmor  11=Jacket/Torso2
    /// GTA V prop slot reference:
    ///   0=Hat  1=Glasses  2=EarPiece
    /// </summary>
    public static class PoliceLoadouts
    {
        public static LoadoutDefinition GetForRank(int rank)
        {
            var cfg  = ConfigManager.PoliceLoadouts;
            var tier = rank >= 8 ? cfg.Command  :
                       rank >= 5 ? cfg.Senior   :
                       rank >= 3 ? cfg.Officer  : cfg.Recruit;
            var name = rank >= 8 ? "Command"        :
                       rank >= 5 ? "Senior Officer" :
                       rank >= 3 ? "Officer"        : "Recruit";

            return new LoadoutDefinition
            {
                Name    = name,
                Weapons = tier.Weapons.Select(w => new WeaponEntry
                {
                    Hash         = (uint)API.GetHashKey(w.Name),
                    Ammo         = w.Ammo,
                    SetAsCurrent = w.SetCurrent,
                }).ToArray(),
                Components = rank >= 8 ? LspdCommandComponents : LspdUniformComponents,
                Props      = rank >= 8 ? LspdCommandProps      : LspdUniformProps,
            };
        }

        private static readonly ComponentEntry[] LspdUniformComponents = new[]
        {
            new ComponentEntry { ComponentId = 3,  DrawableId = 4,  TextureId = 0 },
            new ComponentEntry { ComponentId = 4,  DrawableId = 24, TextureId = 0 },
            new ComponentEntry { ComponentId = 6,  DrawableId = 24, TextureId = 0 },
            new ComponentEntry { ComponentId = 8,  DrawableId = 58, TextureId = 0 },
            new ComponentEntry { ComponentId = 9,  DrawableId = 0,  TextureId = 0 },
            new ComponentEntry { ComponentId = 11, DrawableId = 55, TextureId = 0 },
        };

        private static readonly PropEntry[] LspdUniformProps = new[]
        {
            new PropEntry { PropId = 0, DrawableId = 46, TextureId = 0  },
            new PropEntry { PropId = 1, DrawableId = -1, TextureId = -1 },
        };

        private static readonly ComponentEntry[] LspdCommandComponents = new[]
        {
            new ComponentEntry { ComponentId = 3,  DrawableId = 4,  TextureId = 0 },
            new ComponentEntry { ComponentId = 4,  DrawableId = 24, TextureId = 0 },
            new ComponentEntry { ComponentId = 6,  DrawableId = 24, TextureId = 0 },
            new ComponentEntry { ComponentId = 8,  DrawableId = 58, TextureId = 0 },
            new ComponentEntry { ComponentId = 9,  DrawableId = 0,  TextureId = 0 },
            new ComponentEntry { ComponentId = 11, DrawableId = 48, TextureId = 0 },
        };

        private static readonly PropEntry[] LspdCommandProps = new[]
        {
            new PropEntry { PropId = 0, DrawableId = -1, TextureId = -1 },
            new PropEntry { PropId = 1, DrawableId = -1, TextureId = -1 },
        };
    }
}
