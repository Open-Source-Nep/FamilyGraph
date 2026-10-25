namespace FamilyGraph.Business.Relationships;

/// <summary>Only an administrator may approve a side on behalf of its maintainer (D-040).</summary>
public sealed class ApprovalNotAdministratorException()
    : NotPermittedException("consent.not_administrator", "Only an administrator can approve on behalf of a maintainer.");

/// <summary>
/// An administrator approves only a side still waiting for its maintainer; a decline or a withdrawn
/// approval stays the maintainer's to undo (D-040, PRD 03).
/// </summary>
public sealed class ApprovalNotPendingException()
    : ConflictException(
        "consent.not_pending",
        "This side is not waiting for approval. Only its maintainer can restore an approval they declined or withdrew.");
