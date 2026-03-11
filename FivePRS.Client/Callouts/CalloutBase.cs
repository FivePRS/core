using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CitizenFX.Core;
using FivePRS.Core.Models;

namespace FivePRS.Client.Callouts
{
    /// <summary>
    /// Abstract base for every FivePRS callout regardless of department.
    ///
    /// Key improvements over FivePD's callout base:
    /// ─ <see cref="TrackEntity{T}"/> / <see cref="TrackBlip"/> auto-delete all spawned
    ///   entities and blips on cleanup — callout authors can never leak world objects.
    /// ─ <see cref="GetDispatchLocation"/> gives the dispatcher a location for the
    ///   pre-accept preview blip without starting the scenario.
    /// ─ <see cref="CanBeDispatched"/> lets callouts gate themselves on time, weather,
    ///   distance from player, or any other runtime condition.
    /// ─ <see cref="OnUpdate"/> is called every second while Active by the dispatcher —
    ///   no manual Tick wiring required.
    /// ─ Formal <see cref="CalloutState"/> makes the lifecycle always queryable.
    ///
    /// Callout classes must be decorated with <see cref="CalloutInfoAttribute"/> and
    /// have a public parameterless constructor so the dispatcher can instantiate them
    /// via Activator.CreateInstance.
    /// </summary>
    public abstract class CalloutBase
    {

        public CalloutData Data { get; internal set; } = new();

        public CalloutState State { get; private set; } = CalloutState.Idle;

        internal event Action<CalloutBase, CalloutResult>? Ended;

        private readonly List<Entity> _trackedEntities = new();
        private readonly List<Blip>   _trackedBlips    = new();

        protected T TrackEntity<T>(T entity) where T : Entity
        {
            if (entity is not null)
                _trackedEntities.Add(entity);
            return entity;
        }

        protected Blip TrackBlip(Blip blip)
        {
            if (blip is not null)
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

        internal void Cleanup()
        {
            foreach (var entity in _trackedEntities)
            {
                try
                {
                    if (entity?.Exists() == true)
                        entity.Delete();
                }
                catch { }
            }
            _trackedEntities.Clear();

            foreach (var blip in _trackedBlips)
            {
                try { blip?.Delete(); }
                catch { }
            }
            _trackedBlips.Clear();
        }
    }
}
