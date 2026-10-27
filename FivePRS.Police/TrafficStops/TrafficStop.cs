using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Client;
using FivePRS.Client.Arrest;
using FivePRS.Core.Civilian;
using FivePRS.Core.Police;

namespace FivePRS.Police.TrafficStops
{
    public enum StopPhase
    {
        PullingOver,
        Stopped,
        Fleeing,
    }

    public sealed class TrafficStop
    {
        private const int NormalDrivingStyle = 786603;
        private const int RecklessDrivingStyle = 786468;
        private const int BrakeAction = 27;
        private const int RightIndicator = 0;
        private const int LeaveDoorOpen = 256;
        private const float PullOverSpeed = 7f;
        private const float RoadsideOffset = 3.5f;
        private const float AheadDistance = 22f;
        private const float MinTargetDistance = 8f;
        private const float MaxTargetDistance = 40f;
        private const float MinTargetAlignment = 0.5f;
        private const float MaxPullOverTravel = 50f;

        private readonly Blip _blip;

        public TrafficStop(Ped driver, Vehicle? vehicle, int seed)
        {
            Driver  = driver;
            Vehicle = vehicle;
            Seed    = seed;
            Female  = !API.IsPedMale(driver.Handle);
            Person  = StreetProfiles.Person(seed, Female);
            StartedAt = API.GetGameTimer();

            if (vehicle is not null)
            {
                Plate = StreetIdentity.PlateOf(vehicle);
                Model = StreetIdentity.VehicleName(vehicle);
                API.SetEntityAsMissionEntity(vehicle.Handle, true, true);
            }
            else
            {
                Phase = StopPhase.Stopped;
            }

            API.SetEntityAsMissionEntity(driver.Handle, true, true);
            driver.BlockPermanentEvents = true;
            ArrestManager.RegisterSuspect(driver);

            _blip = vehicle is not null ? vehicle.AttachBlip() : driver.AttachBlip();
            _blip.Sprite = vehicle is not null ? BlipSprite.PersonalVehicleCar : BlipSprite.Standard;
            _blip.Color  = BlipColor.Yellow;
            _blip.Scale  = 0.8f;
            _blip.Name   = vehicle is not null ? $"Traffic stop · {Plate}" : "Stopped person";
        }

        public Ped Driver { get; }
        public Vehicle? Vehicle { get; }
        public int Seed { get; }
        public bool Female { get; }
        public StreetPerson Person { get; }
        public string Plate { get; } = string.Empty;
        public string Model { get; } = string.Empty;
        public int StartedAt { get; }
        public StopPhase Phase { get; private set; } = StopPhase.PullingOver;

        public LookupRecord? Identity { get; set; }
        public LookupRecord? Registration { get; set; }
        public string? RegistrationError { get; set; }
        public bool AwaitingIdentity { get; set; }
        public bool AwaitingPlate { get; set; }
        public bool Searched { get; set; }

        public bool IsVehicleStop => Vehicle is not null;

        public bool IsValid => Driver.Exists() && !Driver.IsDead && (Vehicle is null || Vehicle.Exists());

        public bool DriverInVehicle => Vehicle is not null && Driver.IsInVehicle(Vehicle);

        public bool InCustody => ArrestManager.IsInCustody(Driver);

        public Entity Anchor => (Entity?)Vehicle ?? Driver;

        public int? CharacterId => Identity?.Character.Id;

        public RecordInfo[] ActiveWarrants =>
            Identity?.Records.Where(r => r.Type == RecordType.Warrant && r.Active).ToArray() ?? new RecordInfo[0];

        private Vector3 _pullOverStart;
        private Vector3 _pullOverHeading;

        public void PullOver()
        {
            if (Vehicle is null) return;

            _pullOverStart   = Vehicle.Position;
            _pullOverHeading = Vehicle.ForwardVector;
            API.SetVehicleIndicatorLights(Vehicle.Handle, RightIndicator, true);

            var ahead  = Vehicle.Position + Vehicle.ForwardVector * AheadDistance;
            var target = ahead + Vehicle.RightVector * RoadsideOffset;
            var roadside = Vector3.Zero;
            if (API.GetRoadSidePointWithHeading(ahead.X, ahead.Y, ahead.Z, Vehicle.Heading, ref roadside) && IsAhead(roadside))
                target = roadside;

            if (!IsAhead(target))
            {
                API.TaskVehicleTempAction(Driver.Handle, Vehicle.Handle, BrakeAction, int.MaxValue);
                return;
            }

            API.TaskVehicleDriveToCoord(Driver.Handle, Vehicle.Handle, target.X, target.Y, target.Z,
                PullOverSpeed, 0, (uint)Vehicle.Model.Hash, NormalDrivingStyle, 2f, 0f);
        }

        public bool StrayedWhilePullingOver =>
            Vehicle is not null && Vehicle.Exists() &&
            (Vector3.Distance(Vehicle.Position, _pullOverStart) > MaxPullOverTravel ||
             Vector3.Dot(Vehicle.ForwardVector, _pullOverHeading) < MinTargetAlignment);

        private bool IsAhead(Vector3 target)
        {
            var offset   = target - Vehicle!.Position;
            var distance = offset.Length();
            if (distance < MinTargetDistance || distance > MaxTargetDistance) return false;

            offset.Normalize();
            return Vector3.Dot(offset, Vehicle.ForwardVector) >= MinTargetAlignment;
        }

        public void Hold()
        {
            Phase = StopPhase.Stopped;

            if (Vehicle is not null && DriverInVehicle)
            {
                API.TaskVehicleTempAction(Driver.Handle, Vehicle.Handle, BrakeAction, int.MaxValue);
                return;
            }

            if (InCustody) return;
            Driver.Task.ClearAll();
            API.TaskTurnPedToFaceEntity(Driver.Handle, Game.PlayerPed.Handle, 1_500);
            Driver.Task.StandStill(-1);
        }

        public void Flee()
        {
            Phase = StopPhase.Fleeing;
            _blip.Color = BlipColor.Red;
            _blip.Name  = "Fleeing suspect";

            if (Vehicle is not null && DriverInVehicle)
            {
                API.SetVehicleIndicatorLights(Vehicle.Handle, RightIndicator, false);
                API.SetDriverAggressiveness(Driver.Handle, 1f);
                API.SetDriverAbility(Driver.Handle, 1f);
                API.TaskVehicleDriveWander(Driver.Handle, Vehicle.Handle, 35f, RecklessDrivingStyle);
                return;
            }

            API.SetPedFleeAttributes(Driver.Handle, 0, false);
            API.TaskSmartFleePed(Driver.Handle, Game.PlayerPed.Handle, 300f, -1, false, false);
        }

        public void StepOut()
        {
            if (Vehicle is not null) API.TaskLeaveVehicle(Driver.Handle, Vehicle.Handle, LeaveDoorOpen);
        }

        public void GetBackIn()
        {
            if (Vehicle is not null) API.TaskEnterVehicle(Driver.Handle, Vehicle.Handle, -1, -1, 1f, 1, 0);
        }

        public async Task EndAsync(bool release)
        {
            if (_blip.Exists()) _blip.Delete();
            if (Vehicle is not null && Vehicle.Exists()) API.SetVehicleIndicatorLights(Vehicle.Handle, RightIndicator, false);

            if (Driver.Exists() && !InCustody)
            {
                ArrestManager.UnregisterSuspect(Driver);
                Driver.BlockPermanentEvents = false;

                if (release && !Driver.IsDead)
                    await ReleaseAsync();

                if (Driver.Exists())
                {
                    var driver = Driver.Handle;
                    API.SetEntityAsNoLongerNeeded(ref driver);
                }
            }

            if (Vehicle is not null && Vehicle.Exists())
            {
                var vehicle = Vehicle.Handle;
                API.SetEntityAsNoLongerNeeded(ref vehicle);
            }
        }

        private async Task ReleaseAsync()
        {
            if (Vehicle is null || !Vehicle.Exists())
            {
                Driver.Task.ClearAll();
                API.TaskWanderStandard(Driver.Handle, 10f, 10);
                return;
            }

            if (!DriverInVehicle)
            {
                GetBackIn();
                for (var i = 0; i < 20 && Driver.Exists() && !DriverInVehicle; i++)
                    await BaseScript.Delay(500);
            }

            if (Driver.Exists() && DriverInVehicle)
                API.TaskVehicleDriveWander(Driver.Handle, Vehicle.Handle, 15f, NormalDrivingStyle);
        }
    }
}
