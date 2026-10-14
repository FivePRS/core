using System.Collections.Generic;
using FivePRS.Client.App;

namespace FivePRS.Client.Terminal.Apps
{
    public class LicensesApp : TerminalApp
    {
        public override string Id => "licenses";

        public override string Label => "Licenses";

        public override string Color => "#d97706";

        public override string Icon =>
            "M3 6a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z M8.5 14a2 2 0 1 0 0-4 2 2 0 0 0 0 4z M5.5 17a3 3 0 0 1 6 0 M14 10h4 M14 14h3";

        public override int Order => 40;

        public override string? Page => "nui/apps/licenses/index.html";

        public override object BuildState() => CivilianPanel.BuildView();

        public override void OnAction(string action, IDictionary<string, object> data)
        {
            if (action == "apply") CivilianPanel.ApplyLicense(data);
            else if (action == "show") CivilianPanel.ShowLicense(data);
        }
    }
}
