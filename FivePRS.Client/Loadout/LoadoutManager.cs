using System.Collections.Generic;
using System.Linq;
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
        private static readonly List<WeaponEntry> Issued = new();

        private const int FullArmour = 100;

        public static LoadoutDefinition? Current { get; private set; }

        public static bool IsInUniform => _savedComponents is not null;

        public static async Task ApplyAsync(LoadoutDefinition loadout)
        {
            if (!Game.PlayerPed.Exists()) return;

            await WearAsync(loadout);
            await GiveWeaponsAsync(loadout.Weapons, true);

            Current = loadout;
            Debug.WriteLine($"[LoadoutManager] Applied loadout '{loadout.Name}'.");
        }

        public static async Task WearAsync(LoadoutDefinition outfit)
        {
            var ped = Game.PlayerPed;
            if (!ped.Exists()) return;

            if (_savedComponents is null)
                SaveAppearance(ped.Handle);

            var pedModel = API.IsPedMale(ped.Handle) ? outfit.MalePedModel : outfit.FemalePedModel;
            var target   = pedModel is not null ? API.GetHashKey(pedModel) : _savedModel;

            if (target != 0 && API.GetEntityModel(ped.Handle) != target && await ChangeModelAsync(new Model(target)))
            {
                ped = Game.PlayerPed;
                API.SetPedDefaultComponentVariation(ped.Handle);
                if (pedModel is null) RestoreSavedClothing(ped.Handle);
                ReissueWeapons(ped.Handle);
            }

            ApplyComponents(ped.Handle, outfit.Components);
            ApplyProps(ped.Handle, outfit.Props);
        }

        public static async Task RestoreClothingAsync()
        {
            var ped = Game.PlayerPed;
            if (!ped.Exists() || _savedComponents is null) return;

            if (_savedModel != 0 && API.GetEntityModel(ped.Handle) != _savedModel &&
                await ChangeModelAsync(new Model(_savedModel)))
            {
                ped = Game.PlayerPed;
                ReissueWeapons(ped.Handle);
            }

            RestoreSavedClothing(ped.Handle);
            ClearSavedAppearance();
        }

        public static async Task GiveWeaponsAsync(IEnumerable<WeaponEntry> weapons, bool replace)
        {
            var ped = Game.PlayerPed.Handle;

            if (replace)
            {
                RemoveWeapons();
                await BaseScript.Delay(100);
            }

            uint current = 0;
            foreach (var entry in weapons)
            {
                GiveWeapon(ped, entry);
                if (entry.SetAsCurrent) current = entry.Hash;
            }

            if (current != 0)
                API.SetCurrentPedWeapon(ped, current, true);
        }

        public static void GiveWeapon(WeaponEntry entry) => GiveWeapon(Game.PlayerPed.Handle, entry);

        public static bool HasWeapon(uint hash) => API.HasPedGotWeapon(Game.PlayerPed.Handle, hash, false);

        public static int RefillAmmo()
        {
            var ped     = Game.PlayerPed.Handle;
            var refills = 0;

            foreach (var entry in Issued.Where(e => API.HasPedGotWeapon(ped, e.Hash, false)))
            {
                if (API.GetAmmoInPedWeapon(ped, entry.Hash) >= entry.Ammo) continue;

                API.SetPedAmmo(ped, entry.Hash, entry.Ammo);
                refills++;
            }

            return refills;
        }

        public static void GiveArmour() => API.SetPedArmour(Game.PlayerPed.Handle, FullArmour);

        public static void RemoveWeapons()
        {
            API.RemoveAllPedWeapons(Game.PlayerPed.Handle, true);
            Issued.Clear();
        }

        private static void GiveWeapon(int ped, WeaponEntry entry)
        {
            if (API.HasPedGotWeapon(ped, entry.Hash, false))
                API.SetPedAmmo(ped, entry.Hash, System.Math.Max(entry.Ammo, API.GetAmmoInPedWeapon(ped, entry.Hash)));
            else
                API.GiveWeaponToPed(ped, entry.Hash, entry.Ammo, false, false);

            Issued.RemoveAll(e => e.Hash == entry.Hash);
            Issued.Add(entry);
        }

        private static void ReissueWeapons(int ped)
        {
            foreach (var entry in Issued)
                API.GiveWeaponToPed(ped, entry.Hash, entry.Ammo, false, false);
        }

        public static async Task StripAsync()
        {
            var ped = Game.PlayerPed;
            if (!ped.Exists()) return;

            RemoveWeapons();
            await RestoreClothingAsync();

            Current = null;
            Debug.WriteLine("[LoadoutManager] Loadout stripped.");
        }

        private static void RestoreSavedClothing(int ped)
        {
            if (_savedComponents is not null) ApplyComponents(ped, _savedComponents);
            if (_savedProps is not null) ApplyProps(ped, _savedProps);
        }

        private static void ClearSavedAppearance()
        {
            _savedComponents = null;
            _savedProps      = null;
            _savedModel      = 0;
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
