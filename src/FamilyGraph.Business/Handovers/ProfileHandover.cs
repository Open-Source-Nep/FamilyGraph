using FamilyGraph.Business.Audit;
using FamilyGraph.Business.People;

namespace FamilyGraph.Business.Handovers;

/// <summary>
/// A maintainer's offer to hand a profile over to another account (PRD 01). The maintainer shares
/// a one-time link, and whoever opens it signed in may accept with
/// <see cref="Person.AcceptHandover"/> until it expires. Only <see cref="TokenHash"/> is stored, so
/// the link itself cannot be read back. A handover never touches the profile's links or consent.
/// </summary>
public sealed class ProfileHandover : AuditedEntity
{
    private ProfileHandover(
        HandoverId id,
        PersonId personId,
        AccountId offeredById,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        Id = id;
        PersonId = personId;
        OfferedById = offeredById;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public HandoverId Id { get; }

    public PersonId PersonId { get; }

    /// <summary>The maintainer who created the link. It is void once someone else maintains the profile.</summary>
    public AccountId OfferedById { get; }

    /// <summary>A hash of the link's secret, which Application creates and looks links up by.</summary>
    public string TokenHash { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset ExpiresAt { get; }

    public HandoverState State { get; private set; } = HandoverState.Open;

    /// <summary>The account that accepted the link, or <see langword="null"/>.</summary>
    public AccountId? AcceptedById { get; private set; }

    /// <summary>When the link was accepted or cancelled, or <see langword="null"/> while it is open.</summary>
    public DateTimeOffset? ClosedAt { get; private set; }

    /// <summary>
    /// Creates a handover link for <paramref name="person"/>. Only its current maintainer may, and
    /// not for a deleted profile. Claimed profiles will be refused once claims exist (PRD 01).
    /// </summary>
    public static ProfileHandover Offer(
        Person person,
        AccountId actorId,
        string tokenHash,
        DateTimeOffset now,
        DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(person);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        if (expiresAt <= now)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAt), "A handover link must expire after it is created.");
        }

        person.EnsureNotDeleted();
        if (!person.IsMaintainedBy(actorId))
        {
            throw new NotProfileMaintainerException(person.Id);
        }

        var handover = new ProfileHandover(HandoverId.New(), person.Id, actorId, tokenHash, now, expiresAt);
        handover.Record(new HandoverOffered(handover.Id, person.Id, actorId, expiresAt));
        return handover;
    }

    /// <summary>
    /// Whether the link can still be accepted: open, not expired, and its creator still maintains
    /// <paramref name="person"/>.
    /// </summary>
    public bool CanBeAccepted(Person person, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(person);
        return State == HandoverState.Open && now < ExpiresAt && person.IsMaintainedBy(OfferedById);
    }

    /// <summary>Throws unless the link can still be accepted (see <see cref="CanBeAccepted"/>).</summary>
    public void EnsureCanBeAccepted(Person person, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(person);
        EnsureFor(person);
        if (State != HandoverState.Open || !person.IsMaintainedBy(OfferedById))
        {
            throw new HandoverClosedException();
        }

        if (now >= ExpiresAt)
        {
            throw new HandoverExpiredException();
        }
    }

    /// <summary>
    /// Cancels the link, expired or not, so it can no longer be accepted. Only the profile's
    /// current maintainer may.
    /// </summary>
    public void Cancel(Person person, AccountId actorId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(person);
        EnsureFor(person);
        if (!person.IsMaintainedBy(actorId))
        {
            throw new NotProfileMaintainerException(person.Id);
        }

        if (State != HandoverState.Open)
        {
            throw new HandoverClosedException();
        }

        State = HandoverState.Cancelled;
        ClosedAt = now;
        Record(new HandoverCancelled(Id, PersonId, actorId));
    }

    /// <summary>Called by <see cref="Person.AcceptHandover"/>, which records the event.</summary>
    internal void MarkAccepted(AccountId acceptedById, DateTimeOffset now)
    {
        State = HandoverState.Accepted;
        AcceptedById = acceptedById;
        ClosedAt = now;
    }

    private void EnsureFor(Person person)
    {
        if (person.Id != PersonId)
        {
            throw new ArgumentException("The handover is for another profile.", nameof(person));
        }
    }
}

public sealed class HandoverClosedException()
    : ConflictException("handover.closed", "This handover link was already used or cancelled.");

public sealed class HandoverExpiredException()
    : ConflictException("handover.expired", "This handover link has expired.");

public sealed class HandoverOwnProfileException()
    : ConflictException("handover.own_profile", "You already maintain this profile.");

/// <summary>Another handover link for the profile was created at the same moment.</summary>
public sealed class HandoverAlreadyOpenException()
    : ConflictException("handover.already_open", "Another handover link for this profile was just created.");
