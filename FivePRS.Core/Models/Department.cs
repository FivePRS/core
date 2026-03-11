namespace FivePRS.Core.Models
{
    /// <summary>
    /// Identifies which emergency service a player belongs to.
    /// The numeric value is persisted to the database.
    /// </summary>
    public enum Department
    {
        None   = 0,
        Police = 1,
        EMS    = 2,
        Fire   = 3
    }
}
