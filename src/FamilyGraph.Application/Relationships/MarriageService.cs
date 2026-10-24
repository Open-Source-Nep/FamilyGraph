using FamilyGraph.Application.People;
using FamilyGraph.Application.Persistence;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Application.Relationships;

/// <summary>
/// Creates marriages, changes a spouse's approval, marks a marriage former or current, deletes
/// marriages and lists a person's spouses (PRD 02, PRD 03, D-031). <see cref="Marriage"/> enforces
/// who may do what; this service loads what it needs and saves each change with its audit events.
/// Every person a read returns goes through <see cref="PrivacyProjection"/> (D-025).
/// </summary>
public sealed class MarriageService(
    IPersonStore people,
    IPersonQueries personQueries,
    IMarriageStore marriages,
    IMarriageQueries queries,
    IAuditLog audit,
    IUnitOfWork unitOfWork,
    TimeProvider time,
    RelationshipLimits limits)
{
    public async Task<CreatedMarriage> CreateAsync(
        PersonId spouseId, PersonId otherSpouseId, AccountId actorId, CancellationToken cancellationToken)
    {
        var spouse = await people.FindAsync(spouseId, cancellationToken) ?? throw new PersonNotFoundException();
        var otherSpouse = await people.FindAsync(otherSpouseId, cancellationToken) ?? throw new PersonNotFoundException();
        var differentPeople = spouseId != otherSpouseId;
        var alreadyMarried = differentPeople && await marriages.ExistsAsync(spouseId, otherSpouseId, cancellationToken);
        var directlyRelated = differentPeople && await marriages.AreParentAndChildAsync(spouseId, otherSpouseId, cancellationToken);

        var marriage = Marriage.Create(spouse, otherSpouse, actorId, alreadyMarried, directlyRelated);
        marriages.Add(marriage);
        await SaveAsync(marriage, cancellationToken);

        return new CreatedMarriage(marriage.Id, marriage.FirstSpouseId, marriage.SecondSpouseId, marriage.IsActive);
    }

    /// <summary>Approves for one spouse's side, from pending or disagreed.</summary>
    public async Task<MarriageConsentView> ApproveAsync(
        LinkId marriageId, PersonId spouseId, Viewer actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var (marriage, first, second) = await LoadAsync(marriageId, cancellationToken);

        marriage.Approve(SpouseIn(marriage, spouseId, first, second), actor.AccountId);
        await SaveAsync(marriage, cancellationToken);

        return MarriageConsentView.From(marriage, first, second, actor);
    }

    /// <summary>Declines a pending side or withdraws an approval. The marriage is inactive at once.</summary>
    public async Task<MarriageConsentView> DisagreeAsync(
        LinkId marriageId, PersonId spouseId, Viewer actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var (marriage, first, second) = await LoadAsync(marriageId, cancellationToken);

        var spouse = SpouseIn(marriage, spouseId, first, second);
        marriage.Disagree(spouse, spouse == first ? second : first, actor.AccountId);
        await SaveAsync(marriage, cancellationToken);

        return MarriageConsentView.From(marriage, first, second, actor);
    }

    /// <summary>Marks the marriage former (ended) or current again (D-031).</summary>
    public async Task SetFormerAsync(LinkId marriageId, bool isFormer, AccountId actorId, CancellationToken cancellationToken)
    {
        var (marriage, first, second) = await LoadAsync(marriageId, cancellationToken);

        marriage.SetFormer(first, second, actorId, isFormer);
        await SaveAsync(marriage, cancellationToken);
    }

    /// <summary>Deletes the marriage, in any consent state, when the actor maintains both spouses.</summary>
    public async Task DeleteAsync(LinkId marriageId, AccountId actorId, CancellationToken cancellationToken)
    {
        var (marriage, first, second) = await LoadAsync(marriageId, cancellationToken);

        marriage.Delete(first, second, actorId, time.GetUtcNow());
        await SaveAsync(marriage, cancellationToken);
    }

    /// <summary>The inactive marriages of the profiles the account maintains.</summary>
    public async Task<MarriageConsentList> ListInactiveAsync(Viewer viewer, CancellationToken cancellationToken)
    {
        var list = await queries.ListInactiveMarriagesAsync(viewer, limits.InactiveLinksLimit, cancellationToken);
        return new MarriageConsentList(
            [.. list.Marriages.Select(marriage => MarriageConsentView.From(marriage, viewer))], list.IsTruncated);
    }

    /// <summary>Any signed-in account may list them, like a person's parents and children (D-019).</summary>
    public async Task<SpouseList> ListSpousesAsync(PersonId personId, Viewer viewer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);

        _ = await personQueries.FindAsync(personId, viewer.AccountId, cancellationToken) ?? throw new PersonNotFoundException();
        var list = await queries.ListSpousesAsync(personId, viewer, limits.DirectRelativesLimit, cancellationToken);
        return new SpouseList(
            [.. list.Spouses.Select(spouse =>
                new Spouse(spouse.MarriageId, PrivacyProjection.Project(spouse.Person, viewer), spouse.IsFormer))],
            list.IsTruncated);
    }

    private static Person SpouseIn(Marriage marriage, PersonId spouseId, Person first, Person second) =>
        spouseId == marriage.FirstSpouseId ? first
        : spouseId == marriage.SecondSpouseId ? second
        : throw new NotASpouseException();

    private async Task<(Marriage Marriage, Person First, Person Second)> LoadAsync(
        LinkId marriageId, CancellationToken cancellationToken)
    {
        var marriage = await marriages.FindAsync(marriageId, cancellationToken) ?? throw new MarriageNotFoundException();
        var first = await people.FindAsync(marriage.FirstSpouseId, cancellationToken) ?? throw new PersonNotFoundException();
        var second = await people.FindAsync(marriage.SecondSpouseId, cancellationToken) ?? throw new PersonNotFoundException();
        return (marriage, first, second);
    }

    /// <summary>Saves the change and its audit events in one transaction (D-002, D-015).</summary>
    private async Task SaveAsync(Marriage marriage, CancellationToken cancellationToken)
    {
        audit.Add(marriage.TakeEvents(), time.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>The person named in a consent request is not a spouse in the marriage.</summary>
public sealed class NotASpouseException()
    : Business.DomainException("marriage.not_a_spouse", "This person is not a spouse in this marriage.");
