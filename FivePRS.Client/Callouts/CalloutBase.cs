using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Client.Arrest;
using FivePRS.Client.Tasks;
using FivePRS.Core.Config;
using FivePRS.Core.Jurisdiction;
using FivePRS.Core.Models;

namespace FivePRS.Client.Callouts
{
    public abstract class CalloutBase
    {
        public CalloutData Data { get; internal set; } = new();

        public CalloutState State { get; private set; } = CalloutState.Idle;

        internal event Action<CalloutBase, CalloutResult>? Ended;

        private readonly List<Entity> _trackedEntities = new();
        private readonly List<Blip>   _trackedBlips    = new();

        protected Ped? TrackEntity(Ped? ped)
        {
            Track(ped);
            return ped;
        }

        protected Vehicle? TrackEntity(Vehicle? vehicle)
        {
            Track(vehicle);
            return vehicle;
        }

        protected Prop? TrackEntity(Prop? prop)
        {
            Track(prop);
            return prop;
        }

        private void Track(Entity? entity)
        {
            if (entity is not null)
                _trackedEntities.Add(entity);
        }

        protected Blip TrackBlip(Blip blip)
        {
            if (blip is null) throw new ArgumentNullException(nameof(blip));
            _trackedBlips.Add(blip);
            return blip;
        }

        public abstract Task OnCalloutAccepted(CancellationToken ct);

        public virtual void OnUpdate() { }

        public virtual void OnCalloutDeclined() { }

        public virtual void OnCalloutFailed() { }

        public virtual Vector3 GetDispatchLocation() =>
            new(Data.LocationX, Data.LocationY, Data.LocationZ);

        public virtual bool CanBeDispatched() => true;

        protected void CalloutCompleted()
        {
            if (State != CalloutState.Active) return;
            State = CalloutState.Completed;
            Ended?.Invoke(this, CalloutResult.Completed);
        }

        protected void CalloutFailed()
        {
            if (State != CalloutState.Active) return;
            State = CalloutState.Failed;
            Ended?.Invoke(this, CalloutResult.Failed);
        }

        protected static Vector3 PickLocation(IReadOnlyList<Vector3> locations, Random rng)
        {
            var map    = ConfigManager.Territories;
            var player = ClientBrain.LocalPlayerData;
            var agency = map.ResolveAgency(player.Department, player.Agency);

            var local = locations
                .Where(l => TerritoryMap.IsInJurisdiction(agency, map.Resolve(l.X, l.Y)?.Id))
                .ToList();

            var pool = local.Count > 0 ? local : locations;
            return pool[rng.Next(pool.Count)];
        }

        protected async Task WaitForArrestAsync(Ped suspect, CancellationToken ct, string prompt = "Type ~b~/er_cuff~w~ to arrest the suspect")
        {
            const int PollMs = 250;

            ArrestManager.RegisterSuspect(suspect);

            while (true)
            {
                if (ArrestManager.IsInCustody(suspect))
                {
                    ClientBrain.ShowNotification("~g~Suspect in custody~w~ | Callout complete.");
                    CalloutCompleted();
                    return;
                }

                if (!suspect.Exists() || suspect.IsDead)
                {
                    ClientBrain.ShowNotification("~g~Suspect down~w~ | Callout complete.");
                    CalloutCompleted();
                    return;
                }

                ClientBrain.ShowHelp(prompt, PollMs + 50);
                await Timing.WaitAsync(PollMs, ct);
            }
        }

        protected async Task FootPursuitAsync(Ped suspect, float catchDistM, float escapeDistM, CancellationToken ct)
        {
            const int PollMs = 400;

            suspect.BlockPermanentEvents = true;
            API.SetPedFleeAttributes(suspect.Handle, 0, false);
            API.SetPedCombatAttributes(suspect.Handle, 17, true);
            await TaskManager.AssignTaskAsync(suspect, PedTaskType.FleeFromPlayer);

            ClientBrain.ShowNotification("~r~Suspect fleeing on foot~w~ | Pursue and arrest!");

            while (true)
            {
                await Timing.WaitAsync(PollMs, ct);

                if (!suspect.Exists() || suspect.IsDead)
                {
                    ClientBrain.ShowNotification("~g~Suspect down~w~ | Callout complete.");
                    CalloutCompleted();
                    return;
                }

                var dist = Vector3.Distance(Game.PlayerPed.Position, suspect.Position);

                if (dist <= catchDistM)
                {
                    await TaskManager.AssignTaskAsync(suspect, PedTaskType.PutHandsUp);
                    ClientBrain.ShowNotification("~g~Suspect cornered~w~ | Type ~b~/er_cuff~w~ to arrest.");
                    await WaitForArrestAsync(suspect, ct);
                    return;
                }

                if (dist > escapeDistM)
                {
                    ClientBrain.ShowNotification("~r~Suspect escaped~w~ | Callout failed.");
                    CalloutFailed();
                    return;
                }

                if (dist < 80f)
                    ClientBrain.ShowHelp($"~r~Suspect on foot~w~ ~y~{dist:F0}m~w~ away", PollMs + 50);
            }
        }

        internal void SetState(CalloutState state) => State = state;

        internal void RaiseEnded(CalloutResult result) => Ended?.Invoke(this, result);

        internal void Cleanup(bool releaseToWorld = false)
        {
            foreach (var entity in _trackedEntities)
            {
                try
                {
                    if (!entity.Exists()) continue;

                    if (entity is Ped ped)
                    {
                        if (ArrestManager.IsInCustody(ped)) continue;
                        ArrestManager.UnregisterSuspect(ped);
                    }

                    if (releaseToWorld)
                    {
                        var handle = entity.Handle;
                        API.SetEntityAsNoLongerNeeded(ref handle);
                    }
                    else
                    {
                        entity.Delete();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CalloutBase] Entity cleanup failed: {ex.Message}");
                }
            }
            _trackedEntities.Clear();

            foreach (var blip in _trackedBlips)
            {
                try { blip.Delete(); }
                catch (Exception ex) { Debug.WriteLine($"[CalloutBase] Blip cleanup failed: {ex.Message}"); }
            }
            _trackedBlips.Clear();
        }
    }
}
