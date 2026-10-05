using System;
using System.Threading;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;

namespace FivePRS.Client
{
    public static class Timing
    {
        private const int SliceMs = 100;

        public static async Task WaitAsync(int ms, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var end = API.GetGameTimer() + ms;
            while (true)
            {
                var remaining = end - API.GetGameTimer();
                if (remaining <= 0) return;
                await BaseScript.Delay(Math.Min(SliceMs, remaining));
                ct.ThrowIfCancellationRequested();
            }
        }

        public static async Task<bool> TryWaitAsync(int ms, CancellationToken ct)
        {
            try
            {
                await WaitAsync(ms, ct);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }
    }
}
