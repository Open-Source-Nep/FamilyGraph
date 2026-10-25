using FamilyGraph.Application.Graph;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.Graph;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;
using FamilyGraph.Infrastructure.Persistence;
using FamilyGraph.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FamilyGraph.Infrastructure.Relationships;

/// <summary>
/// Bounded graph reads as hand-written recursive CTEs (D-009, D-023). Each walk follows active links
/// up to parents and down to children, and active marriages across to spouses in the same
/// generation (D-031), through the active indexes, and every query runs under the configured timeout.
/// </summary>
internal sealed class EfGraphQueries(FamilyGraphDbContext context, GraphLimits limits) : IGraphQueries
{
    private static readonly string Active = ParentChildLinkConfiguration.ActiveFilter.Replace(
        "deleted_at", "link.deleted_at", StringComparison.Ordinal)
        .Replace("parent_approval", "link.parent_approval", StringComparison.Ordinal)
        .Replace("child_approval", "link.child_approval", StringComparison.Ordinal);

    private static readonly string ActiveMarriage = MarriageConfiguration.ActiveFilter.Replace(
        "deleted_at", "marriage.deleted_at", StringComparison.Ordinal)
        .Replace("first_approval", "marriage.first_approval", StringComparison.Ordinal)
        .Replace("second_approval", "marriage.second_approval", StringComparison.Ordinal);

    /// <summary>Parameters: <c>@centre</c>, <c>@max_distance</c>, <c>@generations</c>, <c>@row_limit</c>.</summary>
    private static readonly string LocalViewSql = $"""
        WITH RECURSIVE walk (person_id, distance, generation) AS (
            SELECT @centre, 0, 0
            UNION
            SELECT step.person_id, walk.distance + 1, walk.generation + step.offset_
            FROM walk
            CROSS JOIN LATERAL (
                SELECT link.parent_id AS person_id, 1 AS offset_
                FROM parent_child_links AS link
                WHERE link.child_id = walk.person_id AND {Active}
                UNION ALL
                SELECT link.child_id, -1
                FROM parent_child_links AS link
                WHERE link.parent_id = walk.person_id AND {Active}
                UNION ALL
                SELECT marriage.second_spouse_id, 0
                FROM marriages AS marriage
                WHERE marriage.first_spouse_id = walk.person_id AND {ActiveMarriage}
                UNION ALL
                SELECT marriage.first_spouse_id, 0
                FROM marriages AS marriage
                WHERE marriage.second_spouse_id = walk.person_id AND {ActiveMarriage}
            ) AS step
            WHERE walk.distance < @max_distance AND abs(walk.generation + step.offset_) <= @generations
        ),
        nearest AS (
            SELECT DISTINCT ON (person_id) person_id, distance, generation
            FROM walk
            ORDER BY person_id, distance, abs(generation), generation DESC
        )
        SELECT person_id AS "PersonId", generation AS "Generation"
        FROM nearest
        ORDER BY distance, abs(generation), generation DESC, person_id
        LIMIT @row_limit
        """;

    /// <summary>Parameter: <c>@ids</c>, the people in the view.</summary>
    private static readonly string HasMoreSql = $"""
        SELECT shown.id AS "Value"
        FROM unnest(@ids) AS shown (id)
        WHERE EXISTS (
                SELECT 1 FROM parent_child_links AS link
                WHERE link.parent_id = shown.id AND link.child_id <> ALL (@ids) AND {Active})
            OR EXISTS (
                SELECT 1 FROM parent_child_links AS link
                WHERE link.child_id = shown.id AND link.parent_id <> ALL (@ids) AND {Active})
            OR EXISTS (
                SELECT 1 FROM marriages AS marriage
                WHERE marriage.first_spouse_id = shown.id AND marriage.second_spouse_id <> ALL (@ids) AND {ActiveMarriage})
            OR EXISTS (
                SELECT 1 FROM marriages AS marriage
                WHERE marriage.second_spouse_id = shown.id AND marriage.first_spouse_id <> ALL (@ids) AND {ActiveMarriage})
        """;

    /// <summary>Parameters: <c>@starts</c>, the people to start from, and <c>@row_limit</c>.</summary>
    private static readonly string ReachedSql = $"""
        WITH RECURSIVE reached (person_id) AS (
            SELECT origin.id FROM unnest(@starts) AS origin (id)
            UNION
            SELECT step.person_id
            FROM reached
            CROSS JOIN LATERAL (
                SELECT link.parent_id AS person_id
                FROM parent_child_links AS link
                WHERE link.child_id = reached.person_id AND {Active}
                UNION ALL
                SELECT link.child_id
                FROM parent_child_links AS link
                WHERE link.parent_id = reached.person_id AND {Active}
                UNION ALL
                SELECT marriage.second_spouse_id
                FROM marriages AS marriage
                WHERE marriage.first_spouse_id = reached.person_id AND {ActiveMarriage}
                UNION ALL
                SELECT marriage.first_spouse_id
                FROM marriages AS marriage
                WHERE marriage.second_spouse_id = reached.person_id AND {ActiveMarriage}
            ) AS step
        )
        SELECT person_id AS "Value" FROM reached LIMIT @row_limit
        """;

    /// <summary>Parameter: <c>@ids</c>.</summary>
    private static readonly string LinksBetweenSql = $"""
        SELECT link.id AS "Id", link.parent_id AS "ParentId", link.child_id AS "ChildId", link.type AS "Type"
        FROM parent_child_links AS link
        JOIN unnest(@ids) AS parent (id) ON parent.id = link.parent_id
        JOIN unnest(@ids) AS child (id) ON child.id = link.child_id
        WHERE {Active}
        ORDER BY link.id
        """;

    /// <summary>Parameter: <c>@ids</c>.</summary>
    private static readonly string MarriagesBetweenSql = $"""
        SELECT marriage.id AS "Id", marriage.first_spouse_id AS "FirstSpouseId",
            marriage.second_spouse_id AS "SecondSpouseId", marriage.is_former AS "IsFormer"
        FROM marriages AS marriage
        JOIN unnest(@ids) AS first (id) ON first.id = marriage.first_spouse_id
        JOIN unnest(@ids) AS second (id) ON second.id = marriage.second_spouse_id
        WHERE {ActiveMarriage}
        ORDER BY marriage.id
        """;

    /// <summary>
    /// The walk keeps one row per (person, distance, generation), so a person reached by many
    /// routes, or around a cycle, is bounded by the distance limit of twice the generations. The
    /// outer query keeps each person's nearest row, takes the centre and the nearest people
    /// (closest generation first, then ancestors before descendants), and one more to tell that
    /// the view was cut short.
    /// </summary>
    public async Task<LocalViewData> LocalViewAsync(
        PersonId centreId, AccountId viewerId, int generations, int peopleLimit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(generations);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(peopleLimit);

        var maxDistance = 2 * generations;
        var rows = await RunAsync(token => context.Database.SqlQueryRaw<ViewRow>(LocalViewSql,
            new NpgsqlParameter<Guid>("centre", centreId.Value),
            new NpgsqlParameter<int>("max_distance", maxDistance),
            new NpgsqlParameter<int>("generations", generations),
            new NpgsqlParameter<int>("row_limit", peopleLimit + 2)).ToListAsync(token), cancellationToken);

        var shown = rows.Take(peopleLimit + 1).ToList();
        var ids = shown.Select(row => row.PersonId).ToArray();
        var links = await LinksBetweenAsync(ids, cancellationToken);
        var withMore = (await RunAsync(token => context.Database.SqlQueryRaw<Guid>(HasMoreSql,
            new NpgsqlParameter<Guid[]>("ids", ids)).ToListAsync(token), cancellationToken)).ToHashSet();
        var people = (await PeopleAsync([.. ids.Select(id => new PersonId(id))], viewerId, cancellationToken))
            .ToDictionary(person => person.Id.Value);

        return new LocalViewData(
            [.. shown.Select(row => new LocalViewPersonData(people[row.PersonId], row.Generation, withMore.Contains(row.PersonId)))],
            links,
            await MarriagesBetweenAsync(ids, cancellationToken),
            rows.Count > shown.Count);
    }

    /// <summary>
    /// The walk keeps one row per person, so it visits each person once, breadth first, and
    /// PostgreSQL stops it once the outer limit has its rows: the people examined are the
    /// nearest ones. One person past the limit tells that the search was cut short.
    /// </summary>
    public async Task<LinkNeighbourhood> LinksAroundAsync(
        PersonId personId, int peopleLimit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(peopleLimit);

        var reached = await ReachedAsync([personId.Value], peopleLimit + 1, cancellationToken);

        var examined = reached.Take(peopleLimit).ToArray();
        return new LinkNeighbourhood(
            await LinksBetweenAsync(examined, cancellationToken),
            await MarriagesBetweenAsync(examined, cancellationToken),
            reached.Count == examined.Length);
    }

    /// <summary>
    /// The same breadth-first walk as <see cref="LinksAroundAsync"/>, from all the starting people
    /// at once, so the people returned are the nearest to any of them. One person past the limit
    /// tells that more were in reach.
    /// </summary>
    public async Task<OverviewData> OverviewAsync(
        IReadOnlyCollection<PersonId> startIds, AccountId viewerId, int peopleLimit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(peopleLimit);

        var reached = await ReachedAsync([.. startIds.Select(id => id.Value)], peopleLimit + 1, cancellationToken);
        var shown = reached.Take(peopleLimit).ToArray();
        var people = (await PeopleAsync([.. shown.Select(id => new PersonId(id))], viewerId, cancellationToken))
            .ToDictionary(person => person.Id.Value);

        return new OverviewData(
            [.. shown.Where(people.ContainsKey).Select(id => people[id])],
            await LinksBetweenAsync(shown, cancellationToken),
            await MarriagesBetweenAsync(shown, cancellationToken),
            reached.Count > shown.Length);
    }

    public async Task<IReadOnlyList<PersonRecord>> PeopleAsync(
        IReadOnlyCollection<PersonId> ids, AccountId viewerId, CancellationToken cancellationToken)
    {
        var values = ids.Select(id => id.Value).ToArray();
        var rows = await RunAsync(token => context.Database.SqlQuery<PersonRow>($"""
            SELECT person.id AS "Id", person.first_name AS "FirstName", person.middle_name AS "MiddleName",
                person.last_name AS "LastName", person.visibility AS "Visibility",
                person.deleted_at IS NOT NULL AS "IsDeleted",
                person.maintainer_id = {viewerId.Value} AS "MaintainedByViewer", person.image_id AS "ImageId",
                person.gender AS "Gender", person.description AS "Description",
                person.birth_calendar AS "BirthCalendar", person.birth_year AS "BirthYear",
                person.birth_month AS "BirthMonth", person.birth_day AS "BirthDay",
                person.birth_sort_date AS "BirthSortDate", person.birth_date_shown AS "BirthDateShown"
            FROM people AS person
            JOIN unnest({values}) AS wanted (id) ON wanted.id = person.id
            """).ToListAsync(token), cancellationToken);

        return [.. rows.Select(row => new PersonRecord(
            new PersonId(row.Id),
            new PersonName(row.FirstName, row.MiddleName, row.LastName),
            ProfileVisibility.FromValue(row.Visibility),
            row.IsDeleted,
            row.MaintainedByViewer,
            row.ImageId is { } imageId ? new ProfileImageId(imageId) : null,
            row.Gender is { } gender ? Gender.FromValue(gender) : null,
            row.Description,
            BirthDateOf(row),
            row.BirthDateShown))];
    }

    /// <summary>A stored birth date keeps the sort date it was saved with (D-037).</summary>
    private static BirthDate? BirthDateOf(PersonRow row) =>
        (row.BirthCalendar, row.BirthYear, row.BirthSortDate) is ({ } calendar, { } year, { } sortDate)
            ? BirthCalendar.FromValue(calendar) == BirthCalendar.Ad
                ? BirthDate.Ad(year, row.BirthMonth, row.BirthDay)
                : BirthDate.Bs(year, row.BirthMonth, row.BirthDay, sortDate)
            : null;

    private Task<List<Guid>> ReachedAsync(Guid[] starts, int rowLimit, CancellationToken cancellationToken) =>
        RunAsync(token => context.Database.SqlQueryRaw<Guid>(ReachedSql,
            new NpgsqlParameter<Guid[]>("starts", starts),
            new NpgsqlParameter<int>("row_limit", rowLimit)).ToListAsync(token), cancellationToken);

    private async Task<IReadOnlyList<GraphLink>> LinksBetweenAsync(Guid[] ids, CancellationToken cancellationToken)
    {
        var rows = await RunAsync(token => context.Database.SqlQueryRaw<LinkRow>(LinksBetweenSql,
            new NpgsqlParameter<Guid[]>("ids", ids)).ToListAsync(token), cancellationToken);

        return [.. rows.Select(row => new GraphLink(
            new LinkId(row.Id), new PersonId(row.ParentId), new PersonId(row.ChildId), ParentChildType.FromValue(row.Type)))];
    }

    private async Task<IReadOnlyList<GraphMarriage>> MarriagesBetweenAsync(Guid[] ids, CancellationToken cancellationToken)
    {
        var rows = await RunAsync(token => context.Database.SqlQueryRaw<MarriageRow>(MarriagesBetweenSql,
            new NpgsqlParameter<Guid[]>("ids", ids)).ToListAsync(token), cancellationToken);

        return [.. rows.Select(row => new GraphMarriage(
            new LinkId(row.Id), new PersonId(row.FirstSpouseId), new PersonId(row.SecondSpouseId), row.IsFormer))];
    }

    /// <summary>
    /// Runs one query under the graph timeout (D-009). The timeout cancels the query, which also
    /// stops it on the server: a command timeout alone would not, because rows of a walk stream in
    /// one by one.
    /// </summary>
    private async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> query, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(limits.QueryTimeoutSeconds));
        try
        {
            return await query(timeout.Token);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new GraphQueryTimeoutException(exception);
        }
    }

    private sealed record ViewRow(Guid PersonId, int Generation);

    private sealed record LinkRow(Guid Id, Guid ParentId, Guid ChildId, int Type);

    private sealed record MarriageRow(Guid Id, Guid FirstSpouseId, Guid SecondSpouseId, bool IsFormer);

    private sealed record PersonRow(
        Guid Id,
        string FirstName,
        string? MiddleName,
        string? LastName,
        int Visibility,
        bool IsDeleted,
        bool MaintainedByViewer,
        Guid? ImageId,
        int? Gender,
        string? Description,
        int? BirthCalendar,
        int? BirthYear,
        int? BirthMonth,
        int? BirthDay,
        DateOnly? BirthSortDate,
        bool BirthDateShown);
}
