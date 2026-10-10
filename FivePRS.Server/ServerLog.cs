using System;
using System.Reflection;
using CitizenFX.Core;
using CitizenFX.Core.Native;

namespace FivePRS.Server
{
    internal static class ServerLog
    {
        private static bool _hinted;

        public static bool DebugEnabled =>
            string.Equals(API.GetConvar("fiveprs_debug", "false"), "true", StringComparison.OrdinalIgnoreCase);

        public static void Error(string message, Exception ex)
        {
            Debug.WriteLine($"[FivePRS] {message}: {Summary(ex)}");

            if (DebugEnabled)
            {
                Debug.WriteLine(ex.ToString());
            }
            else if (!_hinted)
            {
                _hinted = true;
                Debug.WriteLine("[FivePRS] Set fiveprs_debug true in server.cfg to include full error details in the console.");
            }
        }

        public static string Summary(Exception ex)
        {
            while (ex is AggregateException { InnerException: not null } || ex is TargetInvocationException { InnerException: not null } ||
                   ex is TypeInitializationException { InnerException: not null })
            {
                ex = ex.InnerException!;
            }

            return ex.Message;
        }
    }
}
