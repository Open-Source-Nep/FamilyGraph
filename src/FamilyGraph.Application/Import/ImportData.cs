namespace FamilyGraph.Application.Import;

/// <summary>
/// The sections of an export file as read (D-029, D-033), before any check: values are kept as
/// they were written, enumerations by name, so <see cref="ImportService"/> can report every problem
/// with its place. Server checks the file's format and version.
/// </summary>
public sealed record ImportData(
    IReadOnlyList<ImportAccount> Accounts,
    IReadOnlyList<ImportPerson> People,
    IReadOnlyList<ImportLink> Links,
    IReadOnlyList<ImportMarriage> Marriages);

public sealed record ImportAccount(Guid Id, string? Email);

public sealed record ImportPerson(
    Guid Id,
    string? FirstName,
    string? MiddleName,
    string? LastName,
    string? Visibility,
    DateTimeOffset? DeletedAt,
    Guid MaintainerId,
    string? Gender);

public sealed record ImportLink(
    Guid Id,
    Guid ParentId,
    Guid ChildId,
    string? Type,
    string? ParentApproval,
    string? ChildApproval,
    int? BirthOrder);

public sealed record ImportMarriage(
    Guid Id,
    Guid FirstSpouseId,
    Guid SecondSpouseId,
    string? FirstApproval,
    string? SecondApproval,
    bool IsFormer);

/// <summary>What an import added, and how many profiles went to the importing administrator.</summary>
/// <param name="MatchedAccounts">File accounts whose email found exactly one verified account here.</param>
/// <param name="AssignedToImporter">Profiles whose maintainer had no matching account here.</param>
public sealed record ImportResult(int People, int Links, int Marriages, int MatchedAccounts, int AssignedToImporter);
