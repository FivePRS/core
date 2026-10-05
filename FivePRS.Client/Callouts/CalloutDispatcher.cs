using System;
using System.Threading;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Core.Config;
using FivePRS.Core.Models;

namespace FivePRS.Client.Callouts
{
    public sealed class CalloutDispatcher
    {
        internal static volatile bool AcceptPressed;
        internal static volatile bool DeclinePressed;
        internal static volatile bool EndCalloutPressed;

        private static ResourceSettings Settings => ConfigManager.Settings;

        private readonly Department _department;
        private readonly CalloutRegistry _registry;
        private readonly int _dispatchIntervalMs;
        private readonly Action<CalloutBase, CalloutResult> _onEnded;

        private CancellationTokenSource? _cts;
        private CalloutBase? _activeCallout;
        private bool _busy;

        public bool IsRunning => _cts is not null;
        public bool HasActiveCallout => _activeCallout is not null;

        public CalloutDispatcher(
            Department department,
            CalloutRegistry registry,
            int dispatchIntervalMs,
            Action<CalloutBase, CalloutResult> onEnded)
        {
            _department = department;
            _registry = registry;
            _dispatchIntervalMs = dispatchIntervalMs;
            _onEnded = onEnded;
        }

        public void EndActiveCallout()
        {
            if (_activeCallout is not null)
                EndCalloutPressed = true;
        }

        public void Start()
        {
            if (_cts is not null) return;
            _cts = new CancellationTokenSource();
            _ = DispatchLoopAsync(_cts.Token);
        }

        public void Stop()
        {
            if (_cts is null) return;
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;

            var callout = _activeCallout;
            _activeCallout = null;
            if (callout is null) return;

            try { callout.OnCalloutFailed(); }
            catch (Exception ex) { Debug.WriteLine($"[CalloutDispatcher] OnCalloutFailed threw: {ex.Message}"); }
            callout.SetState(CalloutState.Failed);
            callout.Cleanup();
        }

        public async Task HandleServerCalloutAsync(CalloutData data)
        {
            if (_cts is null || _busy)
            {
                Debug.WriteLine($"[CalloutDispatcher] Server callout '{data.Name}' dropped: dispatcher busy or stopped.");
                return;
            }

            var entry = _registry.FindByName(data.Name);
            if (entry is null)
            {
                Debug.WriteLine($"[CalloutDispatcher] No handler registered for server callout '{data.Name}'.");
                return;
            }

            var callout = CreateCallout(entry);
            if (callout is null) return;

            callout.Data = data;
            await RunCalloutAsync(callout, _cts.Token);
        }

        private async Task DispatchLoopAsync(CancellationToken ct)
        {
            if (!await Timing.TryWaitAsync(Settings.InitialGraceSeconds * 1000, ct)) return;

            while (!ct.IsCancellationRequested)
            {
                if (_busy)
                {
                    if (!await Timing.TryWaitAsync(1000, ct)) return;
                    continue;
                }

                var entry = _registry.PickCallout(_department);
                var callout = entry is null ? null : CreateCallout(entry);

                if (entry is null || callout is null)
                {
                    if (!await Timing.TryWaitAsync(Settings.NoCalloutRetrySeconds * 1000, ct)) return;
                    continue;
                }

                callout.Data = new CalloutData
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = entry.Info.Name,
                    Description = callout.Data.Description,
                    Priority = entry.Info.Priority,
                    RequiredDepartment = _department,
                    XPReward = entry.Info.XPReward,
                    LocationX = callout.Data.LocationX,
                    LocationY = callout.Data.LocationY,
                    LocationZ = callout.Data.LocationZ,
                    Metadata = callout.Data.Metadata
                };

                var result = await RunCalloutAsync(callout, ct);

                var nextDelay = result switch
                {
                    CalloutResult.Completed => Settings.PostCompleteCooldownSeconds * 1000 + _dispatchIntervalMs,
                    CalloutResult.Declined => Settings.PostDeclineCooldownSeconds * 1000,
                    _ => Settings.PostFailCooldownSeconds * 1000 + _dispatchIntervalMs
                };

                if (!await Timing.TryWaitAsync(nextDelay, ct)) return;
            }
        }

        private static CalloutBase? CreateCallout(RegisteredCallout entry)
        {
            try
            {
                return (CalloutBase)Activator.CreateInstance(entry.Type);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CalloutDispatcher] Could not create {entry.Type.Name}: {ex.Message}");
                return null;
            }
        }

        private async Task<CalloutResult> RunCalloutAsync(CalloutBase callout, CancellationToken ct)
        {
            _busy = true;
            try
            {
                return await RunCalloutCoreAsync(callout, ct);
            }
            finally
            {
                _busy = false;
            }
        }

        private async Task<CalloutResult> RunCalloutCoreAsync(CalloutBase callout, CancellationToken ct)
        {
            callout.SetState(CalloutState.Dispatching);

            var dispatchLoc = callout.GetDispatchLocation();
            ShowDispatchNotification(callout, dispatchLoc);
            var previewBlip = CreatePreviewBlip(dispatchLoc, callout.Data);

            AcceptPressed = false;
            DeclinePressed = false;

            var accepted = await RunAcceptWindowAsync(ct);

            previewBlip?.Delete();

            if (!accepted)
            {
                try { callout.OnCalloutDeclined(); }
                catch (Exception ex) { Debug.WriteLine($"[CalloutDispatcher] OnCalloutDeclined threw: {ex.Message}"); }
                callout.Cleanup();
                callout.SetState(CalloutState.Declined);

                if (!ct.IsCancellationRequested)
                    ClientBrain.ShowNotification("~r~[ DISPATCH ]~w~ Callout declined.");
                return CalloutResult.Declined;
            }

            EndCalloutPressed = false;
            callout.SetState(CalloutState.Active);
            _activeCallout = callout;

            ClientBrain.ShowNotification($"~g~[ DISPATCH ]~w~ Callout accepted: ~b~{callout.Data.Name}");

            var finalResult = CalloutResult.Failed;
            using var calloutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            void OnEnded(CalloutBase c, CalloutResult result)
            {
                finalResult = result;
                if (!calloutCts.IsCancellationRequested) calloutCts.Cancel();
            }

            callout.Ended += OnEnded;

            var scenarioTask = RunScenarioAsync(callout, calloutCts.Token);
            await RunUpdateLoopAsync(callout, calloutCts.Token);
            await scenarioTask;

            callout.Ended -= OnEnded;

            if (callout.State == CalloutState.Active)
            {
                try { callout.OnCalloutFailed(); }
                catch (Exception ex) { Debug.WriteLine($"[CalloutDispatcher] OnCalloutFailed threw: {ex.Message}"); }
                callout.SetState(CalloutState.Failed);
                finalResult = CalloutResult.Failed;
            }

            callout.Cleanup(finalResult == CalloutResult.Completed);

            if (ReferenceEquals(_activeCallout, callout))
            {
                _activeCallout = null;
                if (!ct.IsCancellationRequested)
                    _onEnded(callout, finalResult);
            }

            Debug.WriteLine($"[CalloutDispatcher] '{callout.Data.Name}' ended: {finalResult}");
            return finalResult;
        }

        private static async Task<bool> RunAcceptWindowAsync(CancellationToken ct)
        {
            var end = API.GetGameTimer() + Settings.AcceptWindowSeconds * 1000;

            while (!ct.IsCancellationRequested)
            {
                if (AcceptPressed) return true;
                if (DeclinePressed) return false;

                var remainingMs = end - API.GetGameTimer();
                if (remainingMs <= 0) return false;

                API.BeginTextCommandDisplayHelp("STRING");
                API.AddTextComponentSubstringPlayerName(
                    $"~y~[ DISPATCH ]~w~ ~g~/er_accept~w~  or  ~r~/er_decline~w~ ({remainingMs / 1000 + 1}s)");
                API.EndTextCommandDisplayHelp(0, false, false, -1);

                await BaseScript.Delay(0);
            }

            return false;
        }

        private static async Task RunUpdateLoopAsync(CalloutBase callout, CancellationToken ct)
        {
            while (callout.State == CalloutState.Active && !ct.IsCancellationRequested)
            {
                if (EndCalloutPressed)
                {
                    EndCalloutPressed = false;
                    ClientBrain.ShowNotification("~o~[ DISPATCH ]~w~ Callout ended by officer.");
                    callout.SetState(CalloutState.Failed);
                    callout.RaiseEnded(CalloutResult.Failed);
                    return;
                }

                try { callout.OnUpdate(); }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CalloutDispatcher] OnUpdate exception in {callout.GetType().Name}: {ex.Message}");
                }

                if (!await Timing.TryWaitAsync(1000, ct)) return;
            }
        }

        private static async Task RunScenarioAsync(CalloutBase callout, CancellationToken ct)
        {
            try
            {
                await callout.OnCalloutAccepted(ct);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CalloutDispatcher] Unhandled exception in {callout.GetType().Name}.OnCalloutAccepted: {ex}");
                if (callout.State == CalloutState.Active)
                {
                    callout.SetState(CalloutState.Failed);
                    callout.RaiseEnded(CalloutResult.Failed);
                }
            }
        }

        private static void ShowDispatchNotification(CalloutBase callout, Vector3 dispatchLoc)
        {
            var data = callout.Data;
            var codeColor = data.Priority >= CalloutPriority.High ? "~r~" : "~o~";
            var distance = dispatchLoc != Vector3.Zero
                ? $"~s~Distance: ~w~{Vector3.Distance(Game.PlayerPed.Position, dispatchLoc):F0}m~n~"
                : "";

            ClientBrain.ShowNotification(
                $"~y~[ DISPATCH ]~w~  {codeColor}Code {(int)data.Priority}~w~  ~b~{data.Name}~n~" +
                $"{data.Description}~n~{distance}~g~/er_accept~w~   ~r~/er_decline");
        }

        private static Blip? CreatePreviewBlip(Vector3 location, CalloutData data)
        {
            if (location == Vector3.Zero) return null;

            var blip = World.CreateBlip(location);
            blip.Sprite = BlipSprite.PoliceStation;
            blip.Color = BlipColor.Yellow;
            blip.Alpha = 180;
            blip.Name = $"[DISPATCH] {data.Name}";
            blip.IsShortRange = false;
            blip.ShowRoute = true;
            return blip;
        }
    }
}
