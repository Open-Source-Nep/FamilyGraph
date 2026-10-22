namespace FamilyGraph.Business.Relationships;

/// <summary>
/// One endpoint's consent to a link (PRD 03, D-009). Disagreement and the transitions between
/// states belong to the consent lifecycle (#15).
/// </summary>
public enum EndpointApproval
{
    Pending,
    Approved,
}
