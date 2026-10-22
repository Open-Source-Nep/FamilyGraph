namespace FamilyGraph.Application.Relationships;

/// <summary>
/// Configurable list limits for relationships (<c>Relationships:DirectRelativesLimit</c> and
/// <c>Relationships:InactiveLinksLimit</c>, D-010).
/// </summary>
public sealed record RelationshipLimits(
    int DirectRelativesLimit = RelationshipLimits.DefaultDirectRelativesLimit,
    int InactiveLinksLimit = RelationshipLimits.DefaultInactiveLinksLimit)
{
    public const int DefaultDirectRelativesLimit = 500;

    public const int DefaultInactiveLinksLimit = 500;
}
