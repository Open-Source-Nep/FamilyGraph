using FamilyGraph.Application.People;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.People;
using FamilyGraph.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyGraph.Infrastructure.People;

internal sealed class EfPersonQueries(FamilyGraphDbContext context) : IPersonQueries
{
    public Task<PersonRecord?> FindAsync(PersonId id, AccountId viewerId, CancellationToken cancellationToken) =>
        context.Set<Person>()
            .AsNoTracking()
            .Where(person => person.Id == id)
            .Select(person => new PersonRecord(
                person.Id, person.Name, person.Visibility, person.DeletedAt != null, person.MaintainerId == viewerId, person.ImageId, person.Gender, person.Description, person.BirthDate, person.BirthDateShown))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<ImageContent?> FindImageAsync(ProfileImageId id, CancellationToken cancellationToken) =>
        context.Set<ProfileImage>()
            .AsNoTracking()
            .Where(image => image.Id == id)
            .Select(image => new ImageContent(image.Id, image.Format, image.Content))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<ProfileList> ListMaintainedByAsync(AccountId maintainerId, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        // One extra row tells whether the list was cut short.
        var people = await context.Set<Person>()
            .AsNoTracking()
            .Where(person => person.MaintainerId == maintainerId && person.DeletedAt == null)
            .OrderBy(person => person.Name.FirstName)
            .ThenBy(person => person.Name.MiddleName)
            .ThenBy(person => person.Name.LastName)
            .ThenBy(person => person.Id)
            .Select(person => new PersonSummary(person.Id, person.Name, person.Visibility, person.Gender))
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        return people.Count > limit
            ? new ProfileList(people[..limit], IsTruncated: true)
            : new ProfileList(people, IsTruncated: false);
    }

    public async Task<PersonMatches> SearchAsync(
        IReadOnlyList<string> terms, AccountId viewerId, int limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(terms);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        var query = context.Set<Person>()
            .AsNoTracking()
            .Where(person => person.DeletedAt == null
                && (person.Visibility == ProfileVisibility.Public || person.MaintainerId == viewerId));
        foreach (var term in terms)
        {
            var pattern = $"%{EscapeLike(term)}%";
            query = query.Where(person =>
                EF.Functions.ILike(person.Name.FirstName, pattern, LikeEscape)
                || EF.Functions.ILike(person.Name.MiddleName!, pattern, LikeEscape)
                || EF.Functions.ILike(person.Name.LastName!, pattern, LikeEscape));
        }

        // One extra row tells whether there are more matches.
        var people = await query
            .OrderBy(person => person.Name.FirstName)
            .ThenBy(person => person.Name.MiddleName)
            .ThenBy(person => person.Name.LastName)
            .ThenBy(person => person.Id)
            .Select(person => new PersonRecord(
                person.Id, person.Name, person.Visibility, false, person.MaintainerId == viewerId, person.ImageId, person.Gender, person.Description, person.BirthDate, person.BirthDateShown))
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        return people.Count > limit
            ? new PersonMatches(people[..limit], IsTruncated: true)
            : new PersonMatches(people, IsTruncated: false);
    }

    private const string LikeEscape = "\\";

    /// <summary>Searches for <c>%</c>, <c>_</c> and the escape character as themselves.</summary>
    private static string EscapeLike(string term) =>
        term.Replace(LikeEscape, LikeEscape + LikeEscape, StringComparison.Ordinal)
            .Replace("%", LikeEscape + "%", StringComparison.Ordinal)
            .Replace("_", LikeEscape + "_", StringComparison.Ordinal);
}
