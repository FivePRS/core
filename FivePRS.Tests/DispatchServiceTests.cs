using System;
using System.Linq;
using FivePRS.Core.Config;
using FivePRS.Core.Jurisdiction;
using FivePRS.Core.Models;
using FivePRS.Server.Dispatch;
using Xunit;

namespace FivePRS.Tests
{
    public class DispatchServiceTests
    {
        private const int Officer = 1;
        private const int Backup  = 2;

        private readonly ResourceSettings _settings = new()
        {
            DispatchIntervalMinutes     = 5,
            AcceptWindowSeconds         = 30,
            InitialGraceSeconds         = 45,
            PostCompleteCooldownSeconds = 60,
            PostDeclineCooldownSeconds  = 45,
            PostFailCooldownSeconds     = 30,
            NoCalloutRetrySeconds       = 30,
        };

        private readonly TerritoryMap _territories = new(new JurisdictionConfig());

        private DateTime _now = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
        private readonly DispatchService _dispatch;

        public DispatchServiceTests()
        {
            _dispatch = new DispatchService(() => _now, () => _settings, () => _territories, new Random(1));
            _dispatch.RegisterCallouts(new[]
            {
                Definition("Traffic Stop", Department.Police, xp: 60),
                Definition("Structure Fire", Department.Fire, xp: 100),
            });
        }

        [Fact]
        public void Tick_DuringInitialGrace_OffersNothing()
        {
            _dispatch.SetOnDuty(Officer, "Officer", Department.Police, 1);

            Assert.Empty(_dispatch.Tick());
        }

        [Fact]
        public void Tick_AfterGrace_OffersCalloutFromUnitDepartment()
        {
            _dispatch.SetOnDuty(Officer, "Officer", Department.Police, 1);
            Advance(_settings.InitialGraceSeconds);

            var offer = Assert.Single(_dispatch.Tick());

            Assert.Equal(Officer, offer.UnitId);
            Assert.Equal("Traffic Stop", offer.Callout.Name);
            Assert.Equal(Department.Police, offer.Callout.RequiredDepartment);
        }

        [Fact]
        public void Tick_WithPendingOffer_DoesNotOfferAgain()
        {
            OfferTo(Officer);

            Assert.Empty(_dispatch.Tick());
        }

        [Fact]
        public void Tick_BusyUnit_ReceivesNoOffers()
        {
            _dispatch.SetOnDuty(Officer, "Officer", Department.Police, 1);
            _dispatch.SetStatus(Officer, UnitStatus.Busy);
            Advance(_settings.InitialGraceSeconds);

            Assert.Empty(_dispatch.Tick());
        }

        [Fact]
        public void Respond_Accepted_ActivatesCallAndSetsUnitEnRoute()
        {
            var callId = OfferTo(Officer);

            Assert.True(_dispatch.Respond(Officer, callId, OfferResponse.Accepted, 100f, 200f, 30f));

            var snapshot = _dispatch.CreateSnapshot();
            var call = Assert.Single(snapshot.Calls);
            Assert.Equal(callId, call.Id);
            Assert.Equal(new[] { Officer }, call.Units);
            Assert.Equal(UnitStatus.EnRoute, snapshot.Units.Single().Status);
        }

        [Fact]
        public void Respond_FromOtherUnit_IsRejected()
        {
            var callId = OfferTo(Officer);
            _dispatch.SetOnDuty(Backup, "Backup", Department.Police, 1);

            Assert.False(_dispatch.Respond(Backup, callId, OfferResponse.Accepted, 0f, 0f, 0f));
        }

        [Fact]
        public void Respond_Declined_AppliesDeclineCooldown()
        {
            var callId = OfferTo(Officer);
            _dispatch.Respond(Officer, callId, OfferResponse.Declined, 0f, 0f, 0f);

            Advance(_settings.PostDeclineCooldownSeconds - 1);
            Assert.Empty(_dispatch.Tick());
            Assert.Empty(_dispatch.CreateSnapshot().Calls);
        }

        [Fact]
        public void Tick_UnansweredOffer_ExpiresAsDeclined()
        {
            var callId = OfferTo(Officer);

            Advance(_settings.AcceptWindowSeconds + DispatchService.OfferGraceSeconds + 1);
            _dispatch.Tick();

            Assert.False(_dispatch.Respond(Officer, callId, OfferResponse.Accepted, 0f, 0f, 0f));
        }

        [Fact]
        public void Tick_CalloutOnCooldown_IsNotOfferedToAnotherUnit()
        {
            _dispatch.RegisterCallouts(new[] { Definition("Traffic Stop", Department.Police, xp: 60, cooldown: 600) });
            OfferTo(Officer);

            _dispatch.SetOnDuty(Backup, "Backup", Department.Police, 1);
            Advance(_settings.InitialGraceSeconds);

            Assert.Empty(_dispatch.Tick());
        }

        [Fact]
        public void End_Completed_AwardsPrimaryFullAndBackupHalf()
        {
            var callId = AcceptedCall(Officer);
            _dispatch.SetOnDuty(Backup, "Backup", Department.Police, 1);
            Assert.True(_dispatch.Attach(Backup, callId));

            var awards = _dispatch.End(Officer, callId, CalloutResult.Completed);

            Assert.Contains(awards, a => a.UnitId == Officer && a.Amount == 60);
            Assert.Contains(awards, a => a.UnitId == Backup  && a.Amount == 30);
            Assert.Empty(_dispatch.CreateSnapshot().Calls);
            Assert.All(_dispatch.CreateSnapshot().Units, u => Assert.Equal(UnitStatus.Available, u.Status));
        }

        [Fact]
        public void End_Failed_AwardsNothing()
        {
            var callId = AcceptedCall(Officer);

            Assert.Empty(_dispatch.End(Officer, callId, CalloutResult.Failed));
        }

        [Fact]
        public void End_ByBackupUnit_IsRejected()
        {
            var callId = AcceptedCall(Officer);
            _dispatch.SetOnDuty(Backup, "Backup", Department.Police, 1);
            _dispatch.Attach(Backup, callId);

            Assert.Empty(_dispatch.End(Backup, callId, CalloutResult.Completed));
            Assert.Single(_dispatch.CreateSnapshot().Calls);
        }

        [Fact]
        public void End_SameCallTwice_AwardsOnce()
        {
            var callId = AcceptedCall(Officer);

            Assert.NotEmpty(_dispatch.End(Officer, callId, CalloutResult.Completed));
            Assert.Empty(_dispatch.End(Officer, callId, CalloutResult.Completed));
        }

        [Fact]
        public void SetOffDuty_PrimaryUnit_ClosesCallAndFreesBackup()
        {
            var callId = AcceptedCall(Officer);
            _dispatch.SetOnDuty(Backup, "Backup", Department.Police, 1);
            _dispatch.Attach(Backup, callId);

            _dispatch.SetOffDuty(Officer);

            var snapshot = _dispatch.CreateSnapshot();
            Assert.Empty(snapshot.Calls);
            var backup = Assert.Single(snapshot.Units);
            Assert.Null(backup.CallId);
            Assert.Equal(UnitStatus.Available, backup.Status);
        }

        [Fact]
        public void SetStatus_PrimaryWithActiveCall_IsRejected()
        {
            AcceptedCall(Officer);

            Assert.False(_dispatch.SetStatus(Officer, UnitStatus.Available));
        }

        [Fact]
        public void SetStatus_AvailableOnBackup_DetachesFromCall()
        {
            var callId = AcceptedCall(Officer);
            _dispatch.SetOnDuty(Backup, "Backup", Department.Police, 1);
            _dispatch.Attach(Backup, callId);

            Assert.True(_dispatch.SetStatus(Backup, UnitStatus.Available));

            Assert.Equal(new[] { Officer }, _dispatch.CreateSnapshot().Calls.Single().Units);
        }

        [Fact]
        public void UpdatePosition_InsideSceneRadius_MarksUnitOnScene()
        {
            AcceptedCall(Officer, x: 100f, y: 100f);

            _dispatch.UpdatePosition(Officer, 500f, 500f, 0f);
            Assert.Equal(UnitStatus.EnRoute, _dispatch.GetUnit(Officer)!.Status);

            _dispatch.UpdatePosition(Officer, 110f, 105f, 0f);
            Assert.Equal(UnitStatus.OnScene, _dispatch.GetUnit(Officer)!.Status);
        }

        [Fact]
        public void ForceClose_ActiveCall_ReturnsPrimaryAndFreesUnits()
        {
            var callId = AcceptedCall(Officer);

            Assert.Equal(Officer, _dispatch.ForceClose(callId));

            var snapshot = _dispatch.CreateSnapshot();
            Assert.Empty(snapshot.Calls);
            Assert.Equal(UnitStatus.Available, snapshot.Units.Single().Status);
            Assert.Empty(_dispatch.End(Officer, callId, CalloutResult.Completed));
        }

        [Fact]
        public void ForceClose_UnknownCall_ReturnsNull()
        {
            Assert.Null(_dispatch.ForceClose("9999"));
        }

        [Fact]
        public void Attach_ToUnknownCall_Fails()
        {
            _dispatch.SetOnDuty(Backup, "Backup", Department.Police, 1);

            Assert.False(_dispatch.Attach(Backup, "9999"));
        }

        [Fact]
        public void SetOnDuty_AssignsDepartmentCallsign()
        {
            _dispatch.SetOnDuty(7, "Medic", Department.EMS, 1);

            Assert.Equal("EMS-7", _dispatch.GetUnit(7)!.Callsign);
        }

        [Fact]
        public void SetOnDuty_UsesAgencyCallsignPrefix()
        {
            _dispatch.SetOnDuty(Officer, "Deputy", Department.Police, 1, "bcso");

            var unit = _dispatch.GetUnit(Officer)!;
            Assert.Equal("bcso", unit.Agency);
            Assert.Equal("BCSO-1", unit.Callsign);
        }

        [Fact]
        public void SetOnDuty_WithCallsign_UsesIt()
        {
            _dispatch.SetOnDuty(Officer, "Officer", Department.Police, 1, "lspd", "1-ADAM-12");

            Assert.Equal("1-ADAM-12", _dispatch.GetUnit(Officer)!.Callsign);
        }

        [Fact]
        public void IsCallsignTaken_OtherUnitHasIt_ReturnsTrue()
        {
            _dispatch.SetOnDuty(Officer, "Officer", Department.Police, 1, "lspd", "1-ADAM-12");

            Assert.True(_dispatch.IsCallsignTaken("1-adam-12", Backup));
            Assert.False(_dispatch.IsCallsignTaken("1-ADAM-12", Officer));
            Assert.False(_dispatch.IsCallsignTaken("2-ADAM-12", Backup));
        }

        [Fact]
        public void SetOnDuty_AgencyFromOtherDepartment_FallsBackToDefault()
        {
            _dispatch.SetOnDuty(Officer, "Officer", Department.Police, 1, "unknown");

            Assert.Equal("lspd", _dispatch.GetUnit(Officer)!.Agency);
        }

        [Fact]
        public void Tick_UnitOutsideJurisdiction_ReceivesNoOffers()
        {
            _dispatch.SetOnDuty(Officer, "Officer", Department.Police, 1, "lspd");
            _dispatch.UpdatePosition(Officer, 1960f, 3740f, 32f);
            Advance(_settings.InitialGraceSeconds);

            Assert.Empty(_dispatch.Tick());
            Assert.Equal("blaine_county", _dispatch.GetUnit(Officer)!.Territory);
        }

        [Fact]
        public void Tick_UnitInsideJurisdiction_ReceivesOffer()
        {
            _dispatch.SetOnDuty(Officer, "Deputy", Department.Police, 1, "bcso");
            _dispatch.UpdatePosition(Officer, 1960f, 3740f, 32f);
            Advance(_settings.InitialGraceSeconds);

            Assert.Single(_dispatch.Tick());
        }

        [Fact]
        public void Respond_Accepted_ResolvesCallTerritory()
        {
            var callId = OfferTo(Officer);
            _dispatch.Respond(Officer, callId, OfferResponse.Accepted, 25f, -1344f, 29f);

            Assert.Equal("los_santos", _dispatch.CreateSnapshot().Calls.Single().Territory);
        }

        private string OfferTo(int unitId)
        {
            _dispatch.SetOnDuty(unitId, $"Unit {unitId}", Department.Police, 1);
            Advance(_settings.InitialGraceSeconds);
            return Assert.Single(_dispatch.Tick()).Callout.Id;
        }

        private string AcceptedCall(int unitId, float x = 1f, float y = 1f)
        {
            var callId = OfferTo(unitId);
            _dispatch.Respond(unitId, callId, OfferResponse.Accepted, x, y, 0f);
            return callId;
        }

        private void Advance(int seconds) => _now = _now.AddSeconds(seconds);

        private static CalloutDefinition Definition(string name, Department department, int xp, int cooldown = 0) => new()
        {
            Name            = name,
            Department      = department,
            Priority        = CalloutPriority.Low,
            Weight          = 10,
            CooldownSeconds = cooldown,
            XPReward        = xp,
        };
    }
}
