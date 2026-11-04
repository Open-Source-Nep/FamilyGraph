namespace FamilyGraph.Application.Import;

/// <summary>
/// The sections of an export file as read (D-029, D-033, D-049), before any check: values are kept as
/// they were written, enumerations by name, so <see cref="ImportService"/> can report every problem
/// with its place. Server checks the file's format and version; <see cref="Files"/> reads the image files.
/// </summary>
public sealed record ImportData(
    IReadOnlyList<ImportAccount> Accounts,
    IReadOnlyList<ImportPerson> People,
    IReadOnlyList<ImportLink> Links,
    IReadOnlyList<ImportMarriage> Marriages,
    IReadOnlyList<ImportImage> Images,
    IReadOnlyList<ImportHandover> Handovers,
    IReadOnlyList<ImportAuditEvent> AuditEvents,
    IImportFiles Files,
    IReadOnlyList<ImportClaim>? Claims = null);

/// <summary>The other files of an export, such as images, by their path in it.</summary>
public interface IImportFiles
{
    /// <summary>The file's length in bytes, or <see langword="null"/> when the export has no such file.</summary>
    long? LengthOf(string path);

    /// <summary>Reads a file whose <see cref="LengthOf"/> is known and small.</summary>
    Task<byte[]> ReadAsync(string path, CancellationToken cancellationToken);
}

/// <summary>An account by its ID, which every account reference in the file uses (D-049).</summary>
public sealed record ImportAccount(Guid Id, string? GoogleId, string? Email, bool EmailVerified);

public sealed record ImportPerson(
    Guid Id,
    string? FirstName,
    string? MiddleName,
    string? LastName,
    string? Visibility,
    DateTimeOffset? DeletedAt,
    Guid MaintainerId,
    string? Gender,
    string? Description = null,
    ImportBirthDate? BirthDate = null,
    bool BirthDateShown = false,
    Guid? ImageId = null,
    Guid? ClaimedById = null);

/// <summary>A birth date as exported (D-037).</summary>
public sealed record ImportBirthDate(string? Calendar, int Year, int? Month, int? Day);

public sealed record ImportLink(
    Guid Id,
    Guid ParentId,
    Guid ChildId,
    string? Type,
    string? ParentApproval,
    string? ChildApproval,
    int? BirthOrder,
    DateTimeOffset? DeletedAt = null);

public sealed record ImportMarriage(
    Guid Id,
    Guid FirstSpouseId,
    Guid SecondSpouseId,
    string? FirstApproval,
    string? SecondApproval,
    bool IsFormer,
    DateTimeOffset? DeletedAt = null);

/// <summary>A profile image: its row, and <see cref="File"/>, the path of its bytes in the export.</summary>
public sealed record ImportImage(Guid Id, Guid PersonId, string? File);

public sealed record ImportHandover(
    Guid Id,
    Guid PersonId,
    Guid OfferedById,
    string? TokenHash,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    string? State,
    Guid? AcceptedById,
    DateTimeOffset? ClosedAt);

/// <summary>An identity claim as exported, with its evidence note (D-051).</summary>
public sealed record ImportClaim(
    Guid Id,
    Guid PersonId,
    Guid ClaimantId,
    string? Evidence,
    DateTimeOffset SubmittedAt,
    string? State,
    Guid? ClosedById,
    DateTimeOffset? ClosedAt);

/// <summary>An audit history row as exported; <see cref="Data"/> is the event's JSON object, or null when it was not one.</summary>
public sealed record ImportAuditEvent(
    Guid Id,
    DateTimeOffset OccurredAt,
    string? Family,
    string? Type,
    Guid ActorId,
    Guid SubjectId,
    string? Data);

/// <summary>What an import added.</summary>
/// <param name="CreatedAccounts">File accounts created here with their ID.</param>
/// <param name="ReusedAccounts">File accounts whose Google ID already had an account here, which took their place.</param>
public sealed record ImportResult(
    int People,
    int Links,
    int Marriages,
    int Images,
    int Handovers,
    int AuditEvents,
    int CreatedAccounts,
    int ReusedAccounts,
    int Claims = 0);
