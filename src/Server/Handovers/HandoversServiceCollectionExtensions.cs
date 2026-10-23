using FamilyGraph.Application.Handovers;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Server.Handovers;

public static class HandoversServiceCollectionExtensions
{
    /// <summary>
    /// Registers profile handover. <c>Handovers:LinkLifetimeDays</c> (1–90, default 14) is how long
    /// a handover link stays valid; a value out of range fails at startup.
    /// </summary>
    public static IServiceCollection AddHandovers(this IServiceCollection services, IConfiguration configuration)
    {
        var days = configuration.GetValue("Handovers:LinkLifetimeDays", HandoverSettings.DefaultLinkLifetimeDays);
        if (days is < 1 or > HandoverSettings.MaxLinkLifetimeDays)
        {
            throw new InvalidOperationException(
                $"Handovers:LinkLifetimeDays must be between 1 and {HandoverSettings.MaxLinkLifetimeDays}.");
        }

        services.AddSingleton(new HandoverSettings(days));
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<HandoverService>();
        return services;
    }
}
