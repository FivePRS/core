using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Core.Config;
using FivePRS.Core.Updates;
using FivePRS.Server.Http;

namespace FivePRS.Server
{
    public partial class ServerBrain
    {
        private const int FirstUpdateCheckDelayMs = 15_000;
        private const int DownloadTimeoutMs = 180_000;
        private const string GitHubAccept = "application/vnd.github+json";

        private static readonly string[] PreservedFolders = { "config/", "data/" };

        private ReleaseInfo? _availableUpdate;
        private string? _announcedVersion;
        private bool _updateChecked;
        private bool _downloadingUpdate;

        private void RegisterUpdates()
        {
            RegisterAdminCommand("fiveprs_update", "", 0, DownloadUpdateAsync);
            Tick += UpdateCheckTickAsync;
        }

        private static ReleaseVersion InstalledVersion()
        {
            var text = API.GetResourceMetadata(API.GetCurrentResourceName(), "version", 0);
            return ReleaseVersion.TryParse(text, out var version) ? version : ReleaseVersion.Zero;
        }

        private async Task UpdateCheckTickAsync()
        {
            var settings = ConfigManager.Settings.Updates;
            await Delay(_updateChecked ? Math.Max(1, settings.CheckIntervalHours) * 3_600_000 : FirstUpdateCheckDelayMs);
            if (settings.CheckForUpdates) await CheckForUpdateAsync();
        }

        private async Task CheckForUpdateAsync()
        {
            var settings  = ConfigManager.Settings.Updates;
            var installed = InstalledVersion();
            var first     = !_updateChecked;
            _updateChecked = true;

            var response = await HttpBridge.GetAsync(ReleaseFeed.ReleasesUrl(settings.Repository), accept: GitHubAccept);
            if (!response.Ok)
            {
                Debug.WriteLine($"[FivePRS] Could not check for updates: {response.Error}");
                return;
            }

            var latest = ReleaseFeed.Latest(response.Body, settings.AssetName, settings.IncludePreReleases);
            _availableUpdate = latest is not null && latest.Version.IsNewerThan(installed) ? latest : null;

            if (_availableUpdate is null)
            {
                if (first) Debug.WriteLine($"[FivePRS] FivePRS {installed} is up to date.");
                return;
            }

            var version = _availableUpdate.Version.ToString();
            if (_announcedVersion == version) return;
            _announcedVersion = version;

            Debug.WriteLine($"[FivePRS] FivePRS {version} is available. You have {installed}. Release notes: {_availableUpdate.Url}");
            foreach (var line in ReleaseFeed.Summary(_availableUpdate.Notes).Split('\n').Where(line => line.Length > 0))
                Debug.WriteLine($"[FivePRS]   {line}");
            Debug.WriteLine("[FivePRS] Run fiveprs_update in the server console to download it.");

            foreach (var player in Players.Where(player => _permissions.IsAdmin(player.Handle)))
                NotifyUpdate(player);
        }

        private void NotifyUpdate(Player player)
        {
            if (_availableUpdate is null) return;
            Notify(player, $"Update | ~g~FivePRS {_availableUpdate.Version} is available.~w~ Run fiveprs_update in the server console.");
        }

        private async Task<string> DownloadUpdateAsync(Player? caller, string[] args)
        {
            if (_downloadingUpdate) return "An update is already downloading.";

            _downloadingUpdate = true;
            try
            {
                await CheckForUpdateAsync();

                var release = _availableUpdate;
                if (release is null) return $"FivePRS {InstalledVersion()} is up to date.";
                if (release.AssetUrl is null)
                    return $"FivePRS {release.Version} has no {ConfigManager.Settings.Updates.AssetName} to download. Get it from {release.Url}";

                var version      = release.Version.ToString();
                var resourcePath = API.GetResourcePath(API.GetCurrentResourceName());
                var zipRelative  = $"data/updates/fiveprs-{version}.zip";
                var zipPath      = Path.Combine(resourcePath, zipRelative);
                var target       = Path.Combine(resourcePath, "data", "updates", version);
                Directory.CreateDirectory(Path.GetDirectoryName(zipPath)!);

                Reply(caller, $"Downloading FivePRS {version}...");
                var download = await HttpBridge.GetAsync(release.AssetUrl, zipRelative, check: "zip", timeoutMs: DownloadTimeoutMs);
                if (!download.Ok) return $"The download failed: {download.Error}";

                try
                {
                    var extracted = ExtractRelease(zipPath, target);
                    return $"FivePRS {version} is ready in {extracted}. Stop the server, copy everything in that folder over resources/fiveprs, and start it again. " +
                           $"Your config/ and data/ folders are not included, so your settings and database are kept. Release notes: {release.Url}";
                }
                catch (Exception ex)
                {
                    return $"FivePRS {version} was downloaded to {zipPath}, but it could not be unpacked ({ex.Message}). " +
                           "Unpack it yourself and copy it over resources/fiveprs without its config/ and data/ folders.";
                }
            }
            finally
            {
                _downloadingUpdate = false;
            }
        }

        private static string ExtractRelease(string zipPath, string target)
        {
            if (Directory.Exists(target)) Directory.Delete(target, true);
            Directory.CreateDirectory(target);

            var root = Path.GetFullPath(target);
            using var archive = ZipFile.OpenRead(zipPath);

            var prefix = CommonFolder(archive);
            foreach (var entry in archive.Entries)
            {
                var relative = entry.FullName.Replace('\\', '/');
                if (prefix.Length > 0) relative = relative.Substring(prefix.Length);
                if (relative.Length == 0 || PreservedFolders.Any(folder => relative.StartsWith(folder, StringComparison.OrdinalIgnoreCase))) continue;

                var destination = Path.GetFullPath(Path.Combine(root, relative));
                if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;

                if (relative.EndsWith("/"))
                {
                    Directory.CreateDirectory(destination);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                entry.ExtractToFile(destination, true);
            }

            return root;
        }

        private static string CommonFolder(ZipArchive archive)
        {
            var first = archive.Entries.Select(entry => entry.FullName.Replace('\\', '/')).FirstOrDefault(name => name.Contains("/"));
            if (first is null) return string.Empty;

            var folder = first.Substring(0, first.IndexOf('/') + 1);
            return archive.Entries.All(entry => entry.FullName.Replace('\\', '/').StartsWith(folder, StringComparison.Ordinal)) ? folder : string.Empty;
        }
    }
}
