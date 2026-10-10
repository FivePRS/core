using System;
using System.Text;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using Newtonsoft.Json;

namespace FivePRS.Client
{
    public static class ClientEvents
    {
        private const string RelayCommand = "fiveprs_relay";

        private static bool _useRelay;

        public static void TriggerServer(string eventName, params object[] args) => Trigger(eventName, args, true);

        public static void TriggerLocal(string eventName, params object[] args) => Trigger(eventName, args, false);

        private static void Trigger(string eventName, object[] args, bool remote)
        {
            if (!_useRelay)
            {
                try
                {
                    if (remote)
                        BaseScript.TriggerServerEvent(eventName, args);
                    else
                        BaseScript.TriggerEvent(eventName, args);
                    return;
                }
                catch (ArgumentException ex) when (ex.Message.Contains("expected a managed pointer"))
                {
                    _useRelay = true;
                    Debug.WriteLine("[FivePRS] This client build blocks C# event natives; routing events through the Lua relay.");
                }
            }

            var payload = JsonConvert.SerializeObject(new { e = eventName, r = remote, n = args.Length, a = args });
            API.ExecuteCommand($"{RelayCommand} {ToHex(payload)}");
        }

        private static string ToHex(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
                builder.Append(b.ToString("x2"));
            return builder.ToString();
        }
    }
}
