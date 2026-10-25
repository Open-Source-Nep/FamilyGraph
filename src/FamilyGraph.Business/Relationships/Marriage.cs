using FamilyGraph.Business.Audit;
using FamilyGraph.Business.People;

namespace FamilyGraph.Business.Relationships;

/// <summary>
/// A directly registered marriage between two people (PRD 02). Consent works as for a
/// <see cref="ParentChildLink"/> (PRD 03): each spouse's side has its own approval, the marriage is
/// active only when both have approved, each side's maintainer approves or disagrees for it, and an
/// account that maintains both spouses deletes it instead. A marriage that ended is marked former;
/// it stays in the graph (D-031). The two spouses are stored in ID order, so a pair has one row.
/// </summary>
public sealed class Marriage : AuditedEntity
{
    private Marriage(
        LinkId id,
        PersonId firstSpouseId,
        PersonId secondSpouseId,
        EndpointApproval firstApproval,
        EndpointApproval secondApproval)
    {
        Id = id;
        FirstSpouseId = firstSpouseId;
        SecondSpouseId = secondSpouseId;
        FirstApproval = firstApproval;
        SecondApproval = secondApproval;
    }

    public LinkId Id { get; }

    /// <summary>The spouse with the lower ID.</summary>
    public PersonId FirstSpouseId { get; }

    /// <summary>The spouse with the higher ID.</summary>
    public PersonId SecondSpouseId { get; }

    public EndpointApproval FirstApproval { get; private set; }

    public EndpointApproval SecondApproval { get; private set; }

    /// <summary>Whether the marriage has ended (divorce or death). No date or reason is kept (D-031).</summary>
    public bool IsFormer { get; private set; }

    /// <summary>When the marriage was deleted, or <see langword="null"/> while it exists.</summary>
    public DateTimeOffset? DeletedAt { get; private set; }

    public bool IsDeleted => DeletedAt is not null;

    public bool IsActive =>
        !IsDeleted && FirstApproval == EndpointApproval.Approved && SecondApproval == EndpointApproval.Approved;

    /// <summary>
    /// Creates a marriage. The creator must maintain at least one spouse; their side(s) start
    /// approved and a side maintained by another account starts pending (PRD 03). A person may have
    /// any number of marriages, but a pair only one (D-031).
    /// </summary>
    /// <param name="alreadyMarried">Whether the pair already has a non-deleted marriage.</param>
    /// <param name="directlyRelated">
    /// Whether one is the other's parent through a non-deleted parent–child link, which rules the
    /// marriage out. Only direct links are checked (D-031).
    /// </param>
    public static Marriage Create(Person spouse, Person otherSpouse, AccountId creatorId, bool alreadyMarried, bool directlyRelated)
    {
        ArgumentNullException.ThrowIfNull(spouse);
        ArgumentNullException.ThrowIfNull(otherSpouse);

        if (spouse.Id == otherSpouse.Id)
        {
            throw new SelfMarriageException();
        }

        var (first, second) = spouse.Id.Value.CompareTo(otherSpouse.Id.Value) < 0 ? (spouse, otherSpouse) : (otherSpouse, spouse);
        var maintainsFirst = first.IsMaintainedBy(creatorId);
        var maintainsSecond = second.IsMaintainedBy(creatorId);
        if (!maintainsFirst && !maintainsSecond)
        {
            throw new NotSpouseMaintainerException();
        }

        if (first.IsDeleted || second.IsDeleted)
        {
            throw new MarriageToDeletedProfileException();
        }

        if (alreadyMarried)
        {
            throw new DuplicateMarriageException();
        }

        if (directlyRelated)
        {
            throw new ParentChildMarriageException();
        }

        var marriage = new Marriage(
            LinkId.New(),
            first.Id,
            second.Id,
            maintainsFirst ? EndpointApproval.Approved : EndpointApproval.Pending,
            maintainsSecond ? EndpointApproval.Approved : EndpointApproval.Pending);
        marriage.Record(new MarriageCreated(
            marriage.Id, marriage.FirstSpouseId, marriage.SecondSpouseId, marriage.FirstApproval, marriage.SecondApproval, creatorId));
        return marriage;
    }

    /// <summary>
    /// Restores a marriage from an export file with its ID, consent and former flag (D-033). The
    /// spouses may come in either order; each keeps its own approval. The import checks the whole
    /// file for duplicates and parent–child pairs first. It records no event: the import's one
    /// <c>DataImported</c> covers it.
    /// </summary>
    public static Marriage Import(
        LinkId id,
        PersonId spouseId,
        PersonId otherSpouseId,
        EndpointApproval spouseApproval,
        EndpointApproval otherSpouseApproval,
        bool isFormer)
    {
        ArgumentNullException.ThrowIfNull(spouseApproval);
        ArgumentNullException.ThrowIfNull(otherSpouseApproval);

        if (spouseId == otherSpouseId)
        {
            throw new SelfMarriageException();
        }

        var marriage = spouseId.Value.CompareTo(otherSpouseId.Value) < 0
            ? new Marriage(id, spouseId, otherSpouseId, spouseApproval, otherSpouseApproval)
            : new Marriage(id, otherSpouseId, spouseId, otherSpouseApproval, spouseApproval);
        marriage.IsFormer = isFormer;
        return marriage;
    }

    /// <summary>The other spouse of <paramref name="spouseId"/>.</summary>
    public PersonId OtherSpouseOf(PersonId spouseId) =>
        spouseId == FirstSpouseId ? SecondSpouseId
        : spouseId == SecondSpouseId ? FirstSpouseId
        : throw new ArgumentException("The person is not a spouse in this marriage.", nameof(spouseId));

    /// <summary>The approval of <paramref name="spouseId"/>'s side.</summary>
    public EndpointApproval ApprovalOf(PersonId spouseId) =>
        IsFirst(spouseId) ? FirstApproval : SecondApproval;

    /// <summary>Approves for <paramref name="spouse"/>'s side, from pending or from disagreed (restoring approval).</summary>
    public void Approve(Person spouse, AccountId actorId)
    {
        var isFirst = IsConsentSide(spouse, actorId);
        EnsureNotDeleted();
        spouse.EnsureNotDeleted();
        if (Approval(isFirst) == EndpointApproval.Approved)
        {
            throw new MarriageConsentUnchangedException();
        }

        var wasDisagreed = Approval(isFirst) == EndpointApproval.Disagreed;
        SetApproval(isFirst, EndpointApproval.Approved);
        Record(wasDisagreed
            ? new MarriageApprovalRestored(Id, spouse.Id, actorId)
            : new MarriageSpouseApproved(Id, spouse.Id, actorId));
    }

    /// <summary>
    /// An administrator approves <paramref name="spouse"/>'s pending side on behalf of its
    /// maintainer (D-040). Only a pending side: a decline or a withdrawn approval stays the
    /// maintainer's to undo (PRD 03).
    /// </summary>
    public void ApproveAsAdministrator(Person spouse, AccountId actorId, bool actorIsAdministrator)
    {
        ArgumentNullException.ThrowIfNull(spouse);

        var isFirst = IsFirst(spouse.Id);
        if (!actorIsAdministrator)
        {
            throw new ApprovalNotAdministratorException();
        }

        EnsureNotDeleted();
        spouse.EnsureNotDeleted();
        if (Approval(isFirst) == EndpointApproval.Approved)
        {
            throw new MarriageConsentUnchangedException();
        }

        if (Approval(isFirst) == EndpointApproval.Disagreed)
        {
            throw new ApprovalNotPendingException();
        }

        SetApproval(isFirst, EndpointApproval.Approved);
        Record(new MarriageSpouseApprovedByAdministrator(Id, spouse.Id, actorId, spouse.MaintainerId));
    }

    /// <summary>
    /// Records disagreement for <paramref name="spouse"/>'s side, from approved or from pending
    /// (declining). The marriage becomes inactive immediately. An account that maintains both
    /// spouses deletes the marriage instead.
    /// </summary>
    public void Disagree(Person spouse, Person otherSpouse, AccountId actorId)
    {
        ArgumentNullException.ThrowIfNull(otherSpouse);

        var isFirst = IsConsentSide(spouse, actorId);
        EnsureNotDeleted();
        spouse.EnsureNotDeleted();
        if (otherSpouse.Id != OtherSpouseOf(spouse.Id))
        {
            throw new ArgumentException("The other spouse must be the other person in this marriage.", nameof(otherSpouse));
        }

        if (otherSpouse.IsMaintainedBy(actorId))
        {
            throw new MarriageSoleMaintainerMustDeleteException();
        }

        if (Approval(isFirst) == EndpointApproval.Disagreed)
        {
            throw new MarriageConsentUnchangedException();
        }

        var wasPending = Approval(isFirst) == EndpointApproval.Pending;
        SetApproval(isFirst, EndpointApproval.Disagreed);
        Record(wasPending
            ? new MarriageSpouseDeclined(Id, spouse.Id, actorId)
            : new MarriageSpouseDisagreed(Id, spouse.Id, actorId));
    }

    /// <summary>
    /// Deletes the marriage, in any consent state, when the actor maintains both spouses (PRD 03).
    /// Deletion is final and only sets <see cref="DeletedAt"/>, so the history is kept.
    /// </summary>
    public void Delete(Person spouse, Person otherSpouse, AccountId actorId, DateTimeOffset deletedAt)
    {
        EnsureSpouses(spouse, otherSpouse);

        if (!spouse.IsMaintainedBy(actorId) || !otherSpouse.IsMaintainedBy(actorId))
        {
            throw new MarriageNotSoleMaintainerException();
        }

        EnsureNotDeleted();

        DeletedAt = deletedAt;
        Record(new MarriageDeleted(Id, deletedAt, actorId));
    }

    /// <summary>
    /// Marks the marriage as former (ended) or current again. The maintainer of either spouse may,
    /// unless that spouse's profile is deleted. It needs no consent and never changes the approvals
    /// or whether the marriage is active (D-031).
    /// </summary>
    public void SetFormer(Person spouse, Person otherSpouse, AccountId actorId, bool isFormer)
    {
        EnsureSpouses(spouse, otherSpouse);

        var maintainsLiving = (spouse.IsMaintainedBy(actorId) && !spouse.IsDeleted)
            || (otherSpouse.IsMaintainedBy(actorId) && !otherSpouse.IsDeleted);
        if (!maintainsLiving)
        {
            throw new NotSpouseMaintainerException();
        }

        EnsureNotDeleted();

        if (IsFormer == isFormer)
        {
            throw new MarriageFormerUnchangedException();
        }

        IsFormer = isFormer;
        Record(new MarriageFormerChanged(Id, isFormer, actorId));
    }

    private void EnsureSpouses(Person spouse, Person otherSpouse)
    {
        ArgumentNullException.ThrowIfNull(spouse);
        ArgumentNullException.ThrowIfNull(otherSpouse);

        if (otherSpouse.Id != OtherSpouseOf(spouse.Id))
        {
            throw new ArgumentException("The spouses must be the people in this marriage.");
        }
    }

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
        {
            throw new MarriageDeletedException();
        }
    }

    /// <summary>Checks that the actor maintains the spouse and tells which side it is.</summary>
    private bool IsConsentSide(Person spouse, AccountId actorId)
    {
        ArgumentNullException.ThrowIfNull(spouse);

        var isFirst = IsFirst(spouse.Id);
        if (!spouse.IsMaintainedBy(actorId))
        {
            throw new MarriageConsentNotMaintainerException();
        }

        return isFirst;
    }

    private bool IsFirst(PersonId spouseId) =>
        spouseId == FirstSpouseId || (spouseId == SecondSpouseId
            ? false
            : throw new ArgumentException("The person is not a spouse in this marriage.", nameof(spouseId)));

    private EndpointApproval Approval(bool first) => first ? FirstApproval : SecondApproval;

    private void SetApproval(bool first, EndpointApproval approval)
    {
        if (first)
        {
            FirstApproval = approval;
        }
        else
        {
            SecondApproval = approval;
        }
    }
}

public sealed class SelfMarriageException()
    : DomainException("marriage.self", "A person cannot be married to themselves.");

public sealed class ParentChildMarriageException()
    : DomainException("marriage.parent_child", "One of these two people is the other's parent, so they cannot be married in the graph.");

public sealed class NotSpouseMaintainerException()
    : NotPermittedException("marriage.not_spouse_maintainer", "Only the maintainer of one of the spouses can do this.");

public sealed class DuplicateMarriageException()
    : ConflictException("marriage.duplicate", "These two people are already married in the graph.");

public sealed class MarriageToDeletedProfileException()
    : ConflictException("marriage.person_deleted", "A marriage cannot be created with a deleted profile.");

public sealed class MarriageConsentNotMaintainerException()
    : NotPermittedException("marriage.consent_not_maintainer", "Only the maintainer of this spouse can change their approval.");

public sealed class MarriageConsentUnchangedException()
    : ConflictException("marriage.consent_unchanged", "This spouse's side is already in the requested approval state.");

public sealed class MarriageSoleMaintainerMustDeleteException()
    : DomainException(
        "marriage.sole_maintainer_must_delete",
        "You maintain both spouses, so delete the marriage instead of disagreeing with it.");

public sealed class MarriageNotSoleMaintainerException()
    : NotPermittedException(
        "marriage.not_sole_maintainer",
        "Only someone who maintains both spouses can delete the marriage. Withdraw your approval instead.");

public sealed class MarriageFormerUnchangedException()
    : ConflictException("marriage.former_unchanged", "The marriage is already marked that way.");

public sealed class MarriageDeletedException()
    : ConflictException("marriage.deleted", "This marriage has been deleted.");
