using System.Collections.Generic;
using FivePRS.Client.App;
using FivePRS.Core.Models;

namespace FivePRS.Client.Terminal.Apps
{
    public class RecordsApp : TerminalApp
    {
        private const string AppId = "records";

        public override string Id => AppId;

        public override string Label => "Records";

        public override string Color => "#4f46e5";

        public override string Icon =>
            "M3 6a1 1 0 0 1 1-1h5l2 2h9a1 1 0 0 1 1 1v10a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1z M14.5 14.5l2.5 2.5 M13 15a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5z";

        public override int Order => 20;

        public override string? Page => "nui/apps/records/index.html";

        internal static void ShowError(string message) => TerminalApps.SendToApp(AppId, "error", message);

        public override bool IsAvailable(PlayerData player) => RecordsPanel.IsAvailable;

        public override object? BuildState() => RecordsPanel.BuildView();

        public override void OnAction(string action, IDictionary<string, object> data)
        {
            switch (action)
            {
                case "searchName":
                    RecordsPanel.SearchName(data);
                    break;

                case "searchPlate":
                    RecordsPanel.SearchPlate(data);
                    break;

                case "open":
                    RecordsPanel.Open(data);
                    break;

                case "back":
                    RecordsPanel.CloseRecord();
                    break;

                case "issue":
                    RecordsPanel.Issue(data);
                    break;

                case "resolve":
                    RecordsPanel.Resolve(data);
                    break;

                case "license":
                    RecordsPanel.SetLicense(data);
                    break;

                case "vehicle":
                    RecordsPanel.FlagVehicle(data);
                    break;
            }
        }
    }
}
