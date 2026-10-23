namespace FamilyGraph.Business.Handovers;

/// <summary>
/// Where a handover link stands (PRD 01). An open link past its expiry can no longer be accepted;
/// expiry is read from the time, not stored as a state.
/// </summary>
public sealed class HandoverState : BaseEnum<HandoverState>
{
    public static readonly HandoverState Open = new(1, nameof(Open));

    public static readonly HandoverState Accepted = new(2, nameof(Accepted));

    public static readonly HandoverState Cancelled = new(3, nameof(Cancelled));

    private HandoverState(int value, string name)
        : base(value, name)
    {
    }
}
