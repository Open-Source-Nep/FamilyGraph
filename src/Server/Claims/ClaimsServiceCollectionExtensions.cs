using FamilyGraph.Application.Claims;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Server.Claims;

public static class ClaimsServiceCollectionExtensions
{
    /// <summary>
    /// Registers identity claims. <c>Claims:ListLimit</c> (1–1000, default 100) caps each claim list;
    /// a value out of range fails at startup.
    /// </summary>
    public static IServiceCollection AddClaims(this IServiceCollection services, IConfiguration configuration)
    {
        var limit = configuration.GetValue("Claims:ListLimit", ClaimLimits.DefaultListLimit);
        if (limit is < 1 or > 1000)
        {
            throw new InvalidOperationException("Claims:ListLimit must be between 1 and 1000.");
        }

        services.AddSingleton(new ClaimLimits(limit));
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ClaimService>();
        return services;
    }
}
