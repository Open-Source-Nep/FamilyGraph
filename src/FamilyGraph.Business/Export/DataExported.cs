using FamilyGraph.Business.Audit;
using FamilyGraph.Business.People;

namespace FamilyGraph.Business.Export;

/// <summary>
/// An administrator exported the instance's data (D-029). It holds only who and, through the audit
/// record, when; nothing from the export itself (D-015). Its subject is the exporting account.
/// </summary>
public sealed record DataExported(AccountId ActorId) : AuditEvent(ActorId)
{
    public override AuditFamily Family => AuditFamily.Administrative;
}

/// <summary>Only administrators may export the instance's data (D-029).</summary>
public sealed class NotAdministratorException()
    : NotPermittedException("export.not_administrator", "Only an administrator can export the data.");
