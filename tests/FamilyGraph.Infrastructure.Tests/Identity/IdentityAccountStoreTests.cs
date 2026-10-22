using FamilyGraph.Application.Accounts;
using FamilyGraph.Business.People;
using FamilyGraph.Infrastructure.Persistence;
using FamilyGraph.Infrastructure.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyGraph.Infrastructure.Tests.Identity;

public class IdentityAccountStoreTests(PostgresFixture postgres)
{
    private static readonly ExternalLogin MayaAtGoogle = new("Google", "google-1", "maya@example.org", EmailVerified: true);

    [Fact]
    [Trait("AC", "AC-01.12")]
    public async Task Created_account_is_found_by_its_login_and_creates_no_person()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var services = await NewServicesAsync();

        AccountView created;
        await using (var scope = services.CreateAsyncScope())
        {
            created = await scope.ServiceProvider.GetRequiredService<IAccountStore>()
                .CreateAsync(MayaAtGoogle, isAdministrator: false, cancellationToken);
        }

        await using (var scope = services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IAccountStore>();
            var found = await store.FindByLoginAsync("Google", "google-1", cancellationToken);

            Assert.Equal(created, found);
            Assert.Equal(created, await store.FindAsync(created.Id, cancellationToken));
            Assert.False(await scope.ServiceProvider.GetRequiredService<FamilyGraphDbContext>()
                .Set<Person>().AnyAsync(cancellationToken));
        }
    }

    [Fact]
    public async Task Unknown_login_and_unknown_account_are_not_found()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var services = await NewServicesAsync();
        await using var scope = services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IAccountStore>();

        Assert.Null(await store.FindByLoginAsync("Google", "nobody", cancellationToken));
        Assert.Null(await store.FindAsync(new AccountId(Guid.NewGuid()), cancellationToken));
    }

    [Fact]
    public async Task Update_changes_email_and_grants_then_removes_the_administrator_role()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var services = await NewServicesAsync();

        AccountId id;
        await using (var scope = services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IAccountStore>();
            id = (await store.CreateAsync(MayaAtGoogle, isAdministrator: false, cancellationToken)).Id;
            await store.UpdateAsync(id, "maya@new.example.org", emailVerified: true, isAdministrator: true, cancellationToken);
        }

        await using (var scope = services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IAccountStore>();
            var promoted = await store.FindAsync(id, cancellationToken);

            Assert.Equal("maya@new.example.org", promoted!.Email);
            Assert.True(promoted.IsAdministrator);

            await store.UpdateAsync(id, "maya@new.example.org", emailVerified: true, isAdministrator: false, cancellationToken);
        }

        await using (var scope = services.CreateAsyncScope())
        {
            var demoted = await scope.ServiceProvider.GetRequiredService<IAccountStore>().FindAsync(id, cancellationToken);

            Assert.False(demoted!.IsAdministrator);
        }
    }

    [Fact]
    public async Task Account_created_as_administrator_has_both_roles()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var services = await NewServicesAsync();
        await using var scope = services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IAccountStore>();

        var created = await store.CreateAsync(MayaAtGoogle, isAdministrator: true, cancellationToken);

        Assert.True((await store.FindAsync(created.Id, cancellationToken))!.IsAdministrator);
    }

    private async Task<ServiceProvider> NewServicesAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(await postgres.NewMigratedConnectionStringAsync());
        return services.BuildServiceProvider();
    }
}
