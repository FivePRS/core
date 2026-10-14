using System;
using System.IO;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Core.Logging;
using FivePRS.Server.Http;

namespace FivePRS.Server
{
    internal static class ServerIcon
    {
        private const string RelativePath = "data/server_icon.png";
        private const string LoadCommandAce = "command.load_server_icon";
        private const int ApplyTimeoutMs = 2_000;

        private static readonly Logger Log = Logger.For("ServerIcon");

        public static async Task ApplyDefaultAsync(string url)
        {
            try
            {
                await ApplyAsync(url);
            }
            catch (UnauthorizedAccessException)
            {
                Log.Warn("Could not set the server icon. The server needs write access to the fiveprs folder.");
            }
            catch (Exception ex)
            {
                Log.Warn("Could not set the server icon", ex);
            }
        }

        private static async Task ApplyAsync(string url)
        {
            if (!IsEnabled() || HasIcon() || string.IsNullOrWhiteSpace(url)) return;

            var resourcePath = Resource.Path;
            var path         = Path.Combine(resourcePath, RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            await BaseScript.Delay(0);

            var result = await HttpBridge.GetAsync(url, RelativePath, check: "png");
            if (!result.Ok)
            {
                if (!File.Exists(path))
                {
                    Log.Warn($"Could not download the server icon: {result.Error}");
                    return;
                }

                Log.Debug($"Could not download the server icon ({result.Error}), using the saved copy.");
            }

            if (HasIcon()) return;

            var resource = Resource.Name;
            if (!API.IsPrincipalAceAllowed($"resource.{resource}", LoadCommandAce))
            {
                Log.Warn($"Server icon skipped. Add \"add_ace resource.{resource} {LoadCommandAce} allow\" to server.cfg, or set fiveprs_server_icon false.");
                return;
            }

            API.ExecuteCommand($"load_server_icon \"{path.Replace('\\', '/')}\"");

            var deadline = API.GetGameTimer() + ApplyTimeoutMs;
            while (!HasIcon() && API.GetGameTimer() < deadline)
                await BaseScript.Delay(100);

            if (HasIcon())
                Log.Info("Using the FivePRS server icon.");
            else
                Log.Warn("load_server_icon didn't set the server icon. Check the console above for the reason.");
        }

        private static bool IsEnabled() =>
            string.Equals(API.GetConvar("fiveprs_server_icon", "true"), "true", StringComparison.OrdinalIgnoreCase);

        private static bool HasIcon() => !string.IsNullOrEmpty(API.GetConvar("sv_icon", string.Empty));
    }
}
