using System;
using System.Collections.Generic;
using System.IO;

namespace FivePRS.Server
{
    public static class RetiredFiles
    {
        public static readonly IReadOnlyList<string> Server = new[]
        {
            "MySqlConnector.dll",
            "Microsoft.Bcl.AsyncInterfaces.dll",
            "Microsoft.Extensions.DependencyInjection.Abstractions.dll",
            "Microsoft.Extensions.Logging.Abstractions.dll",
            "System.Diagnostics.DiagnosticSource.dll",
            "System.Threading.Tasks.Extensions.dll",
        };

        public static List<string> Remove(string directory, Action<string> warn)
        {
            var removed = new List<string>();
            foreach (var name in Server)
            {
                var path = Path.Combine(directory, name);
                if (!File.Exists(path)) continue;

                try
                {
                    File.Delete(path);
                    removed.Add(name);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    warn($"{name} is left over from an older FivePRS version and couldn't be deleted ({ex.Message}). Delete it from {directory}.");
                }
            }
            return removed;
        }
    }
}
