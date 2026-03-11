using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Core.Events;
using FivePRS.Core.Models;

namespace FivePRS.Client.Callouts
{
    /// <summary>
    /// Orchestrates the full callout lifecycle for a single department:
    ///
    ///   Idle → (dispatch interval) → Dispatching → Accept/Decline → Active
    ///       → Completed / Failed / Declined → (cooldown) → back to Idle
    ///
    /// One instance per agency. Owned and started/stopped by the agency's OnDuty/OffDuty.
    ///
    /// Design notes:
    /// ─ Entirely task-based; never holds a game-thread Tick.
    /// ─ Accept/decline is driven by /er_accept and /er_decline commands (registered once
    ///   in ClientBrain) via two static volatile flags read in the accept window loop.
    /// ─ Server-dispatched callouts bypass the dispatch interval and jump straight to
    ///   the accept window via HandleServerCalloutAsync.
    /// ─ OnUpdate() is called once per second while Active; scenario logic runs concurrently
    ///   as a separate detached Task.
    /// </summary>
    public sealed class CalloutDispatcher
    {
        internal static volatile bool AcceptPressed     = false;
        internal static volatile bool DeclinePressed    = false;
        internal static volatile bool EndCalloutPressed = false;

        private static int AcceptWindowSeconds    => FivePRS.Core.Config.ConfigManager.Settings.AcceptWindowSeconds;
        private static int PostCompleteCooldownMs => FivePRS.Core.Config.ConfigManager.Settings.PostCompleteCooldownSeconds * 1000;
        private static int PostDeclineCooldownMs  => FivePRS.Core.Config.ConfigManager.Settings.PostDeclineCooldownSeconds * 1000;
        private static int PostFailCooldownMs     => FivePRS.Core.Config.ConfigManager.Settings.PostFailCooldownSeconds * 1000;
        private static int NoCalloutRetryMs       => FivePRS.Core.Config.ConfigManager.Settings.NoCalloutRetrySeconds * 1000;
        private static int InitialDelayMs         => FivePRS.Core.Config.ConfigManager.Settings.InitialGraceSeconds * 1000;

        private readonly Department                      _department;
        private readonly CalloutRegistry                 _registry;
        private readonly int                             _dispatchIntervalMs;
        private readonly Action<CalloutBase, CalloutResult> _onEnded;

        private CancellationTokenSource? _cts;
        private CancellationTokenSource? _calloutCts;
        private CalloutBase?             _activeCallout;
        private bool                     _running;

        public bool HasActiveCallout => _activeCallout is not null;

        public CalloutDispatcher(
            Department department,
            CalloutRegistry registry,
            int dispatchIntervalMs,
            Action<CalloutBase, CalloutResult> onEnded)
        {
            _department          = department;
            _registry            = registry;
            _dispatchIntervalMs  = dispatchIntervalMs;
            _onEnded             = onEnded;
        }

        public void EndActiveCallout()
        {
            if (_activeCallout is null) return;
            EndCalloutPressed = true;
        }

        public void Start()
        {
            if (_running) return;
            _running = true;
            _cts     = new CancellationTokenSource();
            _ = DispatchLoopAsync(_cts.Token);
        }

        public void Stop()
        {
            _running = false;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            if (_activeCallout is not null)
            {
                try { _activeCallout.OnCalloutFailed(); } catch { }
                _activeCallout.Cleanup();
                _activeCallout.SetState(CalloutState.Failed);
                _activeCallout = null;
            }
        }

        public async Task HandleServerCalloutAsync(CalloutData data)
        {
            if (_activeCallout is not null)
            {
                Debug.WriteLine($"[CalloutDispatcher] Server callout '{data.Name}' dropped — already active.");
                return;
            }

            var (type, info) = _registry.FindByName(data.Name);
            if (type is null)
            {
                Debug.WriteLine($"[CalloutDispatcher] No handler registered for server callout '{data.Name}'.");
                return;
            }

            CalloutBase callout;
            try { callout = (CalloutBase)Activator.CreateInstance(type)!; }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CalloutDispatcher] Could not create {type.Name}: {ex.Message}");
                return;
            }

            callout.Data = data;
            var ct = _cts?.Token ?? CancellationToken.None;
            await RunCalloutAsync(callout, ct);
        }

        private async Task DispatchLoopAsync(CancellationToken ct)
        {
            await SafeDelay(InitialDelayMs, ct);

            while (!ct.IsCancellationRequested)
            {
                var (type, info) = _registry.PickCallout(_department);

                if (type is null)
                {
                    Debug.WriteLine("[CalloutDispatcher] No callouts available; retrying in 30s.");
                    await SafeDelay(NoCalloutRetryMs, ct);
                    continue;
                }

                CalloutBase callout;
                try { callout = (CalloutBase)Activator.CreateInstance(type)!; }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CalloutDispatcher] Failed to create {type.Name}: {ex.Message}");
                    await SafeDelay(NoCalloutRetryMs, ct);
                    continue;
                }

                callout.Data = new CalloutData
                {
                    Id                 = Guid.NewGuid().ToString(),
                    Name               = info.Name,
                    Description        = callout.Data.Description,
                    Priority           = info.Priority,
                    RequiredDepartment = _department,
                    XPReward           = info.XPReward
                };

                var cooldown = await RunCalloutAsync(callout, ct);

                if (ct.IsCancellationRequested) break;

                var nextDelay = cooldown switch
                {
                    CalloutResult.Completed => PostCompleteCooldownMs + _dispatchIntervalMs,
                    CalloutResult.Declined  => PostDeclineCooldownMs,
                    CalloutResult.Failed    => PostFailCooldownMs + _dispatchIntervalMs,
                    _                       => _dispatchIntervalMs
                };

                await SafeDelay(nextDelay, ct);
            }
        }

        private async Task<CalloutResult> RunCalloutAsync(CalloutBase callout, CancellationToken ct)
        {
            callout.SetState(CalloutState.Dispatching);

            ShowDispatchNotification(callout);

            var dispatchLoc  = callout.GetDispatchLocation();
            var previewBlip  = CreatePreviewBlip(dispatchLoc, callout.Data);

            AcceptPressed  = false;
            DeclinePressed = false;

            var accepted = await RunAcceptWindowAsync(callout.Data, ct);

            previewBlip?.Delete();

            if (!accepted || ct.IsCancellationRequested)
            {
                try { callout.OnCalloutDeclined(); } catch { }
                callout.Cleanup();
                callout.SetState(CalloutState.Declined);

                ClientBrain.ShowNotification("~r~[ DISPATCH ]~w~ Callout declined.");
                Debug.WriteLine($"[CalloutDispatcher] '{callout.Data.Name}' declined.");
                return CalloutResult.Declined;
            }

            callout.SetState(CalloutState.Active);
            _activeCallout = callout;

            ClientBrain.ShowNotification(
                $"~g~[ DISPATCH ]~w~ Callout accepted — ~b~{callout.Data.Name}");

            _calloutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            using var calloutCts = _calloutCts;

            CalloutResult finalResult = CalloutResult.Failed;

            callout.Ended += (c, result) =>
            {
                finalResult = result;
                calloutCts.Cancel();
                _onEnded(c, result);
                _activeCallout = null;
            };

            var scenarioTask = SafeRunScenario(callout, calloutCts.Token);
            await RunUpdateLoopAsync(callout, calloutCts.Token);

            try { await scenarioTask; }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Debug.WriteLine($"[CalloutDispatcher] Scenario error: {ex.Message}"); }

            if (callout.State == CalloutState.Active)
            {
                finalResult = CalloutResult.Failed;
                try { callout.OnCalloutFailed(); } catch { }
                callout.SetState(CalloutState.Failed);
                _activeCallout = null;
            }

            callout.Cleanup();

            Debug.WriteLine($"[CalloutDispatcher] '{callout.Data.Name}' ended → {finalResult}");
            return finalResult;
        }

        private async Task<bool> RunAcceptWindowAsync(CalloutData data, CancellationToken ct)
        {
            const int PollMs     = 500;
            var totalPolls       = (AcceptWindowSeconds * 1000) / PollMs;

            for (var i = 0; i < totalPolls; i++)
            {
                if (ct.IsCancellationRequested) return false;
                if (AcceptPressed)  return true;
                if (DeclinePressed) return false;

                var secondsLeft = AcceptWindowSeconds - (i * PollMs / 1000);
                API.BeginTextCommandDisplayHelp("STRING");
                API.AddTextComponentSubstringPlayerName(
                    $"~y~[ DISPATCH ]~w~ ~g~/er_accept~w~  or  ~r~/er_decline~w~ " +
                    $"(~w~{secondsLeft}s~w~)");
                API.EndTextCommandDisplayHelp(0, false, true, -1);

                await Task.Delay(PollMs);
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
                    if (callout.State == CalloutState.Active)
                    {
                        callout.SetState(CalloutState.Failed);
                        callout.RaiseEnded(CalloutResult.Failed);
                    }
                    break;
                }

                try { callout.OnUpdate(); }
                catch (Exception ex)
                {
                    Debug.WriteLine(
                        $"[CalloutDispatcher] OnUpdate exception in {callout.GetType().Name}: {ex.Message}");
                }
                try { await Task.Delay(1_000, ct); }
                catch (OperationCanceledException) { break; }
            }
        }

        private static async Task SafeRunScenario(CalloutBase callout, CancellationToken ct)
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
                Debug.WriteLine(
                    $"[CalloutDispatcher] Unhandled exception in {callout.GetType().Name}." +
                    $"OnCalloutAccepted: {ex}");
                if (callout.State == CalloutState.Active)
                    callout.SetState(CalloutState.Failed);
            }
        }

        private static void ShowDispatchNotification(CalloutBase callout)
        {
            var data       = callout.Data;
            var priority   = data.Priority;
            var codeColor  = priority >= CalloutPriority.High ? "~r~" : "~o~";
            var codeLabel  = $"{codeColor}Code {(int)priority}~w~";
            var dispatchLoc = callout.GetDispatchLocation();
            var hasLocation = dispatchLoc != Vector3.Zero;

            API.SetNotificationTextEntry("STRING");
            API.AddTextComponentSubstringPlayerName(
                $"~y~[ DISPATCH ]~w~  {codeLabel}  ~b~{data.Name}~n~" +
                $"{data.Description}~n~" +
                (hasLocation ? $"~s~Distance: ~w~{Vector3.Distance(Game.PlayerPed.Position, dispatchLoc):F0}m~n~" : "") +
                $"  ~g~/er_accept~w~   ~r~/er_decline");

            API.DrawNotification(false, true);
        }

        private static Blip? CreatePreviewBlip(Vector3 location, CalloutData data)
        {
            if (location == Vector3.Zero) return null;

            var blip           = World.CreateBlip(location);
            blip.Sprite        = BlipSprite.PoliceStation;
            blip.Color         = BlipColor.Yellow;
            blip.Alpha         = 180;
            blip.Name          = $"[DISPATCH] {data.Name}";
            blip.IsShortRange  = false;
            blip.ShowRoute     = true;
            return blip;
        }

        private static async Task SafeDelay(int ms, CancellationToken ct)
        {
            try { await Task.Delay(ms, ct); }
            catch (OperationCanceledException) { }
        }
    }
}
