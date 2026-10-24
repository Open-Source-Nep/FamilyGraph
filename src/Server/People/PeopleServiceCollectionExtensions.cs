using FamilyGraph.Application.People;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Server.People;

public static class PeopleServiceCollectionExtensions
{
    /// <summary>
    /// Registers the person profile use cases. <c>People:MaintainedListLimit</c> caps "profiles I
    /// maintain" and <c>People:SearchLimit</c> (1 to 100) the matches of a person search (D-010, D-032).
    /// </summary>
    public static IServiceCollection AddPeople(this IServiceCollection services, IConfiguration configuration)
    {
        var limit = configuration.GetValue("People:MaintainedListLimit", PeopleLimits.DefaultMaintainedListLimit);
        if (limit < 1)
        {
            throw new InvalidOperationException("People:MaintainedListLimit must be 1 or greater.");
        }

        var searchLimit = configuration.GetValue("People:SearchLimit", PeopleLimits.DefaultSearchLimit);
        if (searchLimit is < 1 or > 100)
        {
            throw new InvalidOperationException("People:SearchLimit must be between 1 and 100.");
        }

        services.AddSingleton(new PeopleLimits(limit, searchLimit));
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<PersonService>();
        return services;
    }
}
