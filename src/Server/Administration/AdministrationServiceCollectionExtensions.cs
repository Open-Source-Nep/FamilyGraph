using FamilyGraph.Application.Accounts;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Server.Administration;

public static class AdministrationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the administrator's Users page and profile transfers (D-035).
    /// <c>Accounts:ListLimit</c> (1 or greater, default 1000) caps the accounts listed.
    /// </summary>
    public static IServiceCollection AddAdministration(this IServiceCollection services, IConfiguration configuration)
    {
        var limit = configuration.GetValue("Accounts:ListLimit", AccountLimits.DefaultListLimit);
        if (limit < 1)
        {
            throw new InvalidOperationException("Accounts:ListLimit must be 1 or greater.");
        }

        services.AddSingleton(new AccountLimits(limit));
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<AccountAdministrationService>();
        return services;
    }
}
