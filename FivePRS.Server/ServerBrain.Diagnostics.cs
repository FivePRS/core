using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Core.Config;

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
            Debug.WriteLine($"[FivePRS] FivePRS {version} on {Platform()}, database {_databaseType} (schema {_db.SchemaVersion}).");

            if (version.IsPreRelease)
                Debug.WriteLine("[FivePRS] This is a public test build. Back up your database before updating, and report problems at https://github.com/FivePRS/core/issues");

            foreach (var warning in StartupWarnings())
                Debug.WriteLine($"[FivePRS] WARNING: {warning}");
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

        private Task<string> DiagnosticsAsync(Player? caller, string[] args)
        {
            var addons  = StartedAddons().ToList();
            var units   = _dispatch.UnitIds.Count();
            var players = Players.Count();
            var updates = ConfigManager.Settings.Updates;
            var update  = !updates.CheckForUpdates ? "checks off"
                : !_updateChecked ? "not checked yet"
                : _availableUpdate is null ? "up to date"
                : $"{_availableUpdate.Version} available";

            var lines = new List<string>
            {
                "FivePRS diagnostics",
                $"Version: {InstalledVersion()}",
                $"Server: {API.GetConvar("version", "unknown")}",
                $"Platform: {Platform()}, runtime {Environment.Version}",
                $"OneSync: {API.GetConvar("onesync", "off")}",
                $"Database: {(_db.IsReady ? $"{_databaseType}, schema {_db.SchemaVersion}" : "not ready")}",
                $"Players: {players} online, {units} on duty",
                $"Dispatch: {_dispatch.CatalogCount} callouts, {_dispatch.ActiveCallCount} active calls",
                $"Rate limits: {(ConfigManager.Settings.RateLimits.Enabled ? "on" : "off")}",
                $"Updates: {update}",
                $"Addons: {(addons.Count > 0 ? string.Join(", ", addons) : "none")}",
            };

            var warnings = StartupWarnings();
            lines.Add(warnings.Count == 0 ? "Warnings: none" : $"Warnings: {string.Join(" | ", warnings)}");

            return Task.FromResult(string.Join("\n", lines));
        }

        private static IEnumerable<string> StartedAddons()
        {
            var current = API.GetCurrentResourceName();
            for (var i = 0; i < API.GetNumResources(); i++)
            {
                var name = API.GetResourceByFindIndex(i);
                if (string.IsNullOrEmpty(name) || name == current || !name.StartsWith("fiveprs", StringComparison.OrdinalIgnoreCase)) continue;
                if (API.GetResourceState(name) != "started") continue;

                var version = API.GetResourceMetadata(name, "version", 0);
                yield return string.IsNullOrEmpty(version) ? name : $"{name} {version}";
            }
        }

        private static string Platform() =>
            Environment.OSVersion.Platform == PlatformID.Unix ? "Linux" : "Windows";
    }
}
