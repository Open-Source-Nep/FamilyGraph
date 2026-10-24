using FamilyGraph.Application.Import;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Server.Import;

public static class ImportServiceCollectionExtensions
{
    /// <summary>Registers the administrator's data import (D-033).</summary>
    public static IServiceCollection AddImport(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ImportService>();
        return services;
    }
}
