using FamilyGraph.Application.People;
using FamilyGraph.Application.Persistence;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.Handovers;
using FamilyGraph.Business.People;

namespace FamilyGraph.Application.Handovers;

/// <summary>
/// Voluntary profile handover (PRD 01): the maintainer creates a one-time link and shares it; any
/// signed-in account that opens it may accept and becomes the maintainer. A profile has at most one
/// open link, and a new one replaces it. <see cref="ProfileHandover"/> and <see cref="Person"/>
/// enforce who may do what; links and their consent never change.
/// </summary>
public sealed class HandoverService(
    IPersonStore people,
    IPersonQueries personQueries,
    IHandoverStore handovers,
    IAuditLog audit,
    IUnitOfWork unitOfWork,
    TimeProvider time,
    HandoverSettings settings)
{
    /// <summary>Creates a link for a profile the actor maintains, cancelling the previous open one.</summary>
    public async Task<CreatedHandover> OfferAsync(PersonId personId, AccountId actorId, CancellationToken cancellationToken)
    {
        var person = await people.FindAsync(personId, cancellationToken) ?? throw new PersonNotFoundException();
        var now = time.GetUtcNow();
        var token = HandoverToken.New();
        // Whole seconds, so the expiry reads back from the database exactly as returned now.
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(now.AddDays(settings.LinkLifetimeDays).ToUnixTimeSeconds());
        var handover = ProfileHandover.Offer(person, actorId, HandoverToken.HashOf(token)!, now, expiresAt);

        var previous = await handovers.FindOpenAsync(personId, cancellationToken);
        previous?.Cancel(person, actorId, now);
        handovers.Add(handover);

        audit.Add([.. previous?.TakeEvents() ?? [], .. handover.TakeEvents()], now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CreatedHandover(token, handover.ExpiresAt);
    }

    /// <summary>The profile's open link, expired or not, for its maintainer only; <see langword="null"/> when there is none.</summary>
    public async Task<OpenHandover?> GetOpenAsync(PersonId personId, AccountId actorId, CancellationToken cancellationToken)
    {
        var person = await people.FindAsync(personId, cancellationToken) ?? throw new PersonNotFoundException();
        if (!person.IsMaintainedBy(actorId))
        {
            throw new NotProfileMaintainerException(person.Id);
        }

        var handover = await handovers.FindOpenAsync(personId, cancellationToken);
        return handover is null ? null : new OpenHandover(handover.CreatedAt, handover.ExpiresAt);
    }

    public async Task CancelAsync(PersonId personId, AccountId actorId, CancellationToken cancellationToken)
    {
        var person = await people.FindAsync(personId, cancellationToken) ?? throw new PersonNotFoundException();
        if (!person.IsMaintainedBy(actorId))
        {
            throw new NotProfileMaintainerException(person.Id);
        }

        var handover = await handovers.FindOpenAsync(personId, cancellationToken) ?? throw new HandoverNotFoundException();
        handover.Cancel(person, actorId, time.GetUtcNow());
        audit.Add(handover.TakeEvents(), time.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Shows the receiver which profile a link hands over. A used, cancelled or void link is 409
    /// <c>handover.closed</c>, an expired one 409 <c>handover.expired</c>.
    /// </summary>
    public async Task<HandoverPreview> PreviewAsync(string token, Viewer viewer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);

        var (handover, person) = await LoadAsync(token, cancellationToken);
        person.EnsureNotDeleted();
        handover.EnsureCanBeAccepted(person, time.GetUtcNow());
        if (person.IsMaintainedBy(viewer.AccountId))
        {
            throw new HandoverOwnProfileException();
        }

        var record = await personQueries.FindAsync(person.Id, viewer.AccountId, cancellationToken) ?? throw new PersonNotFoundException();
        return new HandoverPreview(PrivacyProjection.Project(record, viewer), handover.ExpiresAt);
    }

    /// <summary>Makes the actor the profile's maintainer.</summary>
    public async Task<AcceptedHandover> AcceptAsync(string token, AccountId actorId, CancellationToken cancellationToken)
    {
        var (handover, person) = await LoadAsync(token, cancellationToken);
        var now = time.GetUtcNow();
        person.AcceptHandover(handover, actorId, now);
        audit.Add(person.TakeEvents(), now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new AcceptedHandover(person.Id);
    }

    private async Task<(ProfileHandover Handover, Person Person)> LoadAsync(string token, CancellationToken cancellationToken)
    {
        var hash = HandoverToken.HashOf(token) ?? throw new HandoverNotFoundException();
        var handover = await handovers.FindByTokenHashAsync(hash, cancellationToken) ?? throw new HandoverNotFoundException();
        var person = await people.FindAsync(handover.PersonId, cancellationToken) ?? throw new PersonNotFoundException();
        return (handover, person);
    }
}
