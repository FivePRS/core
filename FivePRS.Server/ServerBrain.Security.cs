using System;
using CitizenFX.Core;
using FivePRS.Core.Config;
using FivePRS.Server.Security;

namespace FivePRS.Server
{
    public partial class ServerBrain
    {
        private const int MaxCallIdLength = 32;

        private readonly RateLimiter _rateLimiter = new(() => DateTime.UtcNow, () => ConfigManager.Settings.RateLimits.Enabled);

        private bool Allow(Player player, string bucket, Func<RateLimitSettings, RateLimit> limit)
        {
            var result = _rateLimiter.Check(player.Handle, bucket, limit(ConfigManager.Settings.RateLimits));
            if (result == RateLimitResult.LimitedFirst)
                Debug.WriteLine($"[FivePRS] {player.Name} ({player.Handle}) is sending too many {bucket} requests; ignoring them for now.");
            return result == RateLimitResult.Allowed;
        }

        private static bool IsCallId(string? callId) =>
            !string.IsNullOrEmpty(callId) && callId!.Length <= MaxCallIdLength;

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

