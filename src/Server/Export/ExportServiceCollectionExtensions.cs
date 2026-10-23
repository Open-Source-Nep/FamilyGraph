using FamilyGraph.Application.Export;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Server.Export;

public static class ExportServiceCollectionExtensions
{
    /// <summary>Registers the administrator's data export (D-029).</summary>
    public static IServiceCollection AddExport(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ExportService>();
        return services;
    }
}
