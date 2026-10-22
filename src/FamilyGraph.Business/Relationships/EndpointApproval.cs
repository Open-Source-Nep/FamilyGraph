namespace FamilyGraph.Business.Relationships;

/// <summary>
/// One endpoint's consent to a link (PRD 03, D-009). Disagreement and the transitions between
/// states belong to the consent lifecycle (#15).
/// </summary>
public sealed class EndpointApproval : BaseEnum<EndpointApproval>
{
    public static readonly EndpointApproval Pending = new(1, nameof(Pending));

    public static readonly EndpointApproval Approved = new(2, nameof(Approved));

    private EndpointApproval(int value, string name)
        : base(value, name)
    {
    }
}
