using FamilyGraph.Application.Graph;

namespace Server.Graph;

public static class GraphServiceCollectionExtensions
{
    /// <summary>
    /// Registers graph exploration with its limits (D-009, D-023): <c>Graph:ViewGenerations</c>
    /// (1–4), <c>Graph:ViewPeople</c> (1–500), <c>Graph:PathSearchPeople</c>, <c>Graph:MorePaths</c>
    /// and <c>Graph:QueryTimeoutSeconds</c>. A value out of range fails at startup.
    /// </summary>
    public static IServiceCollection AddGraph(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(new GraphLimits(
            configuration.GetValue("Graph:ViewGenerations", GraphLimits.DefaultViewGenerations),
            configuration.GetValue("Graph:ViewPeople", GraphLimits.DefaultViewPeople),
            configuration.GetValue("Graph:PathSearchPeople", GraphLimits.DefaultPathSearchPeople),
            configuration.GetValue("Graph:MorePaths", GraphLimits.DefaultMorePaths),
            configuration.GetValue("Graph:QueryTimeoutSeconds", GraphLimits.DefaultQueryTimeoutSeconds)));
        services.AddScoped<GraphService>();
        return services;
    }
}
