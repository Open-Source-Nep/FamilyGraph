using FamilyGraph.Business;

namespace FamilyGraph.Application.Relationships;

/// <summary>Which side of a link a consent request is for (D-013, D-021).</summary>
public sealed class LinkEndpoint : BaseEnum<LinkEndpoint>
{
    public static readonly LinkEndpoint Parent = new(1, nameof(Parent));

    public static readonly LinkEndpoint Child = new(2, nameof(Child));

    private LinkEndpoint(int value, string name)
        : base(value, name)
    {
    }
}
