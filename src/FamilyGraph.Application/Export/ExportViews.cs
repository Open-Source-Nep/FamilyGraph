using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Application.Export;

/// <summary>An account that maintains at least one profile, with only what the account stores (D-018).</summary>
public sealed record ExportedAccount(AccountId Id, string Email, bool EmailVerified);

/// <summary>
/// A profile as stored. <see cref="Name"/> is <see langword="null"/> for a deleted profile, whose name was erased,
/// and <see cref="Gender"/> when it is not set or was erased.
/// </summary>
public sealed record ExportedPerson(
    PersonId Id,
    PersonName? Name,
    ProfileVisibility Visibility,
    DateTimeOffset? DeletedAt,
    AccountId MaintainerId,
    Gender? Gender);

/// <summary>A non-deleted parent–child link with both endpoints' consent, active or not.</summary>
public sealed record ExportedLink(
    LinkId Id,
    PersonId ParentId,
    PersonId ChildId,
    ParentChildType Type,
    EndpointApproval ParentApproval,
    EndpointApproval ChildApproval,
    int? BirthOrder);

/// <summary>A non-deleted marriage with both spouses' consent, active or not, current or former (D-031).</summary>
public sealed record ExportedMarriage(
    LinkId Id,
    PersonId FirstSpouseId,
    PersonId SecondSpouseId,
    EndpointApproval FirstApproval,
    EndpointApproval SecondApproval,
    bool IsFormer);
