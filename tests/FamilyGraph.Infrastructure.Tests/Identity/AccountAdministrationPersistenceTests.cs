using FamilyGraph.Application.Accounts;
using FamilyGraph.Application.People;
using FamilyGraph.Application.Persistence;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.People;
using FamilyGraph.Infrastructure.Persistence;
using FamilyGraph.Infrastructure.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyGraph.Infrastructure.Tests.Identity;

/// <summary>The account list and profile transfers against PostgreSQL (D-035, #94).</summary>
public class AccountAdministrationPersistenceTests(PostgresFixture postgres)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("AC", "AC-01.23")]
    public async Task Accounts_are_listed_by_email_with_the_administrator_flag_and_live_profile_count()
    {
        await using var services = await NewServicesAsync();
        var (bob, admin, alice) = (await CreateAsync(services, "bob@example.org"), await CreateAsync(services, "admin@example.org", true), await CreateAsync(services, "alice@example.org"));
        await AddPeopleAsync(services, alice.Id, "Maya", "Hari", "Gone");
        await DeleteAsync(services, alice.Id, "Gone");

        await using var scope = services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IAccountQueries>();
        var all = await queries.ListAsync(10, Cancellation);
        var first = await queries.ListAsync(2, Cancellation);

        Assert.Equal(
            [
                new AccountSummary(admin.Id, "admin@example.org", true, true, 0),
                new AccountSummary(alice.Id, "alice@example.org", true, false, 2),
                new AccountSummary(bob.Id, "bob@example.org", true, false, 0),
            ],
            all.Accounts);
        Assert.False(all.IsTruncated);
        Assert.Equal((2, true), (first.Accounts.Count, first.IsTruncated));
    }

    [Fact]
    [Trait("AC", "AC-01.25")]
    public async Task Transfer_all_moves_every_non_deleted_profile_with_an_audit_row_each()
    {
        await using var services = await NewServicesAsync();
        var admin = await CreateAsync(services, "admin@example.org", true);
        var alice = await CreateAsync(services, "alice@example.org");
        var bob = await CreateAsync(services, "bob@example.org");
        await AddPeopleAsync(services, alice.Id, "Maya", "Hari", "Gone");
        await DeleteAsync(services, alice.Id, "Gone");

        await using (var scope = services.CreateAsyncScope())
        {
            var count = await scope.ServiceProvider.GetRequiredService<AccountAdministrationService>()
                .TransferAllAsync(new Viewer(admin.Id, IsAdministrator: true), alice.Id, bob.Id, Cancellation);
            Assert.Equal(2, count);
        }

        await using (var scope = services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<FamilyGraphDbContext>();
            var maintainers = await context.Set<Person>().AsNoTracking()
                .ToDictionaryAsync(person => person.Name.FirstName, person => person.MaintainerId, Cancellation);
            Assert.Equal((bob.Id, bob.Id), (maintainers["Maya"], maintainers["Hari"]));
            Assert.Equal(alice.Id, maintainers[PersonName.Erased.FirstName]);
            Assert.Equal(2, await context.Set<AuditEventRecord>().CountAsync(r => r.Type == nameof(PersonTransferred), Cancellation));
        }
    }

    private static async Task<AccountView> CreateAsync(ServiceProvider services, string email, bool isAdministrator = false)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAccountStore>()
            .CreateAsync(new ExternalLogin("Google", Guid.NewGuid().ToString(), email, EmailVerified: true), isAdministrator, Cancellation);
    }

    private static async Task AddPeopleAsync(ServiceProvider services, AccountId maintainer, params string[] firstNames)
    {
        await using var scope = services.CreateAsyncScope();
        foreach (var firstName in firstNames)
        {
            var person = TestData.NewPerson(maintainer, firstName);
            person.TakeEvents();
            scope.ServiceProvider.GetRequiredService<IPersonStore>().Add(person);
        }

        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Cancellation);
    }

    private static async Task DeleteAsync(ServiceProvider services, AccountId maintainer, string firstName)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<FamilyGraphDbContext>();
        var person = await context.Set<Person>().SingleAsync(p => p.Name.FirstName == firstName && p.MaintainerId == maintainer, Cancellation);
        person.Delete(maintainer, actorIsAdministrator: false, DateTimeOffset.UtcNow);
        person.TakeEvents();
        await context.SaveChangesAsync(Cancellation);
    }

    private async Task<ServiceProvider> NewServicesAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(await postgres.NewMigratedConnectionStringAsync());
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(new PeopleLimits());
        services.AddSingleton(new AccountLimits());
        services.AddScoped<AccountAdministrationService>();
        return services.BuildServiceProvider();
    }
}
