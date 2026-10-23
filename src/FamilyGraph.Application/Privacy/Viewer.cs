using FamilyGraph.Business.People;

namespace FamilyGraph.Application.Privacy;

/// <summary>
/// The signed-in account a read is for. Administrators see through <c>Private</c> placeholders,
/// like a profile's maintainer (PRD 05).
/// </summary>
public sealed record Viewer(AccountId AccountId, bool IsAdministrator);
