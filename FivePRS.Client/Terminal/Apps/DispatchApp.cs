using System.Collections.Generic;
using FivePRS.Client.App;
using FivePRS.Client.Callouts;
using FivePRS.Core.Models;

namespace FivePRS.Client.Terminal.Apps
{
    public class DispatchApp : TerminalApp
    {
        public override string Id => "dispatch";

        public override string Label => "Dispatch";

        public override string Color => "#e81010";

        public override string Icon =>
            "M12 10a2 2 0 1 1 0 4 2 2 0 0 1 0-4z M7.8 7.8a6 6 0 0 0 0 8.4 M16.2 7.8a6 6 0 0 1 0 8.4 M4.9 4.9a10 10 0 0 0 0 14.2 M19.1 4.9a10 10 0 0 1 0 14.2";

        public override int Order => 10;

        public override string? Page => "nui/apps/dispatch/index.html";

        public override string? Badge =>
            CalloutDispatcher.PendingOffer is not null ? "!" :
            CalloutDispatcher.ActiveCall is not null ? "1" : null;

        public override bool IsAvailable(PlayerData player) => DispatchPanel.IsAvailable;

        public override object? BuildState() => DispatchPanel.BuildView();

        public override void OnAction(string action, IDictionary<string, object> data)
        {
            switch (action)
            {
                case "status":
                    DispatchPanel.SetStatus(data);
                    break;

                case "attach":
                    DispatchPanel.Attach(data);
                    break;

                case "clear":
                    DispatchPanel.ClearCall(data);
                    break;

                case "waypoint":
                    DispatchPanel.Waypoint(data);
                    break;

                case "accept":
                    CalloutDispatcher.AcceptOffer();
                    break;

                case "decline":
                    CalloutDispatcher.DeclineOffer();
                    break;

                case "end":
                    CalloutDispatcher.EndActiveCall();
                    break;

                case "backup":
                    DispatchPanel.RequestBackup(data);
                    break;
            }
        }
    }
}
