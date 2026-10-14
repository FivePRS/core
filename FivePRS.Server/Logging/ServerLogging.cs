using System;
using System.IO;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Core.Logging;

namespace FivePRS.Server.Logging
{
    internal static class ServerLogging
    {
        public static void Start(string resourcePath)
        {
            Logger.DebugEnabled = string.Equals(API.GetConvar("fiveprs_debug", "false"), "true", StringComparison.OrdinalIgnoreCase);

            var file = new FileLogSink(Path.Combine(resourcePath, "data", "logs"));
            file.Prune();

            Logger.ClearSinks();
            Logger.AddSink(new ConsoleSink(message => Debug.WriteLine(message), "Full error details are saved in data/logs."));
            Logger.AddSink(file);
        }
    }
}
