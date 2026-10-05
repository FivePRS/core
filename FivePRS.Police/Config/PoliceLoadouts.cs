using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Client.Loadout;
using FivePRS.Core.Config;
using System.Linq;

namespace FivePRS.Police.Config
{
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
