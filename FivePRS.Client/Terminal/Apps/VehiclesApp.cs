using System.Collections.Generic;
using FivePRS.Client.App;

namespace FivePRS.Client.Terminal.Apps
{
    public class VehiclesApp : TerminalApp
    {
        public override string Id => "vehicles";

        public override string Label => "Vehicles";

        public override string Color => "#475569";

        public override string Icon =>
            "M5 17H3v-4l2-5h14l2 5v4h-2 M3 13h18 M7.5 19a2 2 0 1 0 0-4 2 2 0 0 0 0 4z M16.5 19a2 2 0 1 0 0-4 2 2 0 0 0 0 4z";

        public override int Order => 50;

        public override string? Page => "nui/apps/vehicles/index.html";

        public override object BuildState() => CivilianPanel.BuildView();

        public override void OnAction(string action, IDictionary<string, object> data)
        {
            switch (action)
            {
                case "register":
                    CivilianPanel.RegisterVehicle();
                    break;
                case "stolen":
                    CivilianPanel.SetVehicleStolen(data);
                    break;
                case "remove":
                    CivilianPanel.RemoveVehicle(data);
                    break;
            }
        }
    }
}
