using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;

namespace FivePRS.Server
{
    internal static class ServerIcon
    {
        private static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(15);

        public static async Task ApplyDefaultAsync(string url)
        {
            if (!IsEnabled() || HasIcon() || string.IsNullOrWhiteSpace(url)) return;

            var path = Path.Combine(API.GetResourcePath(API.GetCurrentResourceName()), "data", "server_icon.png");

            try
            {
                using var client = new HttpClient { Timeout = DownloadTimeout };
                var bytes = await client.GetByteArrayAsync(url);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, bytes);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FivePRS] Could not download the server icon from {url}: {ex.Message}");
                if (!File.Exists(path)) return;
            }

            await BaseScript.Delay(0);
            if (HasIcon()) return;

            API.ExecuteCommand($"load_server_icon \"{path.Replace('\\', '/')}\"");
            Debug.WriteLine("[FivePRS] Using the FivePRS server icon. Set your own with load_server_icon in server.cfg.");
        }

        private static bool IsEnabled() =>
            string.Equals(API.GetConvar("fiveprs_server_icon", "true"), "true", StringComparison.OrdinalIgnoreCase);

        private static bool HasIcon() => !string.IsNullOrEmpty(API.GetConvar("sv_icon", string.Empty));
    }
}
