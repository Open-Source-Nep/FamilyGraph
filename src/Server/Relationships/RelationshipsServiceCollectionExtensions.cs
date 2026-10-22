using FamilyGraph.Application.Relationships;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Server.Relationships;

public static class RelationshipsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the relationship use cases. <c>Relationships:DirectRelativesLimit</c> caps each of
    /// a person's parent and child lists, and <c>Relationships:InactiveLinksLimit</c> the list of
    /// inactive links (D-010).
    /// </summary>
    public static IServiceCollection AddRelationships(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(new RelationshipLimits(
            Limit(configuration, "Relationships:DirectRelativesLimit", RelationshipLimits.DefaultDirectRelativesLimit),
            Limit(configuration, "Relationships:InactiveLinksLimit", RelationshipLimits.DefaultInactiveLinksLimit)));
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<RelationshipService>();
        return services;
    }

    private static int Limit(IConfiguration configuration, string key, int defaultValue)
    {
        var limit = configuration.GetValue(key, defaultValue);
        return limit >= 1 ? limit : throw new InvalidOperationException($"{key} must be 1 or greater.");
    }
}
