using System;
using System.Collections.Generic;

namespace FivePRS.Core.Civilian
{
    public enum RecordType
    {
        Citation = 0,
        Arrest   = 1,
        Warning  = 2,
        Warrant  = 3,
    }

    public sealed class RecordInfo
    {
        public int        Id              { get; set; }
        public int        CharacterId     { get; set; }
        public RecordType Type            { get; set; }
        public string     Description     { get; set; } = string.Empty;
        public int        Fine            { get; set; }
        public string     OfficerName     { get; set; } = string.Empty;
        public string     OfficerCallsign { get; set; } = string.Empty;
        public DateTime   CreatedAt       { get; set; }
        public bool       Active          { get; set; }
        public string?    Resolution      { get; set; }
    }

    public sealed class CharacterSummary
    {
        public int    Id                { get; set; }
        public string FirstName         { get; set; } = string.Empty;
        public string LastName          { get; set; } = string.Empty;
        public string DateOfBirth       { get; set; } = string.Empty;
        public string Gender            { get; set; } = string.Empty;
        public bool   HasActiveWarrant  { get; set; }
    }

    public sealed class LookupRecord
    {
        public CharacterSummary  Character    { get; set; } = new();
        public List<LicenseView> Licenses     { get; set; } = new();
        public List<VehicleInfo> Vehicles     { get; set; } = new();
        public List<RecordInfo>  Records      { get; set; } = new();
        public string?           MatchedPlate { get; set; }
    }

    public sealed class LookupResult
    {
        public List<CharacterSummary>? Results { get; set; }
        public LookupRecord?           Record  { get; set; }
        public string?                 Query   { get; set; }
    }

    public sealed class OfficerInfo
    {
        public string License  { get; set; } = string.Empty;
        public string Name     { get; set; } = string.Empty;
        public string Callsign { get; set; } = string.Empty;
    }
}
