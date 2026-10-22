using FamilyGraph.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyGraph.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers persistence against PostgreSQL. A missing connection string fails when the database
    /// is first used, not at startup, so the host can still start without one.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string? connectionString)
    {
        services.AddDbContext<FamilyGraphDbContext>(options => options.UseFamilyGraphNpgsql(
            connectionString ?? throw new InvalidOperationException(
                "The connection string 'FamilyGraph' is not configured. Set ConnectionStrings__FamilyGraph.")));
        return services;
    }

    internal static DbContextOptionsBuilder UseFamilyGraphNpgsql(this DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString);
}
