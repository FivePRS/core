using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Client.Loadout;
using FivePRS.Core.Civilian;
using FivePRS.Core.Config;
using FivePRS.Core.Events;
using Newtonsoft.Json;

namespace FivePRS.Client.Appearance
{
    public class AppearanceManager : BaseScript
    {
        private static string _currentJson = string.Empty;

        public static CharacterAppearance? Current { get; private set; }

        public static int CharacterId { get; private set; }

        public AppearanceManager()
        {
            EventHandlers[EventNames.ClientAppearance] += new Action<int, string>(OnAppearance);
            EventHandlers["playerSpawned"]             += new Action(OnSpawned);
        }

        public static IEnumerable<string> AllowedStandardModels => ConfigManager.Settings.Creator.AllowedStandardModels;

        private static async void OnAppearance(int characterId, string json)
        {
            CharacterId = characterId;
            if (json == _currentJson) return;

            _currentJson = json;
            Current = json.Length == 0
                ? null
                : AppearanceRules.Sanitize(JsonConvert.DeserializeObject<CharacterAppearance>(json), AllowedStandardModels);

            if (Current is not null && !LoadoutManager.IsInUniform)
                await ApplyAsync(Current);
        }

        private static async void OnSpawned()
        {
            if (Current is not null && !LoadoutManager.IsInUniform)
                await ApplyAsync(Current);
        }

        public static Task ApplyCurrentAsync() => Current is null ? Task.FromResult(0) : ApplyAsync(Current);

        public static async Task ApplyAsync(CharacterAppearance appearance)
        {
            await SetModelAsync(appearance.Model);
            var ped = Game.PlayerPed.Handle;

            if (appearance.IsFreemode)
            {
                var blend = appearance.HeadBlend;
                API.SetPedHeadBlendData(ped, blend.ShapeFirst, blend.ShapeSecond, 0, blend.SkinFirst, blend.SkinSecond, 0,
                    blend.ShapeMix, blend.SkinMix, 0f, false);

                for (var i = 0; i < appearance.FaceFeatures.Count; i++)
                    API.SetPedFaceFeature(ped, i, appearance.FaceFeatures[i]);

                foreach (var overlay in appearance.Overlays)
                    ApplyOverlay(ped, overlay);

                API.SetPedEyeColor(ped, appearance.EyeColor);
            }

            foreach (var item in appearance.Components)
            {
                if (API.IsPedComponentVariationValid(ped, item.Slot, item.Drawable, item.Texture))
                    API.SetPedComponentVariation(ped, item.Slot, item.Drawable, item.Texture, 0);
            }

            foreach (var prop in appearance.Props)
            {
                if (prop.Drawable < 0)
                    API.ClearPedProp(ped, prop.Slot);
                else
                    API.SetPedPropIndex(ped, prop.Slot, prop.Drawable, Math.Max(0, prop.Texture), true);
            }

            if (appearance.IsFreemode)
                API.SetPedHairColor(ped, appearance.HairColor, appearance.HairHighlight);
        }

        public static void ApplyOverlay(int ped, HeadOverlay overlay)
        {
            API.SetPedHeadOverlay(ped, overlay.Index, overlay.Style < 0 ? 255 : overlay.Style, overlay.Opacity);

            var colorType = OverlayColorType(overlay.Index);
            if (colorType != 0)
                API.SetPedHeadOverlayColor(ped, overlay.Index, colorType, overlay.Color, overlay.SecondColor);
        }

        public static int OverlayColorType(int index) => index switch
        {
            1 or 2 or 10 => 1,
            5 or 8       => 2,
            _            => 0,
        };

        public static async Task<bool> SetModelAsync(string modelName)
        {
            var hash = (uint)API.GetHashKey(modelName);
            if (API.GetEntityModel(Game.PlayerPed.Handle) == (int)hash) return true;

            var model = new Model((int)hash);
            if (!model.IsValid || !model.IsPed) return false;

            var changed = await Game.Player.ChangeModel(model);
            model.MarkAsNoLongerNeeded();
            if (changed) API.SetPedDefaultComponentVariation(Game.PlayerPed.Handle);
            return changed;
        }

        public static CharacterAppearance Capture()
        {
            var ped   = Game.PlayerPed.Handle;
            var model = API.GetEntityModel(ped);
            var name  = new[] { AppearanceRules.MaleFreemode, AppearanceRules.FemaleFreemode }
                .Concat(AllowedStandardModels)
                .FirstOrDefault(m => API.GetHashKey(m) == model);

            var appearance = new CharacterAppearance { Model = name ?? AppearanceRules.MaleFreemode };
            if (name is null) return appearance;

            appearance.FaceFeatures = Enumerable.Repeat(0f, AppearanceRules.FaceFeatureCount).ToList();
            appearance.Components = Enumerable.Range(0, AppearanceRules.ComponentCount)
                .Select(slot => new ClothingItem
                {
                    Slot     = slot,
                    Drawable = API.GetPedDrawableVariation(ped, slot),
                    Texture  = API.GetPedTextureVariation(ped, slot),
                })
                .ToList();
            appearance.Props = AppearanceRules.PropSlots
                .Select(slot => new ClothingItem
                {
                    Slot     = slot,
                    Drawable = API.GetPedPropIndex(ped, slot),
                    Texture  = API.GetPedPropTextureIndex(ped, slot),
                })
                .ToList();
            appearance.HairColor = Math.Max(0, API.GetPedHairColor(ped));
            appearance.HairHighlight = Math.Max(0, API.GetPedHairHighlightColor(ped));
            return appearance;
        }
    }
}
