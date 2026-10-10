using System.Threading;
using System.Threading.Tasks;
using CitizenFX.Core;
using FivePRS.Client;
using FivePRS.Client.Callouts;
using FivePRS.Core.Models;

namespace FivePRS.EMS.Callouts
{
    [CalloutInfo(
        name:            "Medical Emergency",
        department:      Department.EMS,
        priority:        CalloutPriority.High,
        weight:          30,
        cooldownSeconds: 180,
        xpReward:        80)]
    public sealed class MedicalEmergencyCallout : PatientCallout
    {
        private static readonly string[] Conditions =
        {
            "A person collapsed and is struggling to breathe.",
            "Caller reports a person with severe chest pains.",
            "A diabetic person is confused and barely responsive.",
            "An elderly person fell and can't get up.",
            "A person is having a seizure on the sidewalk.",
        };

        private readonly Vector3 _location;

        public MedicalEmergencyCallout()
        {
            _location = World.GetNextPositionOnStreet(Positions.Around(Game.PlayerPed.Position, 250f + Rng.Next(200), Rng));
            Data.Description = Conditions[Rng.Next(Conditions.Length)];
        }

        protected override Vector3 Location => _location;

        protected override string BlipName => "Medical emergency";

        protected override Task<Ped?> SetupSceneAsync(CancellationToken ct) =>
            SpawnPatientAsync(World.GetNextPositionOnSidewalk(_location), Rng.Next(360));

        public override void OnCalloutDeclined() => ClientBrain.ShowNotification("Medical emergency declined.", "~r~Dispatch");

        public override void OnCalloutFailed() => ClientBrain.ShowNotification("Medical emergency ended.", "~r~Dispatch");
    }
}
