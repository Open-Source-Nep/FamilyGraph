using FamilyGraph.Application.Accounts;
using FamilyGraph.Infrastructure.Identity;
using FamilyGraph.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyGraph.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers persistence against PostgreSQL and the ASP.NET Core Identity account store. A
    /// missing connection string fails when the database is first used, not at startup, so the host
    /// can still start without one.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string? connectionString)
    {
        services.AddDbContext<FamilyGraphDbContext>(options => options.UseFamilyGraphNpgsql(
            connectionString ?? throw new InvalidOperationException(
                "The connection string 'FamilyGraph' is not configured. Set ConnectionStrings__FamilyGraph.")));
        services.AddIdentityCore<UserAccount>(options => options.User.RequireUniqueEmail = false)
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<FamilyGraphDbContext>();
        services.AddScoped<IAccountStore, IdentityAccountStore>();
        return services;
    }

    internal static DbContextOptionsBuilder UseFamilyGraphNpgsql(this DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString);
}
