using System;
using System.IO;
using CitizenFX.Core;
using CitizenFX.Core.Native;

namespace FivePRS.Server
{
    internal static class ServerIcon
    {
        private const string IconFile = "branding/server_icon.png";

        public static void ApplyDefault()
        {
            if (!string.Equals(API.GetConvar("fiveprs_server_icon", "true"), "true", StringComparison.OrdinalIgnoreCase))
                return;

            if (!string.IsNullOrEmpty(API.GetConvar("sv_icon", string.Empty)))
                return;

            var path = Path.Combine(API.GetResourcePath(API.GetCurrentResourceName()), IconFile);
            if (!File.Exists(path)) return;

            API.ExecuteCommand($"load_server_icon \"{path.Replace('\\', '/')}\"");
            Debug.WriteLine("[FivePRS] Using the FivePRS server icon. Set your own with load_server_icon in server.cfg.");
        }
    }
}
