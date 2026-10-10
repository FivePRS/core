namespace FivePRS.Core.Events
{
    public static class EventNames
    {
        public const string ClientReceivePlayerData = "FivePRS:Client:ReceivePlayerData";
        public const string ClientDutyStatusChanged = "FivePRS:Client:DutyStatusChanged";
        public const string ClientCalloutOffered    = "FivePRS:Client:CalloutOffered";
        public const string ClientDispatchSnapshot  = "FivePRS:Client:DispatchSnapshot";
        public const string ClientRankedUp          = "FivePRS:Client:RankedUp";
        public const string ClientNotify            = "FivePRS:Client:Notify";
        public const string ClientEndCallout        = "FivePRS:Client:EndCallout";
        public const string ClientEntryOptions      = "FivePRS:Client:EntryOptions";
        public const string ClientEntryRejected     = "FivePRS:Client:EntryRejected";
        public const string ClientCivilianState     = "FivePRS:Client:CivilianState";
        public const string ClientCivilianError     = "FivePRS:Client:CivilianError";
        public const string ClientLookupResult      = "FivePRS:Client:LookupResult";
        public const string ClientLookupError       = "FivePRS:Client:LookupError";
        public const string ClientEmergencyStatus   = "FivePRS:Client:EmergencyStatus";
        public const string ClientEmergencyError    = "FivePRS:Client:EmergencyError";
        public const string ClientAdminState        = "FivePRS:Client:AdminState";
        public const string ClientAppearance        = "FivePRS:Client:Appearance";
        public const string ClientPreferences       = "FivePRS:Client:Preferences";

        public const string ServerPlayerConnected   = "FivePRS:Server:PlayerConnected";
        public const string ServerToggleDuty        = "FivePRS:Server:ToggleDuty";
        public const string ServerEnterService      = "FivePRS:Server:EnterService";
        public const string ServerRegisterCallouts  = "FivePRS:Server:RegisterCallouts";
        public const string ServerCalloutResponse   = "FivePRS:Server:CalloutResponse";
        public const string ServerCalloutEnded      = "FivePRS:Server:CalloutEnded";
        public const string ServerSetUnitStatus     = "FivePRS:Server:SetUnitStatus";
        public const string ServerAttachToCall      = "FivePRS:Server:AttachToCall";
        public const string ServerSetAiCallouts     = "FivePRS:Server:SetAiCallouts";
        public const string ServerCivilianRequest   = "FivePRS:Server:CivilianRequest";
        public const string ServerCharacterCreate   = "FivePRS:Server:CharacterCreate";
        public const string ServerCharacterSelect   = "FivePRS:Server:CharacterSelect";
        public const string ServerCharacterDelete   = "FivePRS:Server:CharacterDelete";
        public const string ServerLicenseApply      = "FivePRS:Server:LicenseApply";
        public const string ServerVehicleRegister   = "FivePRS:Server:VehicleRegister";
        public const string ServerVehicleRemove     = "FivePRS:Server:VehicleRemove";
        public const string ServerVehicleSetStolen  = "FivePRS:Server:VehicleSetStolen";
        public const string ServerLookupName        = "FivePRS:Server:LookupName";
        public const string ServerLookupPlate       = "FivePRS:Server:LookupPlate";
        public const string ServerLookupCharacter   = "FivePRS:Server:LookupCharacter";
        public const string ServerRecordIssue       = "FivePRS:Server:RecordIssue";
        public const string ServerRecordResolve     = "FivePRS:Server:RecordResolve";
        public const string ServerLicenseSetStatus  = "FivePRS:Server:LicenseSetStatus";
        public const string ServerVehicleFlag       = "FivePRS:Server:VehicleFlag";
        public const string ServerEmergencyCall     = "FivePRS:Server:EmergencyCall";
        public const string ServerEmergencyCancel   = "FivePRS:Server:EmergencyCancel";
        public const string ServerEmergencyStatus   = "FivePRS:Server:EmergencyStatus";
        public const string ServerCallClear         = "FivePRS:Server:CallClear";
        public const string ServerAdminRequest      = "FivePRS:Server:AdminRequest";
        public const string ServerAdminRosterSet    = "FivePRS:Server:AdminRosterSet";
        public const string ServerAppearanceSave    = "FivePRS:Server:AppearanceSave";
        public const string ServerSetWallpaper      = "FivePRS:Server:SetWallpaper";

        public const string LocalDutyChanged        = "FivePRS:Local:DutyChanged";
        public const string LocalOpenMenu           = "FivePRS:Local:OpenMenu";
        public const string LocalServerIconDownload = "FivePRS:Local:ServerIconDownload";
        public const string LocalServerIconResult   = "FivePRS:Local:ServerIconResult";
        public const string LocalCalloutReceived    = "FivePRS:Local:CalloutReceived";
        public const string LocalSuspectCuffed      = "FivePRS:Local:SuspectCuffed";
        public const string LocalSuspectUncuffed    = "FivePRS:Local:SuspectUncuffed";
        public const string LocalSuspectEscorted    = "FivePRS:Local:SuspectEscorted";
    }
}
