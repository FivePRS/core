using CitizenFX.Core.Native;
using FivePRS.Client.Callouts;
using FivePRS.Core.Events;

namespace FivePRS.Client.Dispatch
{
    internal static class AiCallouts
    {
        private const string DisabledKey = "ai_callouts_off";

        public static bool Enabled => API.GetResourceKvpInt(DisabledKey) == 0;

        public static void Set(bool enabled)
        {
            API.SetResourceKvpInt(DisabledKey, enabled ? 0 : 1);
            ClientEvents.TriggerServer(EventNames.ServerSetAiCallouts, enabled);

            if (!enabled && CalloutDispatcher.PendingOffer is not null)
                CalloutDispatcher.DeclineOffer();
        }

        public static void Toggle() => Set(!Enabled);

        public static void Restore()
        {
            if (!Enabled) ClientEvents.TriggerServer(EventNames.ServerSetAiCallouts, false);
        }
    }
}
