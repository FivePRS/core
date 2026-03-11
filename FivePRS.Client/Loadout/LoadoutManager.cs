using System;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;

namespace FivePRS.Client.Loadout
{
    /// <summary>
    /// Applies and strips officer loadouts (weapons + uniform + props) on the local player ped.
    ///
    /// All operations target <see cref="Game.PlayerPed"/> directly; call only from the game thread
    /// (Tick or event handlers — the CitizenFX runtime guarantees this for us).
    /// </summary>
    public static class LoadoutManager
    {
        private static LoadoutDefinition? _current;

        public static async Task ApplyAsync(LoadoutDefinition loadout)
        {
            var ped = Game.PlayerPed;
            if (!ped.Exists()) return;

            API.RemoveAllPedWeapons(ped.Handle, true);
            await BaseScript.Delay(100);

            uint currentWeaponHash = 0;

            foreach (var entry in loadout.Weapons)
            {
                API.GiveWeaponToPed(ped.Handle, entry.Hash, entry.Ammo, false, false);
                if (entry.SetAsCurrent)
                    currentWeaponHash = entry.Hash;
            }

            if (currentWeaponHash != 0)
                API.SetCurrentPedWeapon(ped.Handle, currentWeaponHash, true);

            foreach (var comp in loadout.Components)
                API.SetPedComponentVariation(ped.Handle,
                    comp.ComponentId, comp.DrawableId, comp.TextureId, 0);

            foreach (var prop in loadout.Props)
            {
                if (prop.DrawableId < 0)
                    API.ClearPedProp(ped.Handle, prop.PropId);
                else
                    API.SetPedPropIndex(ped.Handle,
                        prop.PropId, prop.DrawableId, prop.TextureId, true);
            }

            _current = loadout;
            Debug.WriteLine($"[LoadoutManager] Applied loadout '{loadout.Name}'.");
        }

        public static void Strip()
        {
            var ped = Game.PlayerPed;
            if (!ped.Exists()) return;

            API.RemoveAllPedWeapons(ped.Handle, true);

            for (var i = 0; i <= 11; i++)
                API.SetPedComponentVariation(ped.Handle, i, 0, 0, 0);

            API.ClearPedProp(ped.Handle, 0);
            API.ClearPedProp(ped.Handle, 1);

            _current = null;
            Debug.WriteLine("[LoadoutManager] Loadout stripped.");
        }

        public static LoadoutDefinition? Current => _current;
    }
}
