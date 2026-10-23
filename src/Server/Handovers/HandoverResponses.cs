using FamilyGraph.Application.Handovers;
using Server.People;

namespace Server.Handovers;

/// <summary>A new handover link's secret, shown only now, and when it expires.</summary>
public sealed record CreatedHandoverResponse(string Token, DateTimeOffset ExpiresAt)
{
    internal static CreatedHandoverResponse From(CreatedHandover handover) => new(handover.Token, handover.ExpiresAt);
}

/// <summary>The profile's open handover link, which may have expired (<see cref="ExpiresAt"/> in the past).</summary>
public sealed record OpenHandoverResponse(DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt)
{
    internal static OpenHandoverResponse From(OpenHandover handover) => new(handover.CreatedAt, handover.ExpiresAt);
}

/// <summary>The profile a link hands over, as you may see it now, and when the link expires.</summary>
public sealed record HandoverPreviewResponse(PersonResponse Person, DateTimeOffset ExpiresAt)
{
    internal static HandoverPreviewResponse From(HandoverPreview preview) =>
        new(PersonResponse.From(preview.Person), preview.ExpiresAt);
}

public sealed record AcceptedHandoverResponse(Guid PersonId);
