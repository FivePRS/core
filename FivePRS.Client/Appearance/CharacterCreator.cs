using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Client.App;
using FivePRS.Core.Civilian;
using FivePRS.Core.Config;
using FivePRS.Core.Events;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace FivePRS.Client.Appearance
{
    public class CharacterCreator : BaseScript
    {
        private const int CameraBlendMs = 500;

        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
        };

        private static readonly string[] ParentNames =
        {
            "Benjamin", "Daniel", "Joshua", "Noah", "Andrew", "Juan", "Alex", "Isaac", "Evan", "Ethan", "Vincent",
            "Angel", "Diego", "Adrian", "Gabriel", "Michael", "Santiago", "Kevin", "Louis", "Samuel", "Anthony",
            "Hannah", "Audrey", "Jasmine", "Giselle", "Amelia", "Isabella", "Zoe", "Ava", "Camila", "Violet",
            "Sophia", "Evelyn", "Nicole", "Ashley", "Grace", "Brianna", "Natalie", "Olivia", "Elizabeth", "Charlotte",
            "Emma", "Claude", "Niko", "John", "Misty",
        };

        private static readonly string[] FaceFeatureNames =
        {
            "Nose width", "Nose height", "Nose length", "Nose bridge", "Nose tip", "Nose bridge shift",
            "Brow height", "Brow depth", "Cheekbone height", "Cheekbone width", "Cheek width", "Eye opening",
            "Lip thickness", "Jaw width", "Jaw length", "Chin height", "Chin length", "Chin width", "Chin cleft",
            "Neck thickness",
        };

        private static readonly string[] OverlayNames =
        {
            "Blemishes", "Facial hair", "Eyebrows", "Ageing", "Makeup", "Blush", "Complexion", "Sun damage",
            "Lipstick", "Freckles", "Chest hair", "Body blemishes", "More body blemishes",
        };

        private static readonly string[] ComponentNames =
        {
            "Face", "Mask", "Hair", "Arms", "Legs", "Bag", "Shoes", "Accessories", "Undershirt", "Body armour", "Decals", "Top",
        };

        private static readonly Dictionary<int, string> PropNames = new()
        {
            [0] = "Hat", [1] = "Glasses", [2] = "Earrings", [6] = "Watch", [7] = "Bracelet",
        };

        private static bool _open;
        private static bool _busy;
        private static string? _pendingJson;
        private static int _characterId;
        private static string _characterName = string.Empty;
        private static CharacterAppearance _working = new();
        private static CharacterAppearance? _original;
        private static int _originalModel;
        private static List<ClothingItem> _originalComponents = new();
        private static List<ClothingItem> _originalProps = new();
        private static int _camera;
        private static Vector3 _anchor;
        private static Vector3 _forward;
        private static Vector3 _right;
        private static float _heading;
        private static string _focus = "body";
        private static CharacterCreator? _instance;

        public static bool IsOpen => _open;

        public CharacterCreator()
        {
            _instance = this;

            RegisterCallback("creatorUpdate", data => _ = UpdateAsync(data));
            RegisterCallback("creatorCamera", data => SetFocus(NuiData.GetString(data, "focus")));
            RegisterCallback("creatorRotate", data => Rotate(data));
            RegisterCallback("creatorSave",   data => _ = SaveAsync());
            RegisterCallback("creatorCancel", data => _ = CancelAsync());

            EventHandlers["onClientResourceStop"] += new Action<string>(name =>
            {
                if (name == API.GetCurrentResourceName() && _open) Teardown();
            });
        }

        public static async void Open(int characterId, string characterName)
        {
            if (_open || _instance is null) return;

            if (ClientBrain.LocalPlayerData.IsOnDuty)
            {
                ClientBrain.ShowNotification("Go off duty before changing your appearance.", "Appearance");
                return;
            }

            _open          = true;
            _characterId   = characterId;
            _characterName = characterName;
            _original      = AppearanceManager.Current;
            SnapshotPed();

            _working = Clone(_original ?? AppearanceManager.Capture());
            await AppearanceManager.ApplyAsync(_working);
            if (_working.Components.Count == 0)
            {
                _working = Defaults(_working.Model);
                await AppearanceManager.ApplyAsync(_working);
            }

            var ped = Game.PlayerPed;
            _anchor  = ped.Position;
            _forward = ped.ForwardVector;
            _right   = ped.RightVector;
            _heading = ped.Heading;
            _focus   = "body";

            PreparePed();
            _camera = API.CreateCam("DEFAULT_SCRIPTED_CAMERA", true);
            PositionCamera();
            API.RenderScriptCams(true, true, CameraBlendMs, true, true);
            API.DisplayRadar(false);
            API.DisplayHud(false);

            NuiFocus.Take();
            _instance.Tick += _instance.HoldFocusAsync;
            Send("open", new { Name = _characterName, Appearance = _working, Options = BuildOptions() });
        }

        private async Task HoldFocusAsync()
        {
            if (_open) NuiFocus.EnsureTaken();
            await Delay(250);
        }

        private static async Task UpdateAsync(IDictionary<string, object> data)
        {
            if (!_open) return;

            _pendingJson = NuiData.GetString(data, "appearance");
            if (_busy) return;

            _busy = true;
            try
            {
                while (_pendingJson is not null && _open)
                {
                    var json = _pendingJson;
                    _pendingJson = null;
                    await ApplyUpdateAsync(json);
                }
            }
            finally
            {
                _busy = false;
            }
        }

        private static async Task ApplyUpdateAsync(string json)
        {
            CharacterAppearance? next;
            try
            {
                next = AppearanceRules.Sanitize(JsonConvert.DeserializeObject<CharacterAppearance>(json), AppearanceManager.AllowedStandardModels);
            }
            catch (JsonException)
            {
                return;
            }

            if (next is null) return;

            if (!string.Equals(next.Model, _working.Model, StringComparison.OrdinalIgnoreCase))
            {
                if (!await AppearanceManager.SetModelAsync(next.Model)) return;

                _working = Defaults(next.Model);
                await AppearanceManager.ApplyAsync(_working);
                PreparePed();
                Send("reset", new { Appearance = _working, Options = BuildOptions() });
                return;
            }

            _working = next;
            await AppearanceManager.ApplyAsync(_working);
            Send("options", BuildOptions());
        }

        private static CharacterAppearance Defaults(string model)
        {
            var appearance = AppearanceManager.Capture();
            appearance.Model = model;

            if (appearance.IsFreemode)
            {
                var parent = model == AppearanceRules.FemaleFreemode ? 21 : 0;
                appearance.HeadBlend = new HeadBlend { ShapeFirst = parent, ShapeSecond = parent, SkinFirst = parent, SkinSecond = parent };
                appearance.FaceFeatures = Enumerable.Repeat(0f, AppearanceRules.FaceFeatureCount).ToList();
            }

            return appearance;
        }

        private static void Rotate(IDictionary<string, object> data)
        {
            if (!_open || !NuiData.TryGetInt(data, "delta", out var delta)) return;

            _heading = (_heading + delta + 360f) % 360f;
            API.SetEntityHeading(Game.PlayerPed.Handle, _heading);
        }

        private static void SetFocus(string focus)
        {
            if (!_open) return;

            _focus = focus is "face" or "legs" ? focus : "body";
            PositionCamera();
        }

        private static void PositionCamera()
        {
            float distance, height, fov;
            switch (_focus)
            {
                case "face":
                    distance = 0.9f; height = 0.65f; fov = 30f;
                    break;
                case "legs":
                    distance = 1.6f; height = -0.45f; fov = 45f;
                    break;
                default:
                    distance = 2.6f; height = 0.15f; fov = 45f;
                    break;
            }

            var target = _anchor + new Vector3(0f, 0f, height) + _right * (distance * 0.3f);
            var position = _anchor + _forward * distance + new Vector3(0f, 0f, height);

            API.SetCamCoord(_camera, position.X, position.Y, position.Z);
            API.PointCamAtCoord(_camera, target.X, target.Y, target.Z);
            API.SetCamFov(_camera, fov);
        }

        private static async Task SaveAsync()
        {
            while (_open && (_busy || _pendingJson is not null))
                await Delay(0);

            if (!_open) return;

            ClientEvents.TriggerServer(EventNames.ServerAppearanceSave, _characterId, JsonConvert.SerializeObject(_working));
            ClientBrain.ShowNotification($"Saved {_characterName}'s appearance.", "Appearance");
            Teardown();
        }

        private static async Task CancelAsync()
        {
            _pendingJson = null;
            while (_open && _busy)
                await Delay(0);

            if (!_open) return;

            _busy = true;
            try
            {
                if (_original is not null)
                    await AppearanceManager.ApplyAsync(_original);
                else
                    await RestoreSnapshotAsync();
            }
            finally
            {
                _busy = false;
                Teardown();
            }
        }

        private static void Teardown()
        {
            _open = false;
            if (_instance is not null) _instance.Tick -= _instance.HoldFocusAsync;

            API.RenderScriptCams(false, true, CameraBlendMs, true, true);
            if (_camera != 0) API.DestroyCam(_camera, false);
            _camera = 0;

            var ped = Game.PlayerPed.Handle;
            API.FreezeEntityPosition(ped, false);
            API.SetEntityInvincible(ped, false);
            API.DisplayRadar(true);
            API.DisplayHud(true);

            NuiFocus.Release();
            Send("close", null);
        }

        private static void PreparePed()
        {
            var ped = Game.PlayerPed.Handle;
            API.ClearPedTasksImmediately(ped);
            API.SetEntityHeading(ped, _heading);
            API.FreezeEntityPosition(ped, true);
            API.SetEntityInvincible(ped, true);
        }

        private static void SnapshotPed()
        {
            var ped = Game.PlayerPed.Handle;
            _originalModel = API.GetEntityModel(ped);
            _originalComponents = Enumerable.Range(0, AppearanceRules.ComponentCount)
                .Select(slot => new ClothingItem { Slot = slot, Drawable = API.GetPedDrawableVariation(ped, slot), Texture = API.GetPedTextureVariation(ped, slot) })
                .ToList();
            _originalProps = AppearanceRules.PropSlots
                .Select(slot => new ClothingItem { Slot = slot, Drawable = API.GetPedPropIndex(ped, slot), Texture = API.GetPedPropTextureIndex(ped, slot) })
                .ToList();
        }

        private static async Task RestoreSnapshotAsync()
        {
            if (API.GetEntityModel(Game.PlayerPed.Handle) != _originalModel)
            {
                var model = new Model(_originalModel);
                if (await Game.Player.ChangeModel(model)) model.MarkAsNoLongerNeeded();
            }

            var ped = Game.PlayerPed.Handle;
            foreach (var item in _originalComponents)
                API.SetPedComponentVariation(ped, item.Slot, item.Drawable, item.Texture, 0);
            foreach (var prop in _originalProps)
            {
                if (prop.Drawable < 0) API.ClearPedProp(ped, prop.Slot);
                else API.SetPedPropIndex(ped, prop.Slot, prop.Drawable, prop.Texture, true);
            }
        }

        private static object BuildOptions()
        {
            var ped = Game.PlayerPed.Handle;
            var models = new List<PedOption>
            {
                new() { Model = AppearanceRules.MaleFreemode, Label = "Male" },
                new() { Model = AppearanceRules.FemaleFreemode, Label = "Female" },
            };
            if (ConfigManager.Settings.Creator.AllowStandardPeds)
                models.AddRange(ConfigManager.Settings.Creator.StandardPeds);

            return new
            {
                Models       = models,
                Freemode     = _working.IsFreemode,
                Parents      = ParentNames,
                FaceFeatures = FaceFeatureNames,
                Overlays     = OverlayNames.Select((name, index) => new
                {
                    Index     = index,
                    Name      = name,
                    Count     = API.GetPedHeadOverlayNum(index),
                    ColorType = AppearanceManager.OverlayColorType(index),
                }),
                HairColors = Math.Max(1, API.GetNumHairColors()),
                EyeColors  = AppearanceRules.MaxEyeColor + 1,
                Components = Enumerable.Range(0, AppearanceRules.ComponentCount).Select(slot => new
                {
                    Slot      = slot,
                    Name      = ComponentNames[slot],
                    Drawables = API.GetNumberOfPedDrawableVariations(ped, slot),
                    Textures  = API.GetNumberOfPedTextureVariations(ped, slot, API.GetPedDrawableVariation(ped, slot)),
                }),
                Props = AppearanceRules.PropSlots.Select(slot => new
                {
                    Slot      = slot,
                    Name      = PropNames[slot],
                    Drawables = API.GetNumberOfPedPropDrawableVariations(ped, slot),
                    Textures  = API.GetPedPropIndex(ped, slot) < 0 ? 0 : API.GetNumberOfPedPropTextureVariations(ped, slot, API.GetPedPropIndex(ped, slot)),
                }),
            };
        }

        private static CharacterAppearance Clone(CharacterAppearance appearance) =>
            JsonConvert.DeserializeObject<CharacterAppearance>(JsonConvert.SerializeObject(appearance))!;

        private static void Send(string type, object? payload) =>
            API.SendNuiMessage(JsonConvert.SerializeObject(new { screen = "creator", type, payload }, JsonSettings));

        private void RegisterCallback(string name, Action<IDictionary<string, object>> handler)
        {
            API.RegisterNuiCallbackType(name);
            EventHandlers[$"__cfx_nui:{name}"] += new Action<IDictionary<string, object>, CallbackDelegate>((data, callback) =>
            {
                try
                {
                    handler(data);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CharacterCreator] NUI callback '{name}' failed: {ex.Message}");
                }

                callback("ok");
            });
        }
    }
}
