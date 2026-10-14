using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using Newtonsoft.Json;

namespace FivePRS.Client.Appearance
{
    public class Mugshot : BaseScript
    {
        private const string CapturedCallback = "fiveprsCaptured";
        private const int HeadBone = 31086;
        private const int SettleFrames = 12;
        private const int TimeoutMs = 8_000;
        private const float Distance = 1.15f;
        private const float FieldOfView = 30f;

        private static TaskCompletionSource<string>? _pending;
        private static int _requestId;

        public Mugshot()
        {
            API.RegisterNuiCallbackType(CapturedCallback);
            EventHandlers[$"__cfx_nui:{CapturedCallback}"] += new Action<IDictionary<string, object>, CallbackDelegate>(OnCaptured);
        }

        private static void OnCaptured(IDictionary<string, object> data, CallbackDelegate callback)
        {
            if (data.TryGetValue("id", out var rawId) && Convert.ToInt32(rawId) == _requestId)
                _pending?.TrySetResult(data.TryGetValue("data", out var image) ? image?.ToString() ?? string.Empty : string.Empty);

            callback(new { });
        }

        public static async Task<string?> CaptureAsync()
        {
            if (_pending is not null) return null;

            var ped  = Game.PlayerPed;
            var head = API.GetPedBoneCoords(ped.Handle, HeadBone, 0f, 0f, 0f);
            var eye  = head + ped.ForwardVector * Distance;

            var camera = API.CreateCamWithParams("DEFAULT_SCRIPTED_CAMERA", eye.X, eye.Y, eye.Z, 0f, 0f, 0f, FieldOfView, false, 0);
            API.PointCamAtCoord(camera, head.X, head.Y, head.Z - 0.04f);
            API.SetCamActive(camera, true);
            API.RenderScriptCams(true, false, 0, true, true);
            API.FreezeEntityPosition(ped.Handle, true);

            var pending = new TaskCompletionSource<string>();
            _pending = pending;

            try
            {
                for (var i = 0; i < SettleFrames; i++)
                {
                    API.HideHudAndRadarThisFrame();
                    await Delay(0);
                }

                API.SendNuiMessage(JsonConvert.SerializeObject(new { fiveprsCapture = ++_requestId }));

                var deadline = API.GetGameTimer() + TimeoutMs;
                while (!pending.Task.IsCompleted && API.GetGameTimer() < deadline)
                {
                    API.HideHudAndRadarThisFrame();
                    await Delay(0);
                }

                return pending.Task.IsCompleted && pending.Task.Result.Length > 0 ? pending.Task.Result : null;
            }
            finally
            {
                _pending = null;
                API.RenderScriptCams(false, false, 0, true, true);
                API.DestroyCam(camera, false);
                API.FreezeEntityPosition(ped.Handle, false);
            }
        }
    }
}
