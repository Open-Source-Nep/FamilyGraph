using FamilyGraph.Application.Claims;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Server.Claims;

public static class ClaimsServiceCollectionExtensions
{
    /// <summary>
    /// Registers identity claims and the job that erases old notes. <c>Claims:ListLimit</c> (1–1000,
    /// default 100) caps each claim list; <c>Claims:MaintainerResponseDays</c> (default 30),
    /// <c>Claims:NewClaimWaitDays</c> (default 90) and <c>Claims:EvidenceRetentionDays</c> (default 90)
    /// are 1–3650 (D-052). A value out of range fails at startup.
    /// </summary>
    public static IServiceCollection AddClaims(this IServiceCollection services, IConfiguration configuration)
    {
        var limit = configuration.GetValue("Claims:ListLimit", ClaimLimits.DefaultListLimit);
        if (limit is < 1 or > 1000)
        {
            throw new InvalidOperationException("Claims:ListLimit must be between 1 and 1000.");
        }

        services.AddSingleton(new ClaimLimits(
            limit,
            Days(configuration, "Claims:MaintainerResponseDays", ClaimLimits.DefaultMaintainerResponseDays),
            Days(configuration, "Claims:NewClaimWaitDays", ClaimLimits.DefaultNewClaimWaitDays),
            Days(configuration, "Claims:EvidenceRetentionDays", ClaimLimits.DefaultEvidenceRetentionDays)));
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ClaimService>();
        services.AddHostedService<ClaimNotesEraser>();
        return services;
    }

    private static int Days(IConfiguration configuration, string key, int defaultValue)
    {
        var days = configuration.GetValue(key, defaultValue);
        return days is < 1 or > 3650 ? throw new InvalidOperationException($"{key} must be between 1 and 3650.") : days;
    }
}
