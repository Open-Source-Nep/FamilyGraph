namespace FamilyGraph.Business.Relationships;

/// <summary>
/// One endpoint's consent to a link (PRD 03, D-009). Transitions (D-013): approve moves pending or
/// disagreed to approved; disagree moves pending or approved to disagreed. Nothing returns to pending.
/// </summary>
public sealed class EndpointApproval : BaseEnum<EndpointApproval>
{
    public static readonly EndpointApproval Pending = new(1, nameof(Pending));

    public static readonly EndpointApproval Approved = new(2, nameof(Approved));

    public static readonly EndpointApproval Disagreed = new(3, nameof(Disagreed));

    private EndpointApproval(int value, string name)
        : base(value, name)
    {
    }
}
