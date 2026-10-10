using System;
using System.Collections.Generic;
using System.Linq;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Core.Events;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace FivePRS.Client.Terminal
{
    public class TerminalApps : BaseScript
    {
        private const int MaxIdLength = 32;

        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
        };

        private static readonly List<RegisteredApp> Apps = new();
        private static string? _openId;

        public static event Action? Changed;

        public TerminalApps()
        {
            Exports.Add("registerApp", new Action<IDictionary<string, object>>(RegisterFromResource));
            Exports.Add("unregisterApp", new Action<string>(id => Remove(Find(id, API.GetInvokingResource()))));
            Exports.Add("setAppAvailable", new Action<string, bool>((id, available) => Update(id, app => app.Available = available)));
            Exports.Add("setAppBadge", new Action<string, string>((id, badge) => Update(id, app => app.Badge = string.IsNullOrEmpty(badge) ? null : badge)));
            Exports.Add("setAppView", new Action<string, object>((id, view) => Update(id, app => app.View = ToScreen(view))));
            Exports.Add("sendAppMessage", new Action<string, string, object>((id, type, payload) =>
            {
                if (Find(id, API.GetInvokingResource()) is not null) SendToApp(id, type, payload);
            }));

            EventHandlers["onClientResourceStop"] += new Action<string>(resource =>
            {
                if (Apps.RemoveAll(app => app.Resource == resource) > 0) NotifyChanged();
            });
        }

        internal static void Register(TerminalApp app)
        {
            if (!IsValidId(app.Id) || Apps.Any(existing => existing.Id == app.Id))
            {
                Debug.WriteLine($"[FivePRS] Terminal app '{app.Id}' was not registered: the id is invalid or already in use.");
                return;
            }

            var page = app.Page is null ? null : $"https://cfx-nui-{API.GetCurrentResourceName()}/{app.Page.TrimStart('/')}";
            Apps.Add(new RegisteredApp { Id = app.Id, Instance = app, Page = page });
            NotifyChanged();
        }

        private static void RegisterFromResource(IDictionary<string, object> definition)
        {
            var resource = API.GetInvokingResource();
            var id       = Text(definition, "id");

            if (!IsValidId(id) || Apps.Any(existing => existing.Id == id && existing.Resource != resource))
            {
                Debug.WriteLine($"[FivePRS] {resource} could not register terminal app '{id}': the id is invalid or already in use.");
                return;
            }

            Apps.RemoveAll(existing => existing.Id == id);
            Apps.Add(new RegisteredApp
            {
                Id        = id,
                Resource  = resource,
                Label     = Text(definition, "label") is { Length: > 0 } label ? label : id,
                Icon      = Text(definition, "icon"),
                Color     = Text(definition, "color") is { Length: > 0 } color ? color : "#334155",
                Order     = definition.TryGetValue("order", out var order) && int.TryParse(order?.ToString(), out var value) ? value : 500,
                Page      = Text(definition, "page") is { Length: > 0 } page ? page : null,
                Available = !definition.TryGetValue("available", out var available) || available is not bool flag || flag,
            });
            NotifyChanged();
        }

        public static void NotifyChanged() => Changed?.Invoke();

        public static void SendToApp(string id, string type, object? payload) =>
            API.SendNuiMessage(JsonConvert.SerializeObject(
                new { screen = "app", type = "appMessage", payload = new { App = id, Type = type, Payload = payload } }, JsonSettings));

        internal static void SetOpen(string? id)
        {
            var next = Apps.FirstOrDefault(app => app.Id == id);
            if (next?.Id == _openId) return;

            Apps.FirstOrDefault(app => app.Id == _openId)?.Instance?.OnClosed();
            _openId = next?.Id;
            next?.Instance?.OnOpened();
        }

        internal static void Act(string id, string action, IDictionary<string, object> data)
        {
            var app = Apps.FirstOrDefault(entry => entry.Id == id);
            if (app is null || action.Length == 0) return;

            if (app.Instance is not null)
                app.Instance.OnAction(action, data);
            else
                TriggerEvent(EventNames.PublicAppAction, id, action, data);
        }

        internal static object BuildList()
        {
            var player = ClientBrain.LocalPlayerData;
            return Apps
                .Where(app => app.Instance?.IsAvailable(player) ?? app.Available)
                .Select(app => new
                {
                    app.Id,
                    Label = app.Instance?.Label ?? app.Label,
                    Icon  = app.Instance?.Icon ?? app.Icon,
                    Color = app.Instance?.Color ?? app.Color,
                    Order = app.Instance?.Order ?? app.Order,
                    Badge = app.Instance?.Badge ?? app.Badge,
                    app.Page,
                })
                .OrderBy(app => app.Order)
                .ToList();
        }

        internal static object? BuildOpenView()
        {
            var app = Apps.FirstOrDefault(entry => entry.Id == _openId);
            if (app is null || app.Page is not null) return null;

            var screen = app.Instance is not null ? app.Instance.BuildScreen() : app.View;
            return screen is null ? null : new { App = app.Id, Screen = screen };
        }

        private static void Update(string id, Action<RegisteredApp> change)
        {
            var app = Find(id, API.GetInvokingResource());
            if (app is null) return;

            change(app);
            NotifyChanged();
        }

        private static RegisteredApp? Find(string id, string resource) =>
            Apps.FirstOrDefault(app => app.Id == id && app.Resource == resource);

        private static void Remove(RegisteredApp? app)
        {
            if (app is not null && Apps.Remove(app)) NotifyChanged();
        }

        private static AppScreen? ToScreen(object? view)
        {
            if (view is null) return null;

            try
            {
                return JsonConvert.DeserializeObject<AppScreen>(JsonConvert.SerializeObject(view));
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"[FivePRS] Invalid terminal app view: {ex.Message}");
                return null;
            }
        }

        private static bool IsValidId(string? id) =>
            id is not null && id.Length > 0 && id.Length <= MaxIdLength &&
            id.All(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-');

        private static string Text(IDictionary<string, object> data, string key) =>
            data.TryGetValue(key, out var value) ? value?.ToString() ?? string.Empty : string.Empty;

        private sealed class RegisteredApp
        {
            public string Id { get; set; } = string.Empty;
            public string? Resource { get; set; }
            public TerminalApp? Instance { get; set; }
            public string Label { get; set; } = string.Empty;
            public string Icon { get; set; } = string.Empty;
            public string Color { get; set; } = "#334155";
            public int Order { get; set; } = 500;
            public string? Page { get; set; }
            public string? Badge { get; set; }
            public bool Available { get; set; } = true;
            public AppScreen? View { get; set; }
        }
    }
}
