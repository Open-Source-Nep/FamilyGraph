using FamilyGraph.Application.Handovers;
using FamilyGraph.Application.People;
using FamilyGraph.Business.Handovers;
using FamilyGraph.Business.People;
using FamilyGraph.Infrastructure.Persistence;
using FamilyGraph.Infrastructure.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyGraph.Infrastructure.Tests.Handovers;

/// <summary>The handover store and use cases against PostgreSQL.</summary>
public class HandoverPersistenceServicesTests(PostgresFixture postgres)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("AC", "AC-01.13")]
    [Trait("AC", "AC-01.14")]
    public async Task A_link_is_saved_found_by_its_token_and_accepted()
    {
        await using var services = await NewServicesAsync();
        var alice = TestData.NewAccount();
        var bob = TestData.NewAccount();
        var personId = await CreatePersonAsync(services, alice);

        var created = await InScopeAsync(services, handovers => handovers.OfferAsync(personId, alice, Cancellation));
        await InScopeAsync(services, handovers => handovers.AcceptAsync(created.Token, bob, Cancellation));

        await using var scope = services.CreateAsyncScope();
        var person = await scope.ServiceProvider.GetRequiredService<IPersonStore>().FindAsync(personId, Cancellation);
        var handover = await scope.ServiceProvider.GetRequiredService<IHandoverStore>()
            .FindByTokenHashAsync(HandoverToken.HashOf(created.Token)!, Cancellation);
        var types = await scope.ServiceProvider.GetRequiredService<FamilyGraphDbContext>()
            .Set<AuditEventRecord>().Where(record => record.SubjectId == personId.Value)
            .OrderBy(record => record.Id).Select(record => record.Type).ToListAsync(Cancellation);

        Assert.True(person!.IsMaintainedBy(bob));
        Assert.Equal((HandoverState.Accepted, (AccountId?)bob), (handover!.State, handover.AcceptedById));
        Assert.Equal(created.ExpiresAt, handover.ExpiresAt);
        Assert.Equal([nameof(PersonCreated), nameof(HandoverOffered), nameof(PersonHandedOver)], types);
    }

    [Fact]
    [Trait("AC", "AC-01.16")]
    public async Task A_new_link_replaces_the_open_one_and_only_one_stays_open()
    {
        await using var services = await NewServicesAsync();
        var alice = TestData.NewAccount();
        var personId = await CreatePersonAsync(services, alice);

        await InScopeAsync(services, handovers => handovers.OfferAsync(personId, alice, Cancellation));
        await InScopeAsync(services, handovers => handovers.OfferAsync(personId, alice, Cancellation));

        await using var scope = services.CreateAsyncScope();
        var states = await scope.ServiceProvider.GetRequiredService<FamilyGraphDbContext>()
            .Set<ProfileHandover>().Where(handover => handover.PersonId == personId)
            .OrderBy(handover => handover.CreatedAt).ThenBy(handover => handover.Id).Select(handover => handover.State).ToListAsync(Cancellation);
        Assert.Equal(2, states.Count);
        Assert.Single(states, HandoverState.Open);
        Assert.Single(states, HandoverState.Cancelled);
    }

    [Fact]
    [Trait("AC", "AC-01.16")]
    public async Task Two_accounts_accepting_the_same_link_at_once_cannot_both_succeed()
    {
        await using var services = await NewServicesAsync();
        var alice = TestData.NewAccount();
        var personId = await CreatePersonAsync(services, alice);
        var created = await InScopeAsync(services, handovers => handovers.OfferAsync(personId, alice, Cancellation));

        await using var first = services.CreateAsyncScope();
        await using var second = services.CreateAsyncScope();
        var bob = TestData.NewAccount();
        var carol = TestData.NewAccount();
        var firstService = first.ServiceProvider.GetRequiredService<HandoverService>();
        var secondService = second.ServiceProvider.GetRequiredService<HandoverService>();
        // Both load the open link before either saves.
        _ = await first.ServiceProvider.GetRequiredService<IHandoverStore>().FindByTokenHashAsync(HandoverToken.HashOf(created.Token)!, Cancellation);
        _ = await second.ServiceProvider.GetRequiredService<IHandoverStore>().FindByTokenHashAsync(HandoverToken.HashOf(created.Token)!, Cancellation);

        await firstService.AcceptAsync(created.Token, bob, Cancellation);
        await Assert.ThrowsAsync<HandoverClosedException>(() => secondService.AcceptAsync(created.Token, carol, Cancellation));

        await using var scope = services.CreateAsyncScope();
        var person = await scope.ServiceProvider.GetRequiredService<IPersonStore>().FindAsync(personId, Cancellation);
        Assert.True(person!.IsMaintainedBy(bob));
    }

    private static async Task<PersonId> CreatePersonAsync(ServiceProvider services, AccountId maintainer)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PersonService>().CreateAsync(new PersonName("Maya"), PersonDetails.None, maintainer, Cancellation);
    }

    private static async Task<T> InScopeAsync<T>(ServiceProvider services, Func<HandoverService, Task<T>> action)
    {
        await using var scope = services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<HandoverService>());
    }

    private async Task<ServiceProvider> NewServicesAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(await postgres.NewMigratedConnectionStringAsync());
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(new PeopleLimits());
        services.AddSingleton(new HandoverSettings());
        services.AddScoped<PersonService>();
        services.AddScoped<HandoverService>();
        return services.BuildServiceProvider();
    }
}
