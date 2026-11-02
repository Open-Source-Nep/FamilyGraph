using FamilyGraph.Business.Audit;
using FamilyGraph.Business.Handovers;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Application.Export;

/// <summary>
/// An account with only what it stores (D-018): its ID, which everything else refers to, its Google ID
/// (<see langword="null"/> only if it has no Google login), its email and whether Google verified it (D-049).
/// </summary>
public sealed record ExportedAccount(AccountId Id, string? GoogleId, string Email, bool EmailVerified);

/// <summary>
/// A profile as stored. <see cref="Name"/> is <see langword="null"/> for a deleted profile, whose name was erased,
/// and <see cref="Gender"/>, <see cref="Description"/> and <see cref="BirthDate"/> when not set or erased.
/// </summary>
public sealed record ExportedPerson(
    PersonId Id,
    PersonName? Name,
    ProfileVisibility Visibility,
    DateTimeOffset? DeletedAt,
    AccountId MaintainerId,
    Gender? Gender,
    string? Description,
    BirthDate? BirthDate,
    bool BirthDateShown,
    ProfileImageId? ImageId);

/// <summary>A parent–child link with both endpoints' consent, active or not, and when it was deleted (D-049).</summary>
public sealed record ExportedLink(
    LinkId Id,
    PersonId ParentId,
    PersonId ChildId,
    ParentChildType Type,
    EndpointApproval ParentApproval,
    EndpointApproval ChildApproval,
    int? BirthOrder,
    DateTimeOffset? DeletedAt);

/// <summary>A marriage with both spouses' consent, active or not, current or former (D-031), and when it was deleted (D-049).</summary>
public sealed record ExportedMarriage(
    LinkId Id,
    PersonId FirstSpouseId,
    PersonId SecondSpouseId,
    EndpointApproval FirstApproval,
    EndpointApproval SecondApproval,
    bool IsFormer,
    DateTimeOffset? DeletedAt);

/// <summary>A profile image's row without its content, which <see cref="ExportedImageContent"/> carries (D-028, D-049).</summary>
public sealed record ExportedImage(ProfileImageId Id, PersonId PersonId, ImageFormat Format);

/// <summary>A profile image's bytes as stored.</summary>
public sealed record ExportedImageContent(ProfileImageId Id, ImageFormat Format, byte[] Content);

/// <summary>A handover link as stored: only the hash of its secret, never the link (D-027, D-049).</summary>
public sealed record ExportedHandover(
    HandoverId Id,
    PersonId PersonId,
    AccountId OfferedById,
    string TokenHash,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    HandoverState State,
    AccountId? AcceptedById,
    DateTimeOffset? ClosedAt);

/// <summary>
/// An audit history row as stored (D-015): <see cref="Type"/> is the event's name and <see cref="Data"/>
/// its other fields as a JSON object, with IDs only.
/// </summary>
public sealed record ExportedAuditEvent(
    Guid Id,
    DateTimeOffset OccurredAt,
    AuditFamily Family,
    string Type,
    AccountId ActorId,
    Guid SubjectId,
    string Data);
