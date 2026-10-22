namespace FamilyGraph.Application.Relationships;

/// <summary>Configurable list limits for relationships (<c>Relationships:DirectRelativesLimit</c>, D-010).</summary>
public sealed record RelationshipLimits(int DirectRelativesLimit = RelationshipLimits.DefaultDirectRelativesLimit)
{
    public const int DefaultDirectRelativesLimit = 500;
}
