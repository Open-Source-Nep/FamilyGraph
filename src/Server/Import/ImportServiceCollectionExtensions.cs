using FamilyGraph.Application.Import;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Server.Import;

public static class ImportServiceCollectionExtensions
{
    /// <summary>Registers the administrator's data import (D-033, D-049).</summary>
    public static IServiceCollection AddImport(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ImportService>();
        var maxMiB = configuration.GetValue("Import:MaxFileSizeMiB", ImportSettings.DefaultMaxFileSizeMiB);
        if (maxMiB < 1)
        {
            throw new InvalidOperationException("Import:MaxFileSizeMiB must be at least 1.");
        }

        services.AddSingleton(new ImportSettings(maxMiB * 1024L * 1024L));
        return services;
    }
}
