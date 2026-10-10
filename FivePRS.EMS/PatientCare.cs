using System;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Client;
using FivePRS.Client.Stations;
using FivePRS.Core.Config;

namespace FivePRS.EMS
{
    public enum PatientStage
    {
        Injured,
        Treating,
        Stable,
        Loaded,
        Delivered,
    }

    public sealed class PatientCare : IDisposable
    {
        private const float TreatRange = 2.2f;
        private const float LoadRange = 8f;
        private const float DeliverRange = 30f;
        private const int TreatMs = 8_000;
        private const int EmergencyClass = 18;

        private const string InjuredDict = "combat@damage@writhe";
        private const string InjuredClip = "writhe_loop";
        private const string StableDict = "amb@world_human_bum_slumped@male@laying_on_left_side@base";
        private const string StableClip = "base";

        private static readonly int[] RearSeats = { 1, 2, 0 };

        private readonly Ped _patient;
        private Blip? _hospitalBlip;
        private StationDef? _hospital;
        private Vehicle? _transport;

        public PatientCare(Ped patient)
        {
            _patient = patient;
            _patient.BlockPermanentEvents = true;
            _patient.IsInvincible = true;
            _ = Animations.PlayAsync(_patient, InjuredDict, InjuredClip);
            Interactions.Register(TryInteract);
        }

        public PatientStage Stage { get; private set; } = PatientStage.Injured;

        public Ped Patient => _patient;

        public string? Prompt()
        {
            if (!_patient.Exists() || (Game.PlayerPed.IsInVehicle() && Stage != PatientStage.Loaded)) return null;

            var distance = Vector3.Distance(Game.PlayerPed.Position, _patient.Position);
            return Stage switch
            {
                PatientStage.Injured when distance <= TreatRange => $"{Binds.Interact} Treat the patient",
                PatientStage.Stable when distance <= LoadRange && FindTransport() is { } vehicle =>
                    $"{Binds.Interact} Load the patient into the {VehicleName(vehicle)}",
                PatientStage.Stable when distance <= LoadRange => "Bring an ambulance closer to load the patient",
                PatientStage.Loaded when _hospital is not null => $"Take the patient to ~y~{_hospital.Name}",
                _ => null,
            };
        }

        public bool Update()
        {
            if (Stage != PatientStage.Loaded || _hospital is null || _transport is null || !_transport.Exists()) return Stage == PatientStage.Delivered;

            var atHospital = Vector3.Distance(_transport.Position, StationService.PositionOf(_hospital)) <= DeliverRange;
            if (!atHospital || _transport.Speed > 1f) return false;

            Stage = PatientStage.Delivered;
            _hospitalBlip?.Delete();
            _patient.Task.LeaveVehicle(_transport, false);
            ClientBrain.ShowNotification($"Patient handed over to ~y~{_hospital.Name}~w~.", "~b~EMS");
            return true;
        }

        private bool TryInteract()
        {
            if (!_patient.Exists() || Game.PlayerPed.IsInVehicle()) return false;

            var distance = Vector3.Distance(Game.PlayerPed.Position, _patient.Position);
            if (Stage == PatientStage.Injured && distance <= TreatRange)
            {
                _ = TreatAsync();
                return true;
            }

            if (Stage == PatientStage.Stable && distance <= LoadRange && FindTransport() is { } vehicle)
            {
                Load(vehicle);
                return true;
            }

            return false;
        }

        private async Task TreatAsync()
        {
            Stage = PatientStage.Treating;
            Game.PlayerPed.Task.TurnTo(_patient);

            var treated = await Treatment.RunAsync(() => _patient.Exists() ? _patient.Position : (Vector3?)null, TreatMs, "Treating the patient");
            if (!treated)
            {
                Stage = PatientStage.Injured;
                return;
            }

            _patient.Task.ClearAll();
            await Animations.PlayAsync(_patient, StableDict, StableClip);
            Stage = PatientStage.Stable;
            ClientBrain.ShowNotification("Patient ~g~stabilized~w~. Load them into the ambulance.", "~b~EMS");
        }

        private void Load(Vehicle vehicle)
        {
            var seat = RearSeats.FirstOrDefault(s => API.IsVehicleSeatFree(vehicle.Handle, s));
            _patient.Task.ClearAll();
            API.TaskWarpPedIntoVehicle(_patient.Handle, vehicle.Handle, seat);

            _transport = vehicle;
            _hospital  = Hospitals.Nearest(vehicle.Position);
            Stage      = PatientStage.Loaded;

            if (_hospital is null)
            {
                ClientBrain.ShowNotification("Patient loaded, but no hospital is configured.", "~b~EMS");
                return;
            }

            _hospitalBlip = World.CreateBlip(StationService.PositionOf(_hospital));
            _hospitalBlip.Sprite    = BlipSprite.Hospital;
            _hospitalBlip.Color     = BlipColor.Red;
            _hospitalBlip.Name      = _hospital.Name;
            _hospitalBlip.ShowRoute = true;
            ClientBrain.ShowNotification($"Patient loaded. Transport them to ~y~{_hospital.Name}~w~.", "~b~EMS");
        }

        private Vehicle? FindTransport() =>
            World.GetAllVehicles()
                .Where(v => v.Exists() && API.GetVehicleClass(v.Handle) == EmergencyClass &&
                            Vector3.Distance(v.Position, _patient.Position) <= LoadRange &&
                            RearSeats.Any(s => API.IsVehicleSeatFree(v.Handle, s)))
                .OrderBy(v => Vector3.Distance(v.Position, _patient.Position))
                .FirstOrDefault();

        private static string VehicleName(Vehicle vehicle)
        {
            var label = API.GetLabelText(API.GetDisplayNameFromVehicleModel((uint)vehicle.Model.Hash));
            return string.IsNullOrEmpty(label) || label == "NULL" ? "vehicle" : label.ToLowerInvariant();
        }

        public void Dispose()
        {
            Interactions.Unregister(TryInteract);
            _hospitalBlip?.Delete();
        }
    }
}
