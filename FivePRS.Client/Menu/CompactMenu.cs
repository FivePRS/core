using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace FivePRS.Client.Menu
{
    public sealed class MenuItem
    {
        public string Label { get; set; } = string.Empty;

        public string? Detail { get; set; }

        public bool Disabled { get; set; }

        public Func<Task>? Select { get; set; }

        public Func<MenuPage>? Submenu { get; set; }
    }

    public sealed class MenuPage
    {
        public string Title { get; set; } = string.Empty;

        public string? Subtitle { get; set; }

        public List<MenuItem> Items { get; set; } = new();
    }

    public class CompactMenu : BaseScript
    {
        private const int ControlUp = 172;
        private const int ControlDown = 173;
        private const int ControlSelect = 191;
        private const int ControlBack = 177;
        private const int ControlScrollUp = 15;
        private const int ControlScrollDown = 14;

        private const string SoundSet = "HUD_FRONTEND_DEFAULT_SOUNDSET";

        private static readonly int[] BlockedControls =
        {
            14, 15, 16, 17, 24, 25, 27, 37, 140, 141, 142, 172, 173, 174, 175, 176, 177,
            191, 199, 200, 201, 202, 241, 242, 257, 263,
        };

        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
        };

        private static readonly List<Func<MenuPage>> Pages = new();
        private static readonly List<int> Indices = new();

        private static MenuPage? _page;
        private static Func<bool>? _keepOpen;
        private static string? _icon;
        private static bool _busy;

        public static bool IsOpen => _page is not null;

        public CompactMenu()
        {
            EventHandlers["onClientResourceStop"] += new Action<string>(name =>
            {
                if (name == API.GetCurrentResourceName()) Close();
            });

            Tick += OnTick;
        }

        public static void Open(Func<MenuPage> root, Func<bool>? keepOpen = null, string? icon = null)
        {
            Pages.Clear();
            Indices.Clear();
            _keepOpen = keepOpen;
            _icon     = icon;
            Push(root);
        }

        public static void Close()
        {
            if (_page is null) return;

            Pages.Clear();
            Indices.Clear();
            _page     = null;
            _keepOpen = null;
            _icon     = null;
            Send("hide", null);
        }

        public static void Refresh()
        {
            if (Pages.Count == 0) return;

            _page = Pages[Pages.Count - 1]();
            Indices[Indices.Count - 1] = Clamp(Indices[Indices.Count - 1], _page.Items.Count);
            Render();
        }

        private static void Push(Func<MenuPage> page)
        {
            Pages.Add(page);
            Indices.Add(0);
            _page = page();
            Indices[Indices.Count - 1] = FirstEnabled(_page);
            Render();
        }

        private static void Pop()
        {
            if (Pages.Count <= 1)
            {
                Close();
                return;
            }

            Pages.RemoveAt(Pages.Count - 1);
            Indices.RemoveAt(Indices.Count - 1);
            Refresh();
        }

        private async Task OnTick()
        {
            if (_page is null)
            {
                await Delay(250);
                return;
            }

            if (_keepOpen is not null && !_keepOpen())
            {
                Close();
                return;
            }

            foreach (var control in BlockedControls)
                API.DisableControlAction(0, control, true);

            if (_busy) return;

            if (Pressed(ControlUp) || Pressed(ControlScrollUp))
                Move(-1);
            else if (Pressed(ControlDown) || Pressed(ControlScrollDown))
                Move(1);
            else if (Pressed(ControlBack))
            {
                PlaySound("BACK");
                Pop();
            }
            else if (Pressed(ControlSelect))
                await SelectAsync();
        }

        private static void Move(int step)
        {
            if (_page is null || _page.Items.Count == 0) return;

            var count = _page.Items.Count;
            var index = Indices[Indices.Count - 1];
            index = ((index + step) % count + count) % count;

            Indices[Indices.Count - 1] = index;
            PlaySound("NAV_UP_DOWN");
            Render();
        }

        private static async Task SelectAsync()
        {
            if (_page is null || _page.Items.Count == 0) return;

            var item = _page.Items[Indices[Indices.Count - 1]];
            if (item.Disabled)
            {
                PlaySound("ERROR");
                return;
            }

            PlaySound("SELECT");

            if (item.Submenu is not null)
            {
                Push(item.Submenu);
                return;
            }

            if (item.Select is null) return;

            _busy = true;
            try
            {
                await item.Select();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CompactMenu] '{item.Label}' failed: {ex}");
            }
            finally
            {
                _busy = false;
            }

            Refresh();
        }

        private static void Render()
        {
            if (_page is null) return;

            Send("show", new
            {
                _page.Title,
                _page.Subtitle,
                Icon  = _icon,
                Index = Indices[Indices.Count - 1],
                Back  = Pages.Count > 1,
                Items = _page.Items.Select(i => new { i.Label, i.Detail, i.Disabled, HasSubmenu = i.Submenu is not null }),
            });
        }

        private static int FirstEnabled(MenuPage page)
        {
            var index = page.Items.FindIndex(i => !i.Disabled);
            return index < 0 ? 0 : index;
        }

        private static int Clamp(int index, int count) => count == 0 ? 0 : Math.Max(0, Math.Min(index, count - 1));

        private static bool Pressed(int control) => API.IsDisabledControlJustPressed(0, control);

        private static void PlaySound(string name) => API.PlaySoundFrontend(-1, name, SoundSet, true);

        private static void Send(string type, object? payload) =>
            API.SendNuiMessage(JsonConvert.SerializeObject(new { screen = "menu", type, payload }, JsonSettings));
    }
}
