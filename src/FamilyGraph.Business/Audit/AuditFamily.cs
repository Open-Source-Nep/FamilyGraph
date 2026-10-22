namespace FamilyGraph.Business.Audit;

/// <summary>
/// The family an audit event belongs to (PRD 07, D-015). Families keep identity-claim decisions
/// apart from relationship-consent events.
/// </summary>
public sealed class AuditFamily : BaseEnum<AuditFamily>
{
    public static readonly AuditFamily RelationshipConsent = new(1, nameof(RelationshipConsent));

    public static readonly AuditFamily IdentityClaim = new(2, nameof(IdentityClaim));

    public static readonly AuditFamily Profile = new(3, nameof(Profile));

    public static readonly AuditFamily Merge = new(4, nameof(Merge));

    public static readonly AuditFamily Administrative = new(5, nameof(Administrative));

    private AuditFamily(int value, string name)
        : base(value, name)
    {
    }
}
