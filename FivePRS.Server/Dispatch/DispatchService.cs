using System;
using System.Collections.Generic;
using System.Linq;
using FivePRS.Core.Config;
using FivePRS.Core.Jurisdiction;
using FivePRS.Core.Models;

namespace FivePRS.Server.Dispatch
{
    public sealed class DispatchOffer
    {
        public int         UnitId  { get; }
        public CalloutData Callout { get; }

        public DispatchOffer(int unitId, CalloutData callout)
        {
            UnitId  = unitId;
            Callout = callout;
        }
    }

    public sealed class XpAward
    {
        public int UnitId { get; }
        public int Amount { get; }

        public XpAward(int unitId, int amount)
        {
            UnitId = unitId;
            Amount = amount;
        }
    }

    public sealed class DispatchService
    {
        public const float OnSceneRadiusM     = 40f;
        public const int   OfferGraceSeconds  = 10;

        private enum CallStatus { Pending, Active }

        private sealed class Unit
        {
            public UnitInfo   Info         { get; } = new();
            public AgencyDef? Agency       { get; set; }
            public bool       HasPosition  { get; set; }
            public string?    PendingCall  { get; set; }
            public DateTime NextOfferUtc { get; set; }
        }

        private sealed class Call
        {
            public string            Id          { get; set; } = string.Empty;
            public CalloutDefinition Definition  { get; set; } = new();
            public CallStatus        Status      { get; set; }
            public int               PrimaryUnit { get; set; }
            public List<int>         Attached    { get; } = new();
            public DateTime          OfferedUtc  { get; set; }
            public bool              HasLocation { get; set; }
            public string?           Territory   { get; set; }
            public float             X           { get; set; }
            public float             Y           { get; set; }
            public float             Z           { get; set; }
        }

        private readonly Func<DateTime>         _clock;
        private readonly Func<ResourceSettings> _settings;
        private readonly Func<TerritoryMap>     _territories;
        private readonly Random                 _rng;

        private readonly Dictionary<int, Unit>                _units          = new();
        private readonly Dictionary<string, Call>             _calls          = new();
        private readonly Dictionary<string, CalloutDefinition> _catalog       = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime>         _lastDispatched = new(StringComparer.OrdinalIgnoreCase);

        private int _nextCallNumber = 1000;

        public DispatchService(Func<DateTime> clock, Func<ResourceSettings> settings, Func<TerritoryMap> territories, Random rng)
        {
            _clock       = clock;
            _settings    = settings;
            _territories = territories;
            _rng         = rng;
        }

        public bool IsDirty { get; private set; }

        public IEnumerable<int> UnitIds => _units.Keys.ToList();

        public int CatalogCount => _catalog.Count;

        public bool IsOnDuty(int unitId) => _units.ContainsKey(unitId);

        public void SetOnDuty(int unitId, string name, Department department, int rank, string? agencyId = null)
        {
            SetOffDuty(unitId);

            var agency = _territories().ResolveAgency(department, agencyId);
            var prefix = string.IsNullOrWhiteSpace(agency?.CallsignPrefix) ? CallsignPrefix(department) : agency!.CallsignPrefix;

            var unit = new Unit
            {
                Agency       = agency,
                NextOfferUtc = _clock().AddSeconds(Settings.InitialGraceSeconds),
            };
            unit.Info.ServerId   = unitId;
            unit.Info.Name       = name;
            unit.Info.Department = department;
            unit.Info.Agency     = agency?.Id ?? string.Empty;
            unit.Info.Rank       = rank;
            unit.Info.Callsign   = $"{prefix}-{unitId}";
            unit.Info.Status     = UnitStatus.Available;

            _units[unitId] = unit;
            IsDirty = true;
        }

        public void SetOffDuty(int unitId)
        {
            if (!_units.TryGetValue(unitId, out var unit)) return;

            if (unit.PendingCall is not null)
                _calls.Remove(unit.PendingCall);

            if (unit.Info.CallId is not null && _calls.TryGetValue(unit.Info.CallId, out var call))
            {
                if (call.PrimaryUnit == unitId)
                    CloseCall(call, Settings.PostFailCooldownSeconds);
                else
                    call.Attached.Remove(unitId);
            }

            _units.Remove(unitId);
            IsDirty = true;
        }

        public void UpdateRank(int unitId, int rank)
        {
            if (!_units.TryGetValue(unitId, out var unit) || unit.Info.Rank == rank) return;
            unit.Info.Rank = rank;
            IsDirty = true;
        }

        public void RegisterCallouts(IEnumerable<CalloutDefinition> definitions)
        {
            foreach (var definition in definitions)
            {
                if (string.IsNullOrWhiteSpace(definition.Name) || definition.Weight < 1) continue;
                _catalog[definition.Name] = definition;
            }
        }

        public IReadOnlyList<DispatchOffer> Tick()
        {
            var now = _clock();
            ExpireStaleOffers(now);

            var offers = new List<DispatchOffer>();

            foreach (var unit in _units.Values)
            {
                if (unit.Info.Status != UnitStatus.Available) continue;
                if (unit.Info.CallId is not null || unit.PendingCall is not null) continue;
                if (now < unit.NextOfferUtc) continue;
                if (unit.HasPosition && !TerritoryMap.IsInJurisdiction(unit.Agency, unit.Info.Territory)) continue;

                var definition = PickCallout(unit.Info.Department, now);
                if (definition is null)
                {
                    unit.NextOfferUtc = now.AddSeconds(Settings.NoCalloutRetrySeconds);
                    continue;
                }

                var call = new Call
                {
                    Id          = (_nextCallNumber++).ToString(),
                    Definition  = definition,
                    Status      = CallStatus.Pending,
                    PrimaryUnit = unit.Info.ServerId,
                    OfferedUtc  = now,
                };

                _calls[call.Id]                  = call;
                _lastDispatched[definition.Name] = now;
                unit.PendingCall                 = call.Id;

                offers.Add(new DispatchOffer(unit.Info.ServerId, ToCalloutData(call)));
            }

            return offers;
        }

        public bool Respond(int unitId, string callId, OfferResponse response, float x, float y, float z)
        {
            if (!_units.TryGetValue(unitId, out var unit) || unit.PendingCall != callId) return false;
            if (!_calls.TryGetValue(callId, out var call) || call.Status != CallStatus.Pending) return false;

            unit.PendingCall = null;
            var now = _clock();

            switch (response)
            {
                case OfferResponse.Accepted:
                    call.Status      = CallStatus.Active;
                    call.HasLocation = x != 0f || y != 0f || z != 0f;
                    call.Territory   = call.HasLocation ? _territories().Resolve(x, y)?.Id : null;
                    call.X = x;
                    call.Y = y;
                    call.Z = z;
                    unit.Info.CallId = call.Id;
                    unit.Info.Status = UnitStatus.EnRoute;
                    IsDirty = true;
                    return true;

                case OfferResponse.Declined:
                    _calls.Remove(callId);
                    unit.NextOfferUtc = now.AddSeconds(Settings.PostDeclineCooldownSeconds);
                    return true;

                default:
                    _calls.Remove(callId);
                    _lastDispatched.Remove(call.Definition.Name);
                    unit.NextOfferUtc = now.AddSeconds(Settings.NoCalloutRetrySeconds);
                    return true;
            }
        }

        public IReadOnlyList<XpAward> End(int unitId, string callId, CalloutResult result)
        {
            if (!_calls.TryGetValue(callId, out var call)) return Array.Empty<XpAward>();
            if (call.Status != CallStatus.Active || call.PrimaryUnit != unitId) return Array.Empty<XpAward>();

            var awards = new List<XpAward>();
            if (result == CalloutResult.Completed)
            {
                awards.Add(new XpAward(call.PrimaryUnit, call.Definition.XPReward));
                awards.AddRange(call.Attached
                    .Where(_units.ContainsKey)
                    .Select(id => new XpAward(id, call.Definition.XPReward / 2)));
            }

            var cooldown = result == CalloutResult.Completed
                ? Settings.PostCompleteCooldownSeconds
                : Settings.PostFailCooldownSeconds;
            CloseCall(call, cooldown);

            return awards;
        }

        public int? ForceClose(string callId)
        {
            if (!_calls.TryGetValue(callId, out var call) || call.Status != CallStatus.Active) return null;

            CloseCall(call, Settings.PostFailCooldownSeconds);
            return call.PrimaryUnit;
        }

        public bool Attach(int unitId, string callId)
        {
            if (!_units.TryGetValue(unitId, out var unit)) return false;
            if (unit.Info.CallId is not null || unit.PendingCall is not null) return false;
            if (!_calls.TryGetValue(callId, out var call) || call.Status != CallStatus.Active) return false;

            call.Attached.Add(unitId);
            unit.Info.CallId = call.Id;
            unit.Info.Status = UnitStatus.EnRoute;
            IsDirty = true;
            return true;
        }

        public bool SetStatus(int unitId, UnitStatus status)
        {
            if (!_units.TryGetValue(unitId, out var unit)) return false;
            if (status != UnitStatus.Available && status != UnitStatus.Busy) return false;
            if (unit.PendingCall is not null) return false;

            if (unit.Info.CallId is not null && _calls.TryGetValue(unit.Info.CallId, out var call))
            {
                if (call.PrimaryUnit == unitId) return false;

                call.Attached.Remove(unitId);
                unit.Info.CallId = null;
            }

            unit.Info.Status = status;
            if (status == UnitStatus.Available)
                unit.NextOfferUtc = Max(unit.NextOfferUtc, _clock().AddSeconds(Settings.PostCompleteCooldownSeconds));

            IsDirty = true;
            return true;
        }

        public void UpdatePosition(int unitId, float x, float y, float z)
        {
            if (!_units.TryGetValue(unitId, out var unit)) return;

            unit.Info.X      = x;
            unit.Info.Y      = y;
            unit.Info.Z      = z;
            unit.HasPosition = true;

            var territory = _territories().Resolve(x, y)?.Id;
            if (territory != unit.Info.Territory)
            {
                unit.Info.Territory = territory;
                IsDirty = true;
            }

            if (unit.Info.Status != UnitStatus.EnRoute || unit.Info.CallId is null) return;
            if (!_calls.TryGetValue(unit.Info.CallId, out var call) || !call.HasLocation) return;

            var dx = call.X - x;
            var dy = call.Y - y;
            if (dx * dx + dy * dy > OnSceneRadiusM * OnSceneRadiusM) return;

            unit.Info.Status = UnitStatus.OnScene;
            IsDirty = true;
        }

        public UnitInfo? GetUnit(int unitId) =>
            _units.TryGetValue(unitId, out var unit) ? unit.Info : null;

        public void ClearDirty() => IsDirty = false;

        public DispatchSnapshot CreateSnapshot()
        {
            return new DispatchSnapshot
            {
                Units = _units.Values.Select(u => Copy(u.Info)).ToList(),
                Calls = _calls.Values
                    .Where(c => c.Status == CallStatus.Active)
                    .Select(c => new CallInfo
                    {
                        Id          = c.Id,
                        Name        = c.Definition.Name,
                        Department  = c.Definition.Department,
                        Priority    = c.Definition.Priority,
                        Territory   = c.Territory,
                        PrimaryUnit = c.PrimaryUnit,
                        Units       = new[] { c.PrimaryUnit }.Concat(c.Attached).ToList(),
                        X           = c.X,
                        Y           = c.Y,
                        Z           = c.Z,
                    })
                    .ToList(),
            };
        }

        private ResourceSettings Settings => _settings();

        private void ExpireStaleOffers(DateTime now)
        {
            var timeout = TimeSpan.FromSeconds(Settings.AcceptWindowSeconds + OfferGraceSeconds);

            var stale = _calls.Values
                .Where(c => c.Status == CallStatus.Pending && now - c.OfferedUtc > timeout)
                .ToList();

            foreach (var call in stale)
                Respond(call.PrimaryUnit, call.Id, OfferResponse.Declined, 0f, 0f, 0f);
        }

        private CalloutDefinition? PickCallout(Department department, DateTime now)
        {
            var pool = _catalog.Values
                .Where(d => d.Department == department && !IsOnCooldown(d, now))
                .ToList();

            if (pool.Count == 0) return null;

            var roll = _rng.Next(pool.Sum(d => d.Weight));
            foreach (var definition in pool)
            {
                roll -= definition.Weight;
                if (roll < 0) return definition;
            }
            return pool[pool.Count - 1];
        }

        private bool IsOnCooldown(CalloutDefinition definition, DateTime now) =>
            definition.CooldownSeconds > 0 &&
            _lastDispatched.TryGetValue(definition.Name, out var last) &&
            (now - last).TotalSeconds < definition.CooldownSeconds;

        private void CloseCall(Call call, int primaryCooldownSeconds)
        {
            var now = _clock();

            foreach (var id in new[] { call.PrimaryUnit }.Concat(call.Attached))
            {
                if (!_units.TryGetValue(id, out var unit) || unit.Info.CallId != call.Id) continue;

                unit.Info.CallId = null;
                unit.Info.Status = UnitStatus.Available;

                var cooldown = id == call.PrimaryUnit ? primaryCooldownSeconds : Settings.PostCompleteCooldownSeconds;
                unit.NextOfferUtc = now.AddSeconds(cooldown + Settings.DispatchIntervalMinutes * 60);
            }

            _calls.Remove(call.Id);
            IsDirty = true;
        }

        private static CalloutData ToCalloutData(Call call) => new()
        {
            Id                 = call.Id,
            Name               = call.Definition.Name,
            Priority           = call.Definition.Priority,
            RequiredDepartment = call.Definition.Department,
            XPReward           = call.Definition.XPReward,
        };

        private static UnitInfo Copy(UnitInfo info) => new()
        {
            ServerId   = info.ServerId,
            Name       = info.Name,
            Callsign   = info.Callsign,
            Department = info.Department,
            Agency     = info.Agency,
            Territory  = info.Territory,
            Rank       = info.Rank,
            Status     = info.Status,
            CallId     = info.CallId,
            X          = info.X,
            Y          = info.Y,
            Z          = info.Z,
        };

        private static string CallsignPrefix(Department department) => department switch
        {
            Department.Police => "PD",
            Department.EMS    => "EMS",
            Department.Fire   => "FD",
            _                 => "U",
        };

        private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
    }
}
