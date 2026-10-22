using FamilyGraph.Application.People;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Server.People;

public static class PeopleServiceCollectionExtensions
{
    /// <summary>Registers the person profile use cases. <c>People:MaintainedListLimit</c> caps "profiles I maintain" (D-010).</summary>
    public static IServiceCollection AddPeople(this IServiceCollection services, IConfiguration configuration)
    {
        var limit = configuration.GetValue("People:MaintainedListLimit", PeopleLimits.DefaultMaintainedListLimit);
        if (limit < 1)
        {
            throw new InvalidOperationException("People:MaintainedListLimit must be 1 or greater.");
        }

        services.AddSingleton(new PeopleLimits(limit));
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<PersonService>();
        return services;
    }
}
