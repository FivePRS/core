namespace FivePRS.Client.Callouts
{
    public enum CalloutState
    {
        Idle,

        Dispatching,

        Active,

        Completed,

        Failed,

        Declined
    }

    public enum CalloutResult
    {
        Completed,
        Failed,
        Declined
    }
}
