using System.Collections.Generic;
using CitizenFX.Core;
using FivePRS.Core.Models;

namespace FivePRS.Client.Terminal
{
    public abstract class TerminalApp : BaseScript
    {
        protected TerminalApp()
        {
            TerminalApps.Register(this);
        }

        public abstract string Id { get; }

        public abstract string Label { get; }

        public virtual string Icon => string.Empty;

        public virtual string Color => "#334155";

        public virtual int Order => 500;

        public virtual string? Page => null;

        public virtual string? Badge => null;

        public virtual bool IsAvailable(PlayerData player) => true;

        public virtual AppScreen? BuildScreen() => null;

        public virtual void OnAction(string action, IDictionary<string, object> data)
        {
        }

        public virtual void OnOpened()
        {
        }

        public virtual void OnClosed()
        {
        }

        protected static PlayerData Player => ClientBrain.LocalPlayerData;

        protected void Refresh() => TerminalApps.NotifyChanged();

        protected void Send(string type, object? payload) => TerminalApps.SendToApp(Id, type, payload);
    }
}
