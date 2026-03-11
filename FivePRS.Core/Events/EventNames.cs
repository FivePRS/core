namespace FivePRS.Core.Events
{
    /// <summary>
    /// Single source of truth for all network event names.
    /// Both client and server reference this class to avoid typos.
    /// </summary>
    public static class EventNames
    {
        public const string ClientReceivePlayerData = "FivePRS:Client:ReceivePlayerData";

        public const string ClientDutyStatusChanged = "FivePRS:Client:DutyStatusChanged";

        public const string ClientCalloutStarted    = "FivePRS:Client:CalloutStarted";

        public const string ClientCalloutEnded      = "FivePRS:Client:CalloutEnded";

        public const string ClientRankedUp           = "FivePRS:Client:RankedUp";

        public const string ServerPlayerConnected   = "FivePRS:Server:PlayerConnected";

        public const string ServerToggleDuty        = "FivePRS:Server:ToggleDuty";

        public const string ServerSetDepartment     = "FivePRS:Server:SetDepartment";

        public const string ServerCalloutCompleted  = "FivePRS:Server:CalloutCompleted";

        public const string LocalDutyChanged        = "FivePRS:Local:DutyChanged";

        public const string LocalCalloutReceived    = "FivePRS:Local:CalloutReceived";

        public const string LocalSuspectCuffed      = "FivePRS:Local:SuspectCuffed";

        public const string LocalSuspectUncuffed    = "FivePRS:Local:SuspectUncuffed";

        public const string LocalSuspectEscorted    = "FivePRS:Local:SuspectEscorted";
    }
}
