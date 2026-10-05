using System;
using System.Threading;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Core.Config;
using FivePRS.Core.Events;
using FivePRS.Core.Models;
using Newtonsoft.Json;

namespace FivePRS.Client.Callouts
{
    public sealed class CalloutDispatcher
    {
        internal static volatile bool AcceptPressed;
        internal static volatile bool DeclinePressed;
        internal static volatile bool EndCalloutPressed;

        private readonly Department _department;
        private readonly CalloutRegistry _registry;
        private readonly Action<CalloutBase, CalloutResult> _onEnded;

        private CancellationTokenSource? _cts;
        private CalloutBase? _activeCallout;
        private bool _busy;

        public bool IsRunning => _cts is not null;
        public bool HasActiveCallout => _activeCallout is not null;

        public CalloutDispatcher(
            Department department,
            CalloutRegistry registry,
            Action<CalloutBase, CalloutResult> onEnded)
        {
            _department = department;
            _registry = registry;
            _onEnded = onEnded;
        }

        public void Start()
        {
            if (_cts is not null) return;
            _cts = new CancellationTokenSource();

            var definitions = _registry.GetDefinitions(_department);
            BaseScript.TriggerServerEvent(EventNames.ServerRegisterCallouts, JsonConvert.SerializeObject(definitions));
            Debug.WriteLine($"[CalloutDispatcher] Registered {definitions.Count} {_department} callout(s) with dispatch.");
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

        public async Task HandleOfferAsync(CalloutData data)
        {
            if (_cts is null || _busy)
            {
                Respond(data.Id, OfferResponse.Unavailable, Vector3.Zero);
                return;
            }

            var callout = CreateCallout(data);
            if (callout is null)
            {
                Respond(data.Id, OfferResponse.Unavailable, Vector3.Zero);
                return;
            }

            _busy = true;
            try
            {
                await RunCalloutAsync(callout, _cts.Token);
            }
            finally
            {
                _busy = false;
            }
        }

        private CalloutBase? CreateCallout(CalloutData data)
        {
            var entry = _registry.FindByName(data.Name);
            if (entry is null)
            {
                Debug.WriteLine($"[CalloutDispatcher] No handler registered for '{data.Name}'.");
                return null;
            }

            try
            {
                var callout = (CalloutBase)Activator.CreateInstance(entry.Type);
                if (!callout.CanBeDispatched()) return null;

                if (string.IsNullOrEmpty(data.Description))
                    data.Description = callout.Data.Description;
                callout.Data = data;
                return callout;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CalloutDispatcher] Could not create {entry.Type.Name}: {ex.Message}");
                return null;
            }
        }

        private async Task RunCalloutAsync(CalloutBase callout, CancellationToken ct)
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
                Respond(callout.Data.Id, OfferResponse.Declined, Vector3.Zero);

                try { callout.OnCalloutDeclined(); }
                catch (Exception ex) { Debug.WriteLine($"[CalloutDispatcher] OnCalloutDeclined threw: {ex.Message}"); }
                callout.Cleanup();
                callout.SetState(CalloutState.Declined);

                if (!ct.IsCancellationRequested)
                    ClientBrain.ShowNotification("~r~[ DISPATCH ]~w~ Callout declined.");
                return;
            }

            Respond(callout.Data.Id, OfferResponse.Accepted, dispatchLoc);

            EndCalloutPressed = false;
            callout.SetState(CalloutState.Active);
            _activeCallout = callout;

            ClientBrain.ShowNotification($"~g~[ DISPATCH ]~w~ Call ~y~#{callout.Data.Id}~w~ accepted: ~b~{callout.Data.Name}");

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
                {
                    BaseScript.TriggerServerEvent(EventNames.ServerCalloutEnded, callout.Data.Id, (int)finalResult);
                    _onEnded(callout, finalResult);
                }
            }

            Debug.WriteLine($"[CalloutDispatcher] Call #{callout.Data.Id} '{callout.Data.Name}' ended: {finalResult}");
        }

        private static void Respond(string callId, OfferResponse response, Vector3 location) =>
            BaseScript.TriggerServerEvent(EventNames.ServerCalloutResponse, callId, (int)response, location.X, location.Y, location.Z);

        private static async Task<bool> RunAcceptWindowAsync(CancellationToken ct)
        {
            var end = API.GetGameTimer() + ConfigManager.Settings.AcceptWindowSeconds * 1000;

            while (!ct.IsCancellationRequested)
            {
                if (AcceptPressed) return true;
                if (DeclinePressed) return false;

                var remainingMs = end - API.GetGameTimer();
                if (remainingMs <= 0) return false;

                ClientBrain.ShowHelp($"~y~[ DISPATCH ]~w~ ~g~/er_accept~w~  or  ~r~/er_decline~w~ ({remainingMs / 1000 + 1}s)", -1);

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
                $"~y~[ DISPATCH ]~w~  {codeColor}Code {(int)data.Priority}~w~  ~b~{data.Name}~w~ ~y~#{data.Id}~n~" +
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
