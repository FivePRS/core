using System;
using System.Threading;
using System.Threading.Tasks;
using CitizenFX.Core;
using FivePRS.Client;
using FivePRS.Client.Callouts;

namespace FivePRS.EMS.Callouts
{
    public abstract class PatientCallout : CalloutBase
    {
        private const float ArrivalDistance = 120f;
        private const int TimeoutMinutes = 30;

        protected static readonly string[] PatientModels =
        {
            "a_m_m_business_01", "a_f_m_business_02", "a_m_y_hipster_01", "a_f_y_hipster_01",
            "a_m_m_tourist_01", "a_f_y_tourist_01", "a_m_o_soucent_01", "a_f_o_soucent_01",
        };

        protected readonly Random Rng = new();

        private PatientCare? _care;

        protected abstract Vector3 Location { get; }

        protected abstract string BlipName { get; }

        protected abstract Task<Ped?> SetupSceneAsync(CancellationToken ct);

        public override Vector3 GetDispatchLocation() => Location;

        public override async Task OnCalloutAccepted(CancellationToken ct)
        {
            var blip       = TrackBlip(World.CreateBlip(Location));
            blip.Sprite    = BlipSprite.Standard;
            blip.Color     = BlipColor.Red;
            blip.Name      = BlipName;
            blip.ShowRoute = true;

            while (Vector3.Distance(Game.PlayerPed.Position, Location) > ArrivalDistance)
                await Timing.WaitAsync(1_000, ct);

            var patient = await SetupSceneAsync(ct);
            if (patient is null || !patient.Exists())
            {
                CalloutFailed();
                return;
            }

            blip.Position  = patient.Position;
            blip.ShowRoute = false;
            _care = new PatientCare(patient);
            ClientBrain.ShowNotification($"~b~{Data.Name}~w~ | Patient located. Treat them, then transport them to hospital.");

            var deadline = DateTime.UtcNow.AddMinutes(TimeoutMinutes);
            while (!ct.IsCancellationRequested)
            {
                await Timing.WaitAsync(400, ct);

                if (!patient.Exists())
                {
                    CalloutFailed();
                    return;
                }

                if (_care.Update())
                {
                    CalloutCompleted();
                    return;
                }

                if (_care.Stage == PatientStage.Loaded && blip.Exists()) blip.Delete();

                if (DateTime.UtcNow > deadline)
                {
                    ClientBrain.ShowNotification($"~b~{Data.Name}~w~ | ~r~The patient was taken by another unit.");
                    CalloutFailed();
                    return;
                }

                var prompt = _care.Prompt();
                if (prompt is not null) ClientBrain.ShowHelp(prompt, 500);
            }
        }

        protected async Task<Ped?> SpawnPatientAsync(Vector3 position, float heading)
        {
            var model = new Model(PatientModels[Rng.Next(PatientModels.Length)]);
            if (!await model.Request(7_000)) return null;

            var ped = TrackEntity(await World.CreatePed(model, position, heading));
            model.MarkAsNoLongerNeeded();
            return ped;
        }

        protected override void OnCleanup() => _care?.Dispose();
    }
}
