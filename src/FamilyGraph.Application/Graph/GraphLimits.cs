namespace FamilyGraph.Application.Graph;

/// <summary>
/// Configurable limits for graph reads (<c>Graph:*</c>, D-009, D-010, D-023). The maximums are
/// enforced in code.
/// </summary>
public sealed record GraphLimits
{
    public const int DefaultViewGenerations = 2;

    public const int MaxViewGenerations = 4;

    public const int DefaultViewPeople = 10;

    public const int MaxViewPeople = 500;

    public const int DefaultOverviewPeople = 500;

    public const int MaxOverviewPeople = 500;

    public const int DefaultPathSearchPeople = 10_000;

    public const int DefaultMorePaths = 5;

    public const int DefaultQueryTimeoutSeconds = 10;

    public GraphLimits(
        int viewGenerations = DefaultViewGenerations,
        int viewPeople = DefaultViewPeople,
        int pathSearchPeople = DefaultPathSearchPeople,
        int morePaths = DefaultMorePaths,
        int queryTimeoutSeconds = DefaultQueryTimeoutSeconds,
        int overviewPeople = DefaultOverviewPeople)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(viewGenerations, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(viewGenerations, MaxViewGenerations);
        ArgumentOutOfRangeException.ThrowIfLessThan(viewPeople, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(viewPeople, MaxViewPeople);
        ArgumentOutOfRangeException.ThrowIfLessThan(pathSearchPeople, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(morePaths);
        ArgumentOutOfRangeException.ThrowIfLessThan(queryTimeoutSeconds, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(overviewPeople, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(overviewPeople, MaxOverviewPeople);

        ViewGenerations = viewGenerations;
        ViewPeople = viewPeople;
        PathSearchPeople = pathSearchPeople;
        MorePaths = morePaths;
        QueryTimeoutSeconds = queryTimeoutSeconds;
        OverviewPeople = overviewPeople;
    }

    /// <summary>How many generations above and below the centre a local view reaches.</summary>
    public int ViewGenerations { get; }

    /// <summary>How many people a local view returns besides its centre.</summary>
    public int ViewPeople { get; }

    /// <summary>How many people the overview of everyone connected to your profiles returns.</summary>
    public int OverviewPeople { get; }

    /// <summary>How many people a search for how two people are related examines.</summary>
    public int PathSearchPeople { get; }

    /// <summary>How many paths besides the shortest a search lists (PRD 08).</summary>
    public int MorePaths { get; }

    /// <summary>How long one graph query may run.</summary>
    public int QueryTimeoutSeconds { get; }
}
