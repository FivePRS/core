using System.Collections.Generic;
using FivePRS.Client.App;
using FivePRS.Core.Models;

namespace FivePRS.Client.Terminal.Apps
{
    public class AdminApp : TerminalApp
    {
        public override string Id => "admin";

        public override string Label => "Admin";

        public override string Color => "#7c3aed";

        public override string Icon =>
            "M4 21v-7 M4 10V3 M12 21v-9 M12 8V3 M20 21v-5 M20 12V3 M1 14h6 M9 8h6 M17 16h6";

        public override int Order => 90;

        public override string? Page => "nui/apps/admin/index.html";

        public override bool IsAvailable(PlayerData player) => AdminPanel.IsAvailable;

        public override void OnOpened() => AdminPanel.Request();

        public override object? BuildState() => AdminPanel.BuildView();

        public override void OnAction(string action, IDictionary<string, object> data)
        {
            switch (action)
            {
                case "search":
                    AdminPanel.Search(data);
                    break;

                case "roster":
                    AdminPanel.SetRoster(data);
                    break;

                case "action":
                    AdminPanel.Act(data);
                    break;
            }
        }
    }
}
