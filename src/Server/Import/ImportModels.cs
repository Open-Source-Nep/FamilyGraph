using FamilyGraph.Application.Import;

namespace Server.Import;

/// <summary>
/// The export file's layout as read back (D-029). Missing sections are empty; unknown fields are
/// ignored. Values are checked by <see cref="ImportService"/>, so each problem is reported with its place.
/// </summary>
public sealed record ImportFileRequest(
    string? Format,
    int? Version,
    IReadOnlyList<ImportAccount>? Accounts,
    IReadOnlyList<ImportPerson>? People,
    IReadOnlyList<ImportLink>? Links,
    IReadOnlyList<ImportMarriage>? Marriages)
{
    internal ImportData ToData() => new(Accounts ?? [], People ?? [], Links ?? [], Marriages ?? []);
}

/// <summary>What was imported, and how many profiles the importing administrator now maintains because no account matched.</summary>
public sealed record ImportResponse(int People, int Links, int Marriages, int MatchedAccounts, int AssignedToYou)
{
    internal static ImportResponse From(ImportResult result) =>
        new(result.People, result.Links, result.Marriages, result.MatchedAccounts, result.AssignedToImporter);
}
