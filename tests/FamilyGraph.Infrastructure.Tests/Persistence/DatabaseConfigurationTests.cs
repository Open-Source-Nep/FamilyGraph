using Microsoft.Extensions.Configuration;
using Npgsql;

namespace FamilyGraph.Infrastructure.Tests.Persistence;

public class DatabaseConfigurationTests
{
    [Fact]
    public void Connection_string_is_built_from_the_database_section()
    {
        var connectionString = InfrastructureServiceCollectionExtensions.ConnectionStringFrom(Section(new()
        {
            ["Database:Host"] = "db",
            ["Database:Port"] = "5433",
            ["Database:Database"] = "familygraph",
            ["Database:Username"] = "familygraph",
            ["Database:Password"] = "secret",
        }));

        var built = new NpgsqlConnectionStringBuilder(connectionString);
        Assert.Equal("db", built.Host);
        Assert.Equal(5433, built.Port);
        Assert.Equal("familygraph", built.Database);
        Assert.Equal("familygraph", built.Username);
        Assert.Equal("secret", built.Password);
    }

    [Fact]
    public void Section_without_a_host_gives_no_connection_string()
    {
        Assert.Null(InfrastructureServiceCollectionExtensions.ConnectionStringFrom(Section(new()
        {
            ["Database:Database"] = "familygraph",
        })));
    }

    [Fact]
    public void Unknown_setting_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => InfrastructureServiceCollectionExtensions.ConnectionStringFrom(Section(new()
        {
            ["Database:Host"] = "db",
            ["Database:Hots"] = "db",
        })));
    }

    private static IConfigurationSection Section(Dictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build()
            .GetSection(InfrastructureServiceCollectionExtensions.DatabaseSection);
}
