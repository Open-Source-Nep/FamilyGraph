using FamilyGraph.Business.Audit;
using FamilyGraph.Business.People;

namespace FamilyGraph.Business.Import;

/// <summary>
/// An administrator imported an export file into the empty instance (D-033). It is the import's
/// only audit event: the imported profiles, links and marriages record none of their own. It holds
/// only who and, through the audit record, when (D-015). Its subject is the importing account.
/// </summary>
public sealed record DataImported(AccountId ActorId) : AuditEvent(ActorId)
{
    public override AuditFamily Family => AuditFamily.Administrative;
}

/// <summary>Only administrators may import data (D-033).</summary>
public sealed class ImportNotAdministratorException()
    : NotPermittedException("import.not_administrator", "Only an administrator can import data.");
