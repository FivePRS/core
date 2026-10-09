using System;
using System.IO;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Core.Events;

namespace FivePRS.Server
{
    internal static class ServerIcon
    {
        private const string RelativePath = "data/server_icon.png";
        private const int DownloadTimeoutMs = 20_000;
        private const string LoadCommandAce = "command.load_server_icon";

        private static TaskCompletionSource<string?>? _download;

        public static async Task ApplyDefaultAsync(string url)
        {
            if (!IsEnabled() || HasIcon() || string.IsNullOrWhiteSpace(url)) return;

            var resourcePath = API.GetResourcePath(API.GetCurrentResourceName());
            var path         = Path.Combine(resourcePath, RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            await BaseScript.Delay(0);

            var error = await DownloadAsync(url);
            if (error is not null)
            {
                Debug.WriteLine($"[FivePRS] Could not download the server icon from {url}: {error}");
                if (!File.Exists(path)) return;
                Debug.WriteLine("[FivePRS] Using the previously downloaded server icon.");
            }

            if (HasIcon()) return;

            var resource = API.GetCurrentResourceName();
            if (!API.IsPrincipalAceAllowed($"resource.{resource}", LoadCommandAce))
            {
                Debug.WriteLine(
                    $"[FivePRS] FivePRS is not allowed to load the server icon. Add \"add_ace resource.{resource} {LoadCommandAce} allow\" " +
                    "to server.cfg, or set fiveprs_server_icon false.");
                return;
            }

            API.ExecuteCommand($"load_server_icon \"{path.Replace('\\', '/')}\"");
            await BaseScript.Delay(0);

            Debug.WriteLine(HasIcon()
                ? "[FivePRS] Using the FivePRS server icon. Set your own with load_server_icon in server.cfg."
                : "[FivePRS] load_server_icon did not set the server icon. Check the server console for the reason.");
        }

        public static void OnDownloaded(bool ok, string message) =>
            _download?.TrySetResult(ok ? null : string.IsNullOrEmpty(message) ? "unknown error" : message);

        private static async Task<string?> DownloadAsync(string url)
        {
            _download = new TaskCompletionSource<string?>();
            BaseScript.TriggerEvent(EventNames.LocalServerIconDownload, url, RelativePath);

            var finished = await Task.WhenAny(_download.Task, BaseScript.Delay(DownloadTimeoutMs));
            return finished == _download.Task ? _download.Task.Result : "timed out";
        }

        private static bool IsEnabled() =>
            string.Equals(API.GetConvar("fiveprs_server_icon", "true"), "true", StringComparison.OrdinalIgnoreCase);

        private static bool HasIcon() => !string.IsNullOrEmpty(API.GetConvar("sv_icon", string.Empty));
    }
}
