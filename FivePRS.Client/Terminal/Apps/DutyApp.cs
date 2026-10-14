using System.Collections.Generic;
using FivePRS.Client.App;

namespace FivePRS.Client.Terminal.Apps
{
    public class DutyApp : TerminalApp
    {
        public const string AppId = "duty";

        public override string Id => AppId;

        public override string Label => "Duty";

        public override string Color => "#335cff";

        public override string Icon =>
            "M12 2l8 3v6c0 5.2-3.4 9.4-8 11-4.6-1.6-8-5.8-8-11V5z M12 8l1.2 2.6 2.8.3-2.1 1.9.6 2.8L12 14.2l-2.5 1.4.6-2.8-2.1-1.9 2.8-.3z";

        public override int Order => 0;

        public override string? Page => "nui/apps/duty/index.html";

        internal static void ShowError(string message) => TerminalApps.SendToApp(AppId, "error", message);

        public override object BuildState() => DutyPanel.BuildView();

        public override void OnAction(string action, IDictionary<string, object> data)
        {
            switch (action)
            {
                case "enter":
                    var error = DutyPanel.Enter(data);
                    if (error is not null) ShowError(error);
                    break;

                case "off":
                    DutyPanel.GoOffDuty();
                    break;
            }
        }
    }
}
