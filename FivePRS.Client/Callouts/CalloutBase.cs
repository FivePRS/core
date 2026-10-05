using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Client.Arrest;
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

        protected T? TrackEntity<T>(T? entity) where T : Entity
        {
            if (entity is not null)
                _trackedEntities.Add(entity);
            return entity;
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
