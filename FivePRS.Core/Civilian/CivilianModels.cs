using System;
using System.Collections.Generic;
using FivePRS.Core.Config;

namespace FivePRS.Core.Civilian
{
    public enum LicenseStatus
    {
        Valid     = 0,
        Suspended = 1,
        Revoked   = 2,
    }

    public enum VehicleStatus
    {
        Valid  = 0,
        Stolen = 1,
    }

    public sealed class CharacterInfo
    {
        public int      Id           { get; set; }
        public string   OwnerLicense { get; set; } = string.Empty;
        public string   FirstName    { get; set; } = string.Empty;
        public string   LastName     { get; set; } = string.Empty;
        public string   DateOfBirth  { get; set; } = string.Empty;
        public string   Gender       { get; set; } = string.Empty;
        public DateTime CreatedAt    { get; set; }
        public DateTime LastUsedAt   { get; set; }
        public string?  Photo        { get; set; }

        public string FullName => $"{FirstName} {LastName}";
    }

    public sealed class LicenseInfo
    {
        public int           CharacterId { get; set; }
        public string        Type        { get; set; } = string.Empty;
        public LicenseStatus Status      { get; set; }
        public DateTime      IssuedAt    { get; set; }
    }

    public sealed class VehicleInfo
    {
        public int           Id           { get; set; }
        public int           CharacterId  { get; set; }
        public string        Plate        { get; set; } = string.Empty;
        public string        Model        { get; set; } = string.Empty;
        public VehicleStatus Status       { get; set; }
        public DateTime      RegisteredAt { get; set; }
    }

    public sealed class CivilianState
    {
        public List<CharacterInfo>    Characters        { get; set; } = new();
        public int?                   ActiveCharacterId { get; set; }
        public List<LicenseView>      Licenses          { get; set; } = new();
        public List<VehicleInfo>      Vehicles          { get; set; } = new();
        public List<RecordInfo>       Records           { get; set; } = new();
        public int                    MaxCharacters     { get; set; }
        public int                    MaxVehicles       { get; set; }
    }

    public sealed class LicenseCard
    {
        public string      FirstName   { get; set; } = string.Empty;
        public string      LastName    { get; set; } = string.Empty;
        public string      DateOfBirth { get; set; } = string.Empty;
        public string      Gender      { get; set; } = string.Empty;
        public string      StateId     { get; set; } = string.Empty;
        public string?     Photo       { get; set; }
        public StateDef    State       { get; set; } = new();
        public LicenseView License     { get; set; } = new();
    }

    public sealed class LicenseView
    {
        public string         Type        { get; set; } = string.Empty;
        public string         Name        { get; set; } = string.Empty;
        public bool           SelfService { get; set; }
        public LicenseStatus? Status      { get; set; }
        public string         Number      { get; set; } = string.Empty;
        public string         Class       { get; set; } = string.Empty;
        public DateTime?      IssuedAt    { get; set; }
        public DateTime?      ExpiresAt   { get; set; }
        public LicenseCardDef Card        { get; set; } = new();
    }
}
