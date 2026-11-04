using FamilyGraph.Business.Audit;
using FamilyGraph.Business.People;

namespace FamilyGraph.Business.Claims;

/// <summary>
/// An account's request to be recognised as the person a profile describes (PRD 04, D-051). The
/// profile's maintainer decides it while it is the only open claim on an unclaimed profile;
/// otherwise only an administrator may (#26). Approval makes the claimant the maintainer and the
/// claimed person (<see cref="Person.ClaimedById"/>). Identity claims never touch relationship
/// consent: links and marriages keep every approval as it is.
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

    /// <summary>
    /// Submits a claim to <paramref name="person"/> (PRD 04). Refused for a deleted profile, for
    /// a profile the claimant already maintains (they use <see cref="Person.ClaimAsSelf"/>), and while
    /// the claimant's account already has a claimed profile or an open claim (D-051).
    /// </summary>
    public static ProfileClaim Submit(
        Person person, AccountId claimantId, string? evidence, bool claimantHasClaim, DateTimeOffset now)
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
        DateTimeOffset? closedAt)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new ProfileClaim(id, personId, claimantId, ClaimEvidence.Normalize(evidence), submittedAt)
        {
            State = state,
            ClosedById = closedById,
            ClosedAt = closedAt,
        };
    }

    /// <summary>
    /// Whether the profile's maintainer may decide this claim: it is the only open claim on
    /// <paramref name="person"/> and the profile is not claimed (PRD 04, AC-04.7, AC-04.9).
    /// </summary>
    public static bool MaintainerMayDecide(Person person, int openClaimsOnProfile)
    {
        ArgumentNullException.ThrowIfNull(person);
        return !person.IsClaimed && openClaimsOnProfile <= 1;
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

        if (!MaintainerMayDecide(person, openClaimsOnProfile))
        {
            throw new ClaimNeedsAdministratorException();
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

    private void Close(ClaimState state, AccountId actorId, DateTimeOffset now)
    {
        State = state;
        ClosedById = actorId;
        ClosedAt = now;
    }
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
