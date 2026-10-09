using System;
using System.Collections.Generic;
using System.Linq;

namespace FivePRS.Core.Text
{
    public static class WeaponLabels
    {
        private static readonly Dictionary<string, string> Known = new(StringComparer.OrdinalIgnoreCase)
        {
            ["WEAPON_NIGHTSTICK"]       = "Nightstick",
            ["WEAPON_FLASHLIGHT"]       = "Flashlight",
            ["WEAPON_STUNGUN"]          = "Taser",
            ["WEAPON_PISTOL"]           = "Pistol",
            ["WEAPON_COMBATPISTOL"]     = "Combat Pistol",
            ["WEAPON_PISTOL_MK2"]       = "Pistol Mk II",
            ["WEAPON_PUMPSHOTGUN"]      = "Pump Shotgun",
            ["WEAPON_PUMPSHOTGUN_MK2"]  = "Pump Shotgun Mk II",
            ["WEAPON_CARBINERIFLE"]     = "Carbine Rifle",
            ["WEAPON_CARBINERIFLE_MK2"] = "Carbine Rifle Mk II",
            ["WEAPON_SMG"]              = "SMG",
            ["WEAPON_SNIPERRIFLE"]      = "Sniper Rifle",
            ["WEAPON_FLARE"]            = "Flare",
            ["WEAPON_FIREEXTINGUISHER"] = "Fire Extinguisher",
            ["WEAPON_HATCHET"]          = "Hatchet",
            ["WEAPON_CROWBAR"]          = "Crowbar",
        };

        public static string For(string weapon, string? label = null)
        {
            if (!string.IsNullOrWhiteSpace(label)) return label!.Trim();
            if (Known.TryGetValue(weapon, out var known)) return known;

            var name = weapon.StartsWith("WEAPON_", StringComparison.OrdinalIgnoreCase) ? weapon.Substring(7) : weapon;
            return string.Join(" ", name.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Length == 0 ? part : char.ToUpperInvariant(part[0]) + part.Substring(1).ToLowerInvariant()));
        }
    }
}
