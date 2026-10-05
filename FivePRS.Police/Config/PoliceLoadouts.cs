using System.Linq;
using CitizenFX.Core.Native;
using FivePRS.Client.Loadout;
using FivePRS.Core.Config;

namespace FivePRS.Police.Config
{
    public static class PoliceLoadouts
    {
        public static LoadoutDefinition GetForRank(int rank, string? agencyId)
        {
            var cfg     = ConfigManager.PoliceLoadouts;
            var tier    = PoliceLoadoutsConfig.TierForRank(rank);
            var weapons = cfg.WeaponsFor(agencyId, tier);
            var uniform = cfg.UniformFor(agencyId, tier);

            return new LoadoutDefinition
            {
                Name    = TierName(tier),
                Weapons = weapons.Weapons.Select(w => new WeaponEntry
                {
                    Hash         = (uint)API.GetHashKey(w.Name),
                    Ammo         = w.Ammo,
                    SetAsCurrent = w.SetCurrent,
                }).ToArray(),
                Components = uniform.Components
                    .Select(c => new ComponentEntry { ComponentId = c.Slot, DrawableId = c.Drawable, TextureId = c.Texture })
                    .ToArray(),
                Props = uniform.Props
                    .Select(p => new PropEntry { PropId = p.Slot, DrawableId = p.Drawable, TextureId = p.Texture })
                    .ToArray(),
                MalePedModel   = NullIfEmpty(uniform.PedModels?.Male),
                FemalePedModel = NullIfEmpty(uniform.PedModels?.Female),
            };
        }

        private static string TierName(LoadoutTier tier) => tier switch
        {
            LoadoutTier.Command => "Command",
            LoadoutTier.Senior  => "Senior Officer",
            LoadoutTier.Officer => "Officer",
            _                   => "Recruit",
        };

        private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
