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
            var outfit  = ToOutfit(cfg.UniformFor(agencyId, tier));

            outfit.Name    = TierName(tier);
            outfit.Weapons = cfg.WeaponsFor(agencyId, tier).Weapons.Select(ToEntry).ToArray();
            return outfit;
        }

        public static WeaponEntry ToEntry(WeaponDef weapon) => new()
        {
            Hash         = (uint)API.GetHashKey(weapon.Name),
            Ammo         = weapon.Ammo,
            SetAsCurrent = weapon.SetCurrent,
        };

        public static LoadoutDefinition ToOutfit(UniformDef uniform) => new()
        {
            Components = uniform.Components
                .Select(c => new ComponentEntry { ComponentId = c.Slot, DrawableId = c.Drawable, TextureId = c.Texture })
                .ToArray(),
            Props = uniform.Props
                .Select(p => new PropEntry { PropId = p.Slot, DrawableId = p.Drawable, TextureId = p.Texture })
                .ToArray(),
            MalePedModel   = NullIfEmpty(uniform.PedModels?.Male),
            FemalePedModel = NullIfEmpty(uniform.PedModels?.Female),
        };

        public static string TierName(LoadoutTier tier) => tier switch
        {
            LoadoutTier.Command => "Command",
            LoadoutTier.Senior  => "Senior Officer",
            LoadoutTier.Officer => "Officer",
            _                   => "Recruit",
        };

        private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
