using System;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Core.Events;

namespace FivePRS.Client.Appearance
{
    public class Mugshot : BaseScript
    {
        private const string ScreenshotResource = "screenshot-basic";
        private const int HeadBone = 31086;
        private const int SettleFrames = 12;
        private const int TimeoutMs = 8_000;
        private const float Distance = 1.15f;
        private const float FieldOfView = 30f;

        private static TaskCompletionSource<string>? _pending;

        public Mugshot()
        {
            EventHandlers[EventNames.LocalScreenshotTaken] += new Action<string>(data => _pending?.TrySetResult(data ?? string.Empty));
        }

        public static bool IsAvailable => API.GetResourceState(ScreenshotResource) == "started";

        public static async Task<string?> CaptureAsync()
        {
            if (!IsAvailable || _pending is not null) return null;

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

                ClientEvents.TriggerLocal(EventNames.LocalCaptureScreenshot);

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
