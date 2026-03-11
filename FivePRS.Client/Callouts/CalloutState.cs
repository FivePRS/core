namespace FivePRS.Client.Callouts
{
    /// <summary>
    /// Formal state machine for a callout's lifecycle.
    /// The dispatcher is the sole authority that advances this state.
    /// </summary>
    public enum CalloutState
    {
        Idle,

        Dispatching,

        Active,

        Completed,

        Failed,

        Declined
    }

    /// <summary>Outcome reported to the agency when a callout's active phase ends.</summary>
    public enum CalloutResult
    {
        Completed,
        Failed,
        Declined
    }
}
