namespace FivePRS.Server.Database
{
    public static class AuditActions
    {
        public const string DutyOn           = "duty.on";
        public const string DutyOff          = "duty.off";
        public const string DepartmentSet    = "department.set";
        public const string AgencySet        = "agency.set";
        public const string XpAwarded        = "xp.awarded";
        public const string RankUp           = "rank.up";
        public const string PermissionDenied = "permission.denied";
        public const string AdminSetRank     = "admin.setrank";
        public const string AdminAddXp       = "admin.addxp";
        public const string AdminSetDept     = "admin.setdept";
        public const string AdminOffDuty     = "admin.offduty";
        public const string AdminEndCall     = "admin.endcall";
        public const string CharacterCreated = "civ.character.create";
        public const string CharacterDeleted = "civ.character.delete";
        public const string LicenseIssued    = "civ.license.issue";
        public const string VehicleRegistered = "civ.vehicle.register";
        public const string VehicleRemoved   = "civ.vehicle.remove";
    }

    public sealed class AuditEntry
    {
        public string  Action        { get; set; } = string.Empty;
        public string? ActorLicense  { get; set; }
        public string  ActorName     { get; set; } = string.Empty;
        public string? TargetLicense { get; set; }
        public string  Details       { get; set; } = string.Empty;
    }
}
