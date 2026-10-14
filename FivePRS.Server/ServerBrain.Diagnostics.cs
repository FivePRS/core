using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Core.Config;
using FivePRS.Core.Models;

namespace FivePRS.Server
{
    public partial class ServerBrain
    {
        private void RegisterDiagnostics()
        {
            RegisterAdminCommand("fiveprs_diag", "", 0, DiagnosticsAsync);
        }

        private void LogStartup()
        {
            var version = InstalledVersion();
            Log.Success($"FivePRS {version} ready on {Platform()}, {_databaseType} schema {_db.SchemaVersion}.");

            if (version.IsPreRelease)
                Log.Warn("Public test build. Back up your database before updating.");

            foreach (var warning in StartupWarnings())
                Log.Warn(warning);
        }

        private static List<string> StartupWarnings()
        {
            var warnings = new List<string>();

            var oneSync = API.GetConvar("onesync", "off");
            if (string.Equals(oneSync, "off", StringComparison.OrdinalIgnoreCase))
                warnings.Add("OneSync is off. FivePRS needs OneSync for unit positions, 911 locations and vehicle registration. Set \"onesync on\" in server.cfg or txAdmin.");

            var dbType = API.GetConvar("fiveprs_db_type", "sqlite");
            if (!dbType.Equals("sqlite", StringComparison.OrdinalIgnoreCase) && !dbType.Equals("mysql", StringComparison.OrdinalIgnoreCase))
                warnings.Add($"fiveprs_db_type \"{dbType}\" is not sqlite or mysql, so SQLite is being used.");

            if (!ConfigManager.Settings.RateLimits.Enabled)
                warnings.Add("Rate limits are turned off in config/settings.json, so players can flood the server with requests.");

            return warnings;
        }

        private Task<CommandResult> DiagnosticsAsync(Player? caller, string[] args)
        {
            var lines = new List<string> { "FivePRS diagnostics" };
            lines.AddRange(DiagnosticFacts().Select(fact => $"{fact.Label}: {fact.Value}"));

            var addons = Addons().Select(addon => string.IsNullOrEmpty(addon.Version) ? addon.Name : $"{addon.Name} {addon.Version}").ToList();
            lines.Add($"Addons: {(addons.Count > 0 ? string.Join(", ", addons) : "none")}");

            var warnings = StartupWarnings();
            lines.Add(warnings.Count == 0 ? "Warnings: none" : $"Warnings: {string.Join(" | ", warnings)}");

            return Task.FromResult(CommandResult.Done(string.Join("\n", lines)));
        }

        private List<AdminFact> DiagnosticFacts()
        {
            var units   = _dispatch.UnitIds.Count();
            var players = Players.Count();
            var updates = ConfigManager.Settings.Updates;
            var update  = !updates.CheckForUpdates ? "checks off"
                : !_updateChecked ? "not checked yet"
                : _availableUpdate is null ? "up to date"
                : $"{_availableUpdate.Version} available";

            return new List<AdminFact>
            {
                new("Version", InstalledVersion().ToString()),
                new("Server", API.GetConvar("version", "unknown")),
                new("Platform", $"{Platform()}, runtime {Environment.Version}"),
                new("OneSync", API.GetConvar("onesync", "off")),
                new("Database", _db.IsReady ? $"{_databaseType}, schema {_db.SchemaVersion}" : "not ready"),
                new("Players", $"{players} online, {units} on duty"),
                new("Dispatch", $"{_dispatch.CatalogCount} callouts, {_dispatch.ActiveCallCount} active calls"),
                new("Rate limits", ConfigManager.Settings.RateLimits.Enabled ? "on" : "off"),
                new("Updates", update),
            };
        }

        private const string OfficialRepository = "https://github.com/FivePRS/";

        private static readonly string[] DropInFolders = { "plugins", "callouts", "apps" };

        private static List<AdminAddon> Addons()
        {
            var addons = new List<AdminAddon>();
            var current = Resource.Name;

            for (var i = 0; i < API.GetNumResources(); i++)
            {
                var name = API.GetResourceByFindIndex(i);
                if (string.IsNullOrEmpty(name) || name == current || API.GetResourceState(name) != "started") continue;
                if (!name.StartsWith("fiveprs", StringComparison.OrdinalIgnoreCase) && !DependsOnFivePrs(name, current)) continue;

                var repository = API.GetResourceMetadata(name, "repository", 0) ?? string.Empty;
                addons.Add(new AdminAddon
                {
                    Name        = name,
                    Kind        = "Resource",
                    Version     = API.GetResourceMetadata(name, "version", 0) ?? string.Empty,
                    Author      = AuthorName(API.GetResourceMetadata(name, "author", 0)),
                    Description = API.GetResourceMetadata(name, "description", 0) ?? string.Empty,
                    Official    = repository.StartsWith(OfficialRepository, StringComparison.OrdinalIgnoreCase),
                });
            }

            foreach (var folder in DropInFolders)
            {
                var path = Path.Combine(Resource.Path, folder);
                if (!Directory.Exists(path)) continue;

                foreach (var file in Directory.GetFiles(path, "*.net.dll"))
                {
                    var name = Path.GetFileName(file);
                    addons.Add(new AdminAddon
                    {
                        Name = name.Substring(0, name.Length - ".net.dll".Length),
                        Kind = char.ToUpperInvariant(folder[0]) + folder.Substring(1, folder.Length - 2),
                    });
                }
            }

            return addons.OrderByDescending(addon => addon.Official).ThenBy(addon => addon.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static bool DependsOnFivePrs(string resource, string fiveprs)
        {
            foreach (var key in new[] { "dependency", "dependencies" })
            {
                var count = API.GetNumResourceMetadata(resource, key);
                for (var i = 0; i < count; i++)
                {
                    if (string.Equals(API.GetResourceMetadata(resource, key, i), fiveprs, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }

            return false;
        }

        private static string AuthorName(string? author)
        {
            var value = (author ?? string.Empty).Trim();
            var link = value.IndexOf('<');
            return (link > 0 ? value.Substring(0, link) : value).Trim();
        }

        private static string Platform() =>
            Environment.OSVersion.Platform == PlatformID.Unix ? "Linux" : "Windows";
    }
}
