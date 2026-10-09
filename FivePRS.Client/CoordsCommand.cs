using System;
using System.Collections.Generic;
using System.Globalization;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using Newtonsoft.Json;

namespace FivePRS.Client
{
    public class CoordsCommand : BaseScript
    {
        private static readonly string[] PointNames = { "Armory", "Locker", "Garage" };

        public CoordsCommand()
        {
            API.RegisterCommand("fprs_coords", new Action<int, List<object>, string>((_, args, __) =>
                Capture(args.Count > 0 ? args[0]?.ToString() : null)), false);
        }

        private static void Capture(string? point)
        {
            var pointName = Array.Find(PointNames, p => string.Equals(p, point, StringComparison.OrdinalIgnoreCase));

            var ped       = Game.PlayerPed;
            var vehicle   = ped.CurrentVehicle;
            var inVehicle = vehicle is not null && vehicle.Exists();
            var entity    = inVehicle ? (Entity)vehicle! : ped;

            var x = Format(entity.Position.X);
            var y = Format(entity.Position.Y);
            var z = Format(entity.Position.Z);
            var h = Format(entity.Heading);

            string label, text;
            if (inVehicle)
            {
                label = "Parking spot";
                text  = $"[{x}, {y}, {z}, {h}]";
            }
            else if (pointName is not null)
            {
                label = $"{pointName} point";
                text  = $"\"{pointName.ToLowerInvariant()}\": [{x}, {y}, {z}]";
            }
            else
            {
                label = "Duty point";
                text  = $"\"dutyPoint\": [{x}, {y}, {z}], \"heading\": {h}";
            }

            Debug.WriteLine($"[FivePRS] {label}: {text}");
            API.SendNuiMessage(JsonConvert.SerializeObject(new { screen = "clipboard", type = "copy", payload = text }));
            ClientBrain.ShowNotification($"~b~{label}~w~ copied to clipboard~n~{x}, {y}, {z}~n~Heading {h}", "Coordinates");
        }

        private static string Format(float value) =>
            Math.Round(value, 2).ToString("0.0#", CultureInfo.InvariantCulture);
    }
}
