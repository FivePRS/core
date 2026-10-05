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
        private static int                   _savedModel;

        public static LoadoutDefinition? Current { get; private set; }

        public static async Task ApplyAsync(LoadoutDefinition loadout)
        {
            var ped = Game.PlayerPed;
            if (!ped.Exists()) return;

            if (_savedComponents is null)
                SaveAppearance(ped.Handle);

            var pedModel = API.IsPedMale(ped.Handle) ? loadout.MalePedModel : loadout.FemalePedModel;
            if (pedModel is not null && await ChangeModelAsync(new Model(pedModel)))
            {
                ped = Game.PlayerPed;
                API.SetPedDefaultComponentVariation(ped.Handle);
            }

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

            ApplyComponents(ped.Handle, loadout.Components);
            ApplyProps(ped.Handle, loadout.Props);

            Current = loadout;
            Debug.WriteLine($"[LoadoutManager] Applied loadout '{loadout.Name}'.");
        }

        public static async Task StripAsync()
        {
            var ped = Game.PlayerPed;
            if (!ped.Exists()) return;

            API.RemoveAllPedWeapons(ped.Handle, true);

            if (_savedModel != 0 && API.GetEntityModel(ped.Handle) != _savedModel &&
                await ChangeModelAsync(new Model(_savedModel)))
            {
                ped = Game.PlayerPed;
            }

            if (_savedComponents is not null)
                ApplyComponents(ped.Handle, _savedComponents);

            if (_savedProps is not null)
                ApplyProps(ped.Handle, _savedProps);

            _savedComponents = null;
            _savedProps      = null;
            _savedModel      = 0;
            Current          = null;
            Debug.WriteLine("[LoadoutManager] Loadout stripped.");
        }

        private static async Task<bool> ChangeModelAsync(Model model)
        {
            if (!model.IsValid || !model.IsPed)
            {
                Debug.WriteLine($"[LoadoutManager] Ped model {model.Hash} is not a valid ped.");
                return false;
            }

            var changed = await Game.Player.ChangeModel(model);
            model.MarkAsNoLongerNeeded();

            if (!changed)
                Debug.WriteLine($"[LoadoutManager] Failed to change player model to {model.Hash}.");
            return changed;
        }

        private static void SaveAppearance(int ped)
        {
            _savedModel = API.GetEntityModel(ped);

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

        private static void ApplyComponents(int ped, IEnumerable<ComponentEntry> components)
        {
            foreach (var comp in components)
            {
                if (API.IsPedComponentVariationValid(ped, comp.ComponentId, comp.DrawableId, comp.TextureId))
                    API.SetPedComponentVariation(ped, comp.ComponentId, comp.DrawableId, comp.TextureId, 0);
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
