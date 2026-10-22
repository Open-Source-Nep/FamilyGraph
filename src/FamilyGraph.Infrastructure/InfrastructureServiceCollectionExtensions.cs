using FamilyGraph.Application.Accounts;
using FamilyGraph.Application.People;
using FamilyGraph.Application.Persistence;
using FamilyGraph.Application.Relationships;
using FamilyGraph.Infrastructure.Identity;
using FamilyGraph.Infrastructure.People;
using FamilyGraph.Infrastructure.Persistence;
using FamilyGraph.Infrastructure.Relationships;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyGraph.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers persistence against PostgreSQL, the stores, queries and unit of work that
    /// Application owns, and the ASP.NET Core Identity account store. A
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
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IAuditLog, EfAuditLog>();
        services.AddScoped<IPersonStore, EfPersonStore>();
        services.AddScoped<IPersonQueries, EfPersonQueries>();
        services.AddScoped<IParentChildLinkStore, EfParentChildLinkStore>();
        services.AddScoped<IRelationshipQueries, EfRelationshipQueries>();
        return services;
    }

    internal static DbContextOptionsBuilder UseFamilyGraphNpgsql(this DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString);
}
