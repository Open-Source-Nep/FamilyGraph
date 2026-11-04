using FamilyGraph.Business.Audit;
using FamilyGraph.Business.People;

namespace FamilyGraph.Business.Claims;

/// <summary>
/// An account's request to be recognised as the person a profile describes (PRD 04, D-051). The
/// profile's maintainer decides it while it is the only open claim on an unclaimed profile and the
/// claimant has not asked for review; otherwise an administrator decides it, with a rationale
/// (D-052). Approval makes the claimant the maintainer and the claimed person
/// (<see cref="Person.ClaimedById"/>). Identity claims never touch relationship consent: links and
/// marriages keep every approval as it is.
/// </summary>
public sealed class ProfileClaim : AuditedEntity
{
    private ProfileClaim(ClaimId id, PersonId personId, AccountId claimantId, string? evidence, DateTimeOffset submittedAt)
    {
        Id = id;
        PersonId = personId;
        ClaimantId = claimantId;
        Evidence = evidence;
        SubmittedAt = submittedAt;
    }

    public ClaimId Id { get; }

    public PersonId PersonId { get; }

    public AccountId ClaimantId { get; }

    /// <summary>The claimant's note, or <see langword="null"/> (see <see cref="ClaimEvidence"/>).</summary>
    public string? Evidence { get; private set; }

    public DateTimeOffset SubmittedAt { get; }

    public ClaimState State { get; private set; } = ClaimState.Open;

    public bool IsOpen => State == ClaimState.Open;

    /// <summary>Who approved, rejected, withdrew or cancelled the claim, or <see langword="null"/> while it is open.</summary>
    public AccountId? ClosedById { get; private set; }

    /// <summary>When the claim stopped being open, or <see langword="null"/>.</summary>
    public DateTimeOffset? ClosedAt { get; private set; }

    /// <summary>When the claimant asked administrators to review it, or <see langword="null"/> (D-052).</summary>
    public DateTimeOffset? ReviewRequestedAt { get; private set; }

    public bool ReviewRequested => ReviewRequestedAt is not null;

    /// <summary>Whether the claim was reopened by an appeal of the maintainer's rejection; once only (AC-04.8).</summary>
    public bool Appealed { get; private set; }

    /// <summary>Whether an administrator approved or rejected it, rather than the maintainer.</summary>
    public bool DecidedByAdministrator { get; private set; }

    /// <summary>
    /// The administrator's reason for the decision. It is kept with the decision when the notes are
    /// erased (AC-04.10) and shown to administrators only (AC-04.5).
    /// </summary>
    public string? Rationale { get; private set; }

    /// <summary>The administrator's request for more evidence, for the claimant.</summary>
    public string? AdministratorQuestion { get; private set; }

    /// <summary>The reply of a claimed profile's maintainer, for administrators (AC-04.9).</summary>
    public string? MaintainerResponse { get; private set; }

    /// <summary>When the note, question and reply were erased after the retention period (AC-04.10).</summary>
    public DateTimeOffset? NotesErasedAt { get; private set; }

    /// <summary>
    /// Submits a claim to <paramref name="person"/> (PRD 04). Refused for a deleted profile, for
    /// a profile the claimant already maintains (they use <see cref="Person.ClaimAsSelf"/>), while
    /// the claimant's account already has a claimed profile or an open claim (D-051), and during the
    /// waiting period after an administrator rejected the claimant's claim on it (AC-04.8).
    /// </summary>
    public static ProfileClaim Submit(
        Person person, AccountId claimantId, string? evidence, bool claimantHasClaim, DateTimeOffset now, DateTimeOffset? waitUntil = null)
    {
        ArgumentNullException.ThrowIfNull(person);

        person.EnsureNotDeleted();
        if (person.IsMaintainedBy(claimantId))
        {
            throw new ClaimOwnProfileException();
        }

        if (claimantHasClaim)
        {
            throw new AccountAlreadyClaimedException();
        }

        if (waitUntil is { } until && now < until)
        {
            throw new ClaimWaitingPeriodException(until);
        }

        var claim = new ProfileClaim(ClaimId.New(), person.Id, claimantId, ClaimEvidence.Normalize(evidence), now);
        claim.Record(new ClaimSubmitted(claim.Id, person.Id, claimantId, claim.Evidence is not null));
        return claim;
    }

    /// <summary>Restores a claim from an export file with its ID and state. It records no event.</summary>
    public static ProfileClaim Import(
        ClaimId id,
        PersonId personId,
        AccountId claimantId,
        string? evidence,
        DateTimeOffset submittedAt,
        ClaimState state,
        AccountId? closedById,
        DateTimeOffset? closedAt,
        ClaimReview? review = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        review ??= ClaimReview.None;
        return new ProfileClaim(id, personId, claimantId, ClaimEvidence.Normalize(evidence), submittedAt)
        {
            State = state,
            ClosedById = closedById,
            ClosedAt = closedAt,
            ReviewRequestedAt = review.ReviewRequestedAt,
            Appealed = review.Appealed,
            DecidedByAdministrator = review.DecidedByAdministrator,
            Rationale = review.Rationale is null ? null : ClaimText.Rationale(review.Rationale),
            AdministratorQuestion = review.AdministratorQuestion is null ? null : ClaimText.Question(review.AdministratorQuestion),
            MaintainerResponse = ClaimText.Response(review.MaintainerResponse),
            NotesErasedAt = review.NotesErasedAt,
        };
    }

    /// <summary>
    /// Whether the profile's maintainer may decide claims on <paramref name="person"/>: it has one
    /// open claim and is not claimed (PRD 04, AC-04.7, AC-04.9).
    /// </summary>
    public static bool MaintainerMayDecide(Person person, int openClaimsOnProfile)
    {
        ArgumentNullException.ThrowIfNull(person);
        return MaintainerMayDecide(person.IsClaimed, openClaimsOnProfile);
    }

    /// <inheritdoc cref="MaintainerMayDecide(Person, int)"/>
    public static bool MaintainerMayDecide(bool profileClaimed, int openClaimsOnProfile) =>
        !profileClaimed && openClaimsOnProfile <= 1;

    /// <summary>
    /// Whether this open claim is the maintainer's to decide: the profile allows it and the claimant
    /// has not asked for review. Otherwise it is an administrator's (D-052).
    /// </summary>
    public bool IsForMaintainer(Person person, int openClaimsOnProfile)
    {
        ArgumentNullException.ThrowIfNull(person);
        return IsForMaintainer(person.IsClaimed, openClaimsOnProfile);
    }

    /// <inheritdoc cref="IsForMaintainer(Person, int)"/>
    public bool IsForMaintainer(bool profileClaimed, int openClaimsOnProfile) =>
        IsOpen && !ReviewRequested && MaintainerMayDecide(profileClaimed, openClaimsOnProfile);

    /// <summary>Whether this open claim waits for an administrator (D-052).</summary>
    public bool IsForAdministrators(bool profileClaimed, int openClaimsOnProfile) =>
        IsOpen && !IsForMaintainer(profileClaimed, openClaimsOnProfile);

    /// <summary>
    /// Whether the claimant may ask administrators to review the claim now (AC-04.2, AC-04.6,
    /// AC-04.8): an open claim with the maintainer that waited <paramref name="maintainerUnavailable"/>,
    /// or the maintainer's rejection, appealed once, before the notes are erased and while the
    /// account has no other claim.
    /// </summary>
    public bool MayRequestReview(
        bool profileDeleted, bool profileClaimed, int openClaimsOnProfile, bool maintainerUnavailable, bool claimantHasOtherClaim)
    {
        if (profileDeleted)
        {
            return false;
        }

        if (IsOpen)
        {
            return maintainerUnavailable && IsForMaintainer(profileClaimed, openClaimsOnProfile);
        }

        return State == ClaimState.Rejected && !DecidedByAdministrator && !Appealed && NotesErasedAt is null
            && !claimantHasOtherClaim;
    }

    /// <summary>
    /// The claimant asks administrators to review the claim (D-052). An open claim stays open and
    /// goes to administrators; a claim the maintainer rejected is reopened as an appeal. See
    /// <see cref="MayRequestReview"/> for when it is allowed.
    /// </summary>
    public void RequestReview(
        Person person, AccountId actorId, int openClaimsOnProfile, bool maintainerUnavailable, bool claimantHasOtherClaim, DateTimeOffset now)
    {
        EnsureFor(person);
        if (actorId != ClaimantId)
        {
            throw new NotClaimantException();
        }

        person.EnsureNotDeleted();
        if (IsOpen)
        {
            if (!IsForMaintainer(person, openClaimsOnProfile))
            {
                throw new ClaimAlreadyWithAdministratorsException();
            }

            if (!maintainerUnavailable)
            {
                throw new ClaimReviewTooEarlyException();
            }
        }
        else
        {
            if (State != ClaimState.Rejected || DecidedByAdministrator || Appealed)
            {
                throw new ClaimNotAppealableException();
            }

            if (NotesErasedAt is not null)
            {
                throw new ClaimAppealTooLateException();
            }

            if (claimantHasOtherClaim)
            {
                throw new AccountAlreadyClaimedException();
            }

            Appealed = true;
            State = ClaimState.Open;
            ClosedById = null;
            ClosedAt = null;
        }

        ReviewRequestedAt = now;
        Record(new ClaimReviewRequested(Id, PersonId, actorId, Appealed));
    }

    /// <summary>
    /// The maintainer approves: the claimant becomes the maintainer and the claimed person. Consent
    /// on the profile's links and marriages is unchanged (AC-04.3, AC-04.4).
    /// </summary>
    public void Approve(Person person, AccountId actorId, int openClaimsOnProfile, DateTimeOffset now)
    {
        EnsureMaintainerMayDecide(person, actorId, openClaimsOnProfile);

        Close(ClaimState.Approved, actorId, now);
        person.AcceptClaim(this);
        Record(new ClaimApproved(Id, PersonId, actorId, ClaimantId));
    }

    /// <summary>The maintainer rejects; nothing about the profile changes (AC-04.1).</summary>
    public void Reject(Person person, AccountId actorId, int openClaimsOnProfile, DateTimeOffset now)
    {
        EnsureMaintainerMayDecide(person, actorId, openClaimsOnProfile);

        Close(ClaimState.Rejected, actorId, now);
        Record(new ClaimRejected(Id, PersonId, actorId, ClaimantId));
    }

    /// <summary>
    /// An administrator approves a claim waiting for administrators, with a rationale (D-052). The
    /// claimant becomes the maintainer and the claimed person, in place of anyone who had claimed
    /// the profile. The profile's <paramref name="otherOpenClaims"/> are rejected with the same
    /// rationale, as they are decided together (PRD 04). Consent is unchanged (AC-04.4).
    /// </summary>
    public void ApproveAsAdministrator(
        Person person, AccountId actorId, bool actorIsAdministrator, string? rationale, IReadOnlyList<ProfileClaim> otherOpenClaims, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(otherOpenClaims);
        var reason = EnsureAdministratorMayDecide(person, actorIsAdministrator, rationale, otherOpenClaims.Count + 1);

        var previous = person.ClaimedById;
        CloseByAdministrator(ClaimState.Approved, actorId, reason, now);
        person.AcceptClaim(this);
        Record(new ClaimApprovedByAdministrator(Id, PersonId, actorId, ClaimantId, previous));

        foreach (var other in otherOpenClaims)
        {
            other.EnsureFor(person);
            if (other.Id == Id || !other.IsOpen)
            {
                throw new ArgumentException("The other claims must be the profile's other open claims.", nameof(otherOpenClaims));
            }

            other.CloseByAdministrator(ClaimState.Rejected, actorId, reason, now);
            other.Record(new ClaimRejectedByAdministrator(other.Id, PersonId, actorId, other.ClaimantId));
        }
    }

    /// <summary>
    /// An administrator rejects a claim waiting for administrators, with a rationale (D-052). The
    /// claimant may claim the profile again only after the waiting period (AC-04.8).
    /// </summary>
    public void RejectAsAdministrator(
        Person person, AccountId actorId, bool actorIsAdministrator, string? rationale, int openClaimsOnProfile, DateTimeOffset now)
    {
        var reason = EnsureAdministratorMayDecide(person, actorIsAdministrator, rationale, openClaimsOnProfile);

        CloseByAdministrator(ClaimState.Rejected, actorId, reason, now);
        Record(new ClaimRejectedByAdministrator(Id, PersonId, actorId, ClaimantId));
    }

    /// <summary>An administrator asks the claimant for more evidence (PRD 04 manual review).</summary>
    public void AskForEvidence(
        Person person, AccountId actorId, bool actorIsAdministrator, string? question, int openClaimsOnProfile)
    {
        EnsureAdministrator(person, actorIsAdministrator, openClaimsOnProfile);

        AdministratorQuestion = ClaimText.Question(question);
        Record(new ClaimEvidenceRequested(Id, PersonId, actorId));
    }

    /// <summary>The claimant changes the note of their open claim, for example to answer a question.</summary>
    public void ChangeEvidence(AccountId actorId, string? evidence)
    {
        if (actorId != ClaimantId)
        {
            throw new NotClaimantException();
        }

        EnsureOpen();
        Evidence = ClaimEvidence.Normalize(evidence);
        Record(new ClaimEvidenceChanged(Id, PersonId, actorId, Evidence is not null));
    }

    /// <summary>
    /// The maintainer of a claimed profile replies to an open claim on it; administrators read the
    /// reply, and the maintainer does not decide (AC-04.9). A blank reply clears it.
    /// </summary>
    public void Respond(Person person, AccountId actorId, string? response)
    {
        EnsureFor(person);
        EnsureOpen();
        person.EnsureNotDeleted();
        if (!person.IsMaintainedBy(actorId))
        {
            throw new NotProfileMaintainerException(person.Id);
        }

        if (!person.IsClaimed)
        {
            throw new ClaimResponseNotNeededException();
        }

        MaintainerResponse = ClaimText.Response(response);
        Record(new ClaimResponded(Id, PersonId, actorId, MaintainerResponse is not null));
    }

    /// <summary>
    /// Erases the note, question and reply of a closed claim after the retention period (AC-04.10).
    /// The decision and rationale stay. It records no event: no one acts, and the audit history never
    /// held the notes.
    /// </summary>
    public void EraseNotes(DateTimeOffset now)
    {
        if (IsOpen)
        {
            throw new InvalidOperationException("An open claim keeps its notes.");
        }

        Evidence = null;
        AdministratorQuestion = null;
        MaintainerResponse = null;
        NotesErasedAt ??= now;
    }

    /// <summary>The claimant takes back an open claim, which frees their account to claim again.</summary>
    public void Withdraw(AccountId actorId, DateTimeOffset now)
    {
        if (actorId != ClaimantId)
        {
            throw new NotClaimantException();
        }

        EnsureOpen();
        Close(ClaimState.Withdrawn, actorId, now);
        Record(new ClaimWithdrawn(Id, PersonId, actorId));
    }

    /// <summary>Closes an open claim because <paramref name="person"/> was deleted by <paramref name="actorId"/>.</summary>
    public void CancelForDeletedProfile(Person person, AccountId actorId, DateTimeOffset now)
    {
        EnsureFor(person);
        if (!person.IsDeleted)
        {
            throw new InvalidOperationException("Only a deleted profile's claims are cancelled.");
        }

        EnsureOpen();
        Close(ClaimState.Cancelled, actorId, now);
        Record(new ClaimCancelled(Id, PersonId, actorId, ClaimantId));
    }

    private void EnsureMaintainerMayDecide(Person person, AccountId actorId, int openClaimsOnProfile)
    {
        EnsureFor(person);
        EnsureOpen();
        person.EnsureNotDeleted();
        if (!person.IsMaintainedBy(actorId))
        {
            throw new NotProfileMaintainerException(person.Id);
        }

        if (!IsForMaintainer(person, openClaimsOnProfile))
        {
            throw new ClaimNeedsAdministratorException();
        }
    }

    private string EnsureAdministratorMayDecide(Person person, bool actorIsAdministrator, string? rationale, int openClaimsOnProfile)
    {
        EnsureAdministrator(person, actorIsAdministrator, openClaimsOnProfile);
        return ClaimText.Rationale(rationale);
    }

    private void EnsureAdministrator(Person person, bool actorIsAdministrator, int openClaimsOnProfile)
    {
        EnsureFor(person);
        if (!actorIsAdministrator)
        {
            throw new ClaimNotAdministratorException();
        }

        EnsureOpen();
        person.EnsureNotDeleted();
        if (!IsForAdministrators(person.IsClaimed, openClaimsOnProfile))
        {
            throw new ClaimForMaintainerException();
        }
    }

    private void EnsureOpen()
    {
        if (!IsOpen)
        {
            throw new ClaimClosedException();
        }
    }

    private void EnsureFor(Person person)
    {
        ArgumentNullException.ThrowIfNull(person);
        if (person.Id != PersonId)
        {
            throw new ArgumentException("The claim is for another profile.", nameof(person));
        }
    }

    private void CloseByAdministrator(ClaimState state, AccountId actorId, string rationale, DateTimeOffset now)
    {
        Close(state, actorId, now);
        DecidedByAdministrator = true;
        Rationale = rationale;
    }

    private void Close(ClaimState state, AccountId actorId, DateTimeOffset now)
    {
        State = state;
        ClosedById = actorId;
        ClosedAt = now;
    }
}

/// <summary>The review fields of an imported claim (D-052).</summary>
public sealed record ClaimReview(
    DateTimeOffset? ReviewRequestedAt,
    bool Appealed,
    bool DecidedByAdministrator,
    string? Rationale,
    string? AdministratorQuestion,
    string? MaintainerResponse,
    DateTimeOffset? NotesErasedAt)
{
    public static readonly ClaimReview None = new(null, false, false, null, null, null, null);
}

public sealed class ClaimOwnProfileException()
    : ConflictException("claim.own_profile", "You maintain this profile; mark it as yourself instead.");

/// <summary>An account has at most one claimed profile, and no second claim while it has one or an open claim (D-051).</summary>
public sealed class AccountAlreadyClaimedException()
    : ConflictException("claim.account_has_claim", "Your account already has a claimed profile or an open claim.");

public sealed class ClaimClosedException()
    : ConflictException("claim.closed", "This claim was already decided, withdrawn or cancelled.");

/// <summary>Several open claims, or a claim on a claimed profile, are decided by an administrator (AC-04.7, AC-04.9).</summary>
public sealed class ClaimNeedsAdministratorException()
    : NotPermittedException("claim.administrator_only", "Only an administrator can decide this claim.");

public sealed class NotClaimantException()
    : NotPermittedException("claim.not_claimant", "Only the claimant can withdraw a claim.");

public sealed class ClaimNotAdministratorException()
    : NotPermittedException("claim.not_administrator", "Only an administrator can review claims.");

/// <summary>An administrator does not decide a claim that is still the maintainer's (D-052).</summary>
public sealed class ClaimForMaintainerException()
    : ConflictException("claim.for_maintainer", "The profile's maintainer decides this claim.");

public sealed class ClaimAlreadyWithAdministratorsException()
    : ConflictException("claim.with_administrators", "Administrators already review this claim.");

/// <summary>The maintainer has the response time to answer before the claimant may ask for review (AC-04.6).</summary>
public sealed class ClaimReviewTooEarlyException()
    : ConflictException("claim.review_too_early", "The maintainer still has time to answer this claim.");

/// <summary>Only the maintainer's rejection is appealed, and once (AC-04.8).</summary>
public sealed class ClaimNotAppealableException()
    : ConflictException("claim.not_appealable", "This claim cannot be appealed.");

public sealed class ClaimAppealTooLateException()
    : ConflictException("claim.appeal_too_late", "This claim's note was erased, so it can no longer be appealed.");

public sealed class ClaimWaitingPeriodException(DateTimeOffset until)
    : ConflictException("claim.waiting_period", $"An administrator rejected your claim; you can claim this profile again from {until:yyyy-MM-dd}.")
{
    public DateTimeOffset Until { get; } = until;
}

public sealed class ClaimResponseNotNeededException()
    : ConflictException("claim.not_claimed_profile", "Only the maintainer of a claimed profile replies to a claim.");
