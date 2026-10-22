using FamilyGraph.Application.Relationships;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Server.Relationships;

public static class RelationshipsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the relationship use cases. <c>Relationships:DirectRelativesLimit</c> caps each of
    /// a person's parent and child lists (D-010).
    /// </summary>
    public static IServiceCollection AddRelationships(this IServiceCollection services, IConfiguration configuration)
    {
        var limit = configuration.GetValue(
            "Relationships:DirectRelativesLimit", RelationshipLimits.DefaultDirectRelativesLimit);
        if (limit < 1)
        {
            throw new InvalidOperationException("Relationships:DirectRelativesLimit must be 1 or greater.");
        }

        services.AddSingleton(new RelationshipLimits(limit));
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<RelationshipService>();
        return services;
    }
}
