using FamilyGraph.Application.Accounts;
using FamilyGraph.Application.Graph;
using FamilyGraph.Application.Handovers;
using FamilyGraph.Application.People;
using FamilyGraph.Application.Persistence;
using FamilyGraph.Application.Relationships;
using FamilyGraph.Infrastructure.Handovers;
using FamilyGraph.Infrastructure.Identity;
using FamilyGraph.Infrastructure.People;
using FamilyGraph.Infrastructure.Persistence;
using FamilyGraph.Infrastructure.Relationships;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FamilyGraph.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>The configuration section that holds the database connection settings.</summary>
    public const string DatabaseSection = "Database";

    /// <summary>
    /// Registers persistence with the connection built from the <c>Database</c> section, whose keys
    /// are Npgsql connection string keywords such as <c>Host</c>, <c>Port</c>, <c>Database</c>,
    /// <c>Username</c> and <c>Password</c>.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration) =>
        services.AddInfrastructure(ConnectionStringFrom(configuration.GetSection(DatabaseSection)));

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
                "The database is not configured. Set at least Database__Host.")));
        services.AddIdentityCore<UserAccount>(options => options.User.RequireUniqueEmail = false)
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<FamilyGraphDbContext>();
        services.AddScoped<IAccountStore, IdentityAccountStore>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IAuditLog, EfAuditLog>();
        services.AddScoped<IPersonStore, EfPersonStore>();
        services.AddScoped<IPersonQueries, EfPersonQueries>();
        services.AddScoped<IProfileImageStore, EfProfileImageStore>();
        services.AddScoped<IParentChildLinkStore, EfParentChildLinkStore>();
        services.AddScoped<IRelationshipQueries, EfRelationshipQueries>();
        services.AddScoped<IGraphQueries, EfGraphQueries>();
        services.AddScoped<IHandoverStore, EfHandoverStore>();
        return services;
    }

    /// <summary>
    /// The connection string for the section's settings, or null when no host is set. An unknown key
    /// throws, so a misspelt setting fails at startup instead of being ignored.
    /// </summary>
    internal static string? ConnectionStringFrom(IConfigurationSection section)
    {
        var builder = new NpgsqlConnectionStringBuilder();
        foreach (var setting in section.GetChildren().Where(setting => setting.Value is not null))
        {
            builder[setting.Key] = setting.Value;
        }

        return string.IsNullOrEmpty(builder.Host) ? null : builder.ConnectionString;
    }

    internal static DbContextOptionsBuilder UseFamilyGraphNpgsql(this DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString);
}
