using System.Collections.Generic;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;

namespace FivePRS.Client.Loadout
{
    public static class LoadoutManager
    {
        private const int ComponentSlots = 12;
        private static readonly int[] PropSlots = { 0, 1, 2, 6, 7 };

        private static List<ComponentEntry>? _savedComponents;
        private static List<PropEntry>?      _savedProps;

        public static LoadoutDefinition? Current { get; private set; }

        public static async Task ApplyAsync(LoadoutDefinition loadout)
        {
            var ped = Game.PlayerPed;
            if (!ped.Exists()) return;

            if (_savedComponents is null)
                SaveAppearance(ped.Handle);

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
            {
                if (API.IsPedComponentVariationValid(ped.Handle, comp.ComponentId, comp.DrawableId, comp.TextureId))
                    API.SetPedComponentVariation(ped.Handle, comp.ComponentId, comp.DrawableId, comp.TextureId, 0);
            }

            ApplyProps(ped.Handle, loadout.Props);

            Current = loadout;
            Debug.WriteLine($"[LoadoutManager] Applied loadout '{loadout.Name}'.");
        }

        public static void Strip()
        {
            var ped = Game.PlayerPed;
            if (!ped.Exists()) return;

            API.RemoveAllPedWeapons(ped.Handle, true);

            if (_savedComponents is not null)
            {
                foreach (var comp in _savedComponents)
                    API.SetPedComponentVariation(ped.Handle, comp.ComponentId, comp.DrawableId, comp.TextureId, 0);
            }

            if (_savedProps is not null)
                ApplyProps(ped.Handle, _savedProps);

            _savedComponents = null;
            _savedProps      = null;
            Current          = null;
            Debug.WriteLine("[LoadoutManager] Loadout stripped.");
        }

        private static void SaveAppearance(int ped)
        {
            _savedComponents = new List<ComponentEntry>();
            for (var slot = 0; slot < ComponentSlots; slot++)
            {
                _savedComponents.Add(new ComponentEntry
                {
                    ComponentId = slot,
                    DrawableId  = API.GetPedDrawableVariation(ped, slot),
                    TextureId   = API.GetPedTextureVariation(ped, slot),
                });
            }

            _savedProps = new List<PropEntry>();
            foreach (var slot in PropSlots)
            {
                _savedProps.Add(new PropEntry
                {
                    PropId     = slot,
                    DrawableId = API.GetPedPropIndex(ped, slot),
                    TextureId  = API.GetPedPropTextureIndex(ped, slot),
                });
            }
        }

        private static void ApplyProps(int ped, IEnumerable<PropEntry> props)
        {
            foreach (var prop in props)
            {
                if (prop.DrawableId < 0)
                    API.ClearPedProp(ped, prop.PropId);
                else
                    API.SetPedPropIndex(ped, prop.PropId, prop.DrawableId, prop.TextureId, true);
            }
        }
    }
}
