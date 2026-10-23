using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.People;

namespace FamilyGraph.Application.Handovers;

/// <summary>A new handover link. <see cref="Token"/> is shown once: only its hash is stored.</summary>
public sealed record CreatedHandover(string Token, DateTimeOffset ExpiresAt);

/// <summary>The maintainer's view of their profile's open handover link, without its token.</summary>
public sealed record OpenHandover(DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);

/// <summary>
/// What the receiver sees before accepting: the profile as they may see it now (a Private profile
/// is a placeholder until they maintain it, PRD 05) and when the link expires. It never names the
/// maintaining account.
/// </summary>
public sealed record HandoverPreview(PersonReference Person, DateTimeOffset ExpiresAt);

public sealed record AcceptedHandover(PersonId PersonId);
