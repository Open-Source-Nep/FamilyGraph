using FamilyGraph.Application.Claims;
using FamilyGraph.Application.People;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.Audit;
using FamilyGraph.Business.Claims;
using FamilyGraph.Business.People;
using FamilyGraph.Infrastructure.Persistence;
using FamilyGraph.Infrastructure.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyGraph.Infrastructure.Tests.Claims;

/// <summary>The claim store, queries and use cases against PostgreSQL.</summary>
public class ClaimPersistenceServicesTests(PostgresFixture postgres)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static Viewer As(AccountId account) => new(account, IsAdministrator: false);

    [Fact]
    [Trait("AC", "AC-04.1")]
    [Trait("AC", "AC-04.3")]
    public async Task A_claim_is_saved_listed_approved_and_audited_as_an_identity_claim()
    {
        await using var services = await NewServicesAsync();
        var alice = TestData.NewAccount();
        var bob = TestData.NewAccount();
        var personId = await CreatePersonAsync(services, alice);

        var claim = await InScopeAsync(services, claims => claims.SubmitAsync(personId, "I am Maya.", As(bob), Cancellation));
        var toDecide = await InScopeAsync(services, claims => claims.ListToDecideAsync(As(alice), Cancellation));
        Assert.Equal((claim.Id, "I am Maya."), (Assert.Single(toDecide.Claims).Id, toDecide.Claims[0].Evidence));
        await InScopeAsync(services, async claims =>
        {
            await claims.ApproveAsync(claim.Id, alice, Cancellation);
            return 0;
        });

        await using var scope = services.CreateAsyncScope();
        var person = await scope.ServiceProvider.GetRequiredService<IPersonStore>().FindAsync(personId, Cancellation);
        var stored = await scope.ServiceProvider.GetRequiredService<IClaimStore>().FindAsync(claim.Id, Cancellation);
        var records = await scope.ServiceProvider.GetRequiredService<FamilyGraphDbContext>()
            .Set<AuditEventRecord>().Where(record => record.SubjectId == personId.Value)
            .OrderBy(record => record.Id).Select(record => new { record.Type, record.Family, record.Data }).ToListAsync(Cancellation);
        var mine = await scope.ServiceProvider.GetRequiredService<ClaimService>().ListMineAsync(As(bob), Cancellation);

        Assert.Equal((bob, (AccountId?)bob), (person!.MaintainerId, person.ClaimedById));
        Assert.Equal(ClaimState.Approved, stored!.State);
        Assert.Equal([nameof(PersonCreated), nameof(ClaimSubmitted), nameof(ClaimApproved)], records.Select(r => r.Type));
        Assert.All(records.Skip(1), r => Assert.Equal(AuditFamily.IdentityClaim.Value, r.Family));
        Assert.DoesNotContain(records, r => r.Data.Contains("I am Maya", StringComparison.Ordinal));
        Assert.Equal(ClaimState.Approved, Assert.Single(mine.Claims).State);
    }

    [Fact]
    [Trait("AC", "AC-04.7")]
    public async Task A_profile_with_two_open_claims_is_not_listed_to_decide()
    {
        await using var services = await NewServicesAsync();
        var alice = TestData.NewAccount();
        var personId = await CreatePersonAsync(services, alice);

        await InScopeAsync(services, claims => claims.SubmitAsync(personId, null, As(TestData.NewAccount()), Cancellation));
        Assert.Single((await InScopeAsync(services, claims => claims.ListToDecideAsync(As(alice), Cancellation))).Claims);
        await InScopeAsync(services, claims => claims.SubmitAsync(personId, null, As(TestData.NewAccount()), Cancellation));

        Assert.Empty((await InScopeAsync(services, claims => claims.ListToDecideAsync(As(alice), Cancellation))).Claims);
    }

    [Fact]
    [Trait("AC", "AC-04.12")]
    public async Task One_account_claiming_two_profiles_at_once_cannot_keep_both_open()
    {
        await using var services = await NewServicesAsync();
        var alice = TestData.NewAccount();
        var bob = TestData.NewAccount();
        var maya = await CreatePersonAsync(services, alice);
        var sita = await CreatePersonAsync(services, alice);

        await using var first = services.CreateAsyncScope();
        await using var second = services.CreateAsyncScope();
        await first.ServiceProvider.GetRequiredService<ClaimService>().SubmitAsync(maya, null, As(bob), Cancellation);

        // A second claim whose check ran before the first was saved: the open-claim index refuses it.
        var context = second.ServiceProvider.GetRequiredService<FamilyGraphDbContext>();
        var person = await second.ServiceProvider.GetRequiredService<IPersonStore>().FindAsync(sita, Cancellation);
        context.Add(ProfileClaim.Submit(person!, bob, null, claimantHasClaim: false, DateTimeOffset.UtcNow));

        await Assert.ThrowsAsync<AccountAlreadyClaimedException>(
            () => second.ServiceProvider.GetRequiredService<Application.Persistence.IUnitOfWork>().SaveChangesAsync(Cancellation));
    }

    [Fact]
    [Trait("AC", "AC-04.15")]
    public async Task Deleting_a_profile_cancels_its_open_claim_and_frees_the_claimant()
    {
        await using var services = await NewServicesAsync();
        var alice = TestData.NewAccount();
        var bob = TestData.NewAccount();
        var personId = await CreatePersonAsync(services, alice);
        var claim = await InScopeAsync(services, claims => claims.SubmitAsync(personId, null, As(bob), Cancellation));

        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<PersonService>().DeleteAsync(personId, As(alice), Cancellation);
        }

        await using var check = services.CreateAsyncScope();
        var stored = await check.ServiceProvider.GetRequiredService<IClaimStore>().FindAsync(claim.Id, Cancellation);
        Assert.Equal(ClaimState.Cancelled, stored!.State);
        Assert.False(await check.ServiceProvider.GetRequiredService<IClaimStore>().AccountHasClaimAsync(bob, Cancellation));
    }

    [Fact]
    [Trait("AC", "AC-04.2")]
    [Trait("AC", "AC-04.10")]
    public async Task An_appeal_is_listed_for_administrators_decided_and_its_notes_erased_later()
    {
        var time = new MovableTime(DateTimeOffset.UtcNow);
        await using var services = await NewServicesAsync(time);
        var alice = TestData.NewAccount();
        var bob = TestData.NewAccount();
        var carol = TestData.NewAccount();
        var admin = new Viewer(TestData.NewAccount(), IsAdministrator: true);
        var personId = await CreatePersonAsync(services, alice);
        var other = await CreatePersonAsync(services, alice);
        var claim = await InScopeAsync(services, claims => claims.SubmitAsync(personId, "I am Maya.", As(bob), Cancellation));
        await InScopeAsync(services, claims => claims.SubmitAsync(other, "Me.", As(carol), Cancellation));
        await RunAsync(services, claims => claims.RejectAsync(claim.Id, alice, Cancellation));

        await RunAsync(services, claims => claims.RequestReviewAsync(claim.Id, bob, Cancellation));
        var toDecide = await InScopeAsync(services, claims => claims.ListToDecideAsync(As(alice), Cancellation));
        var review = await InScopeAsync(services, claims => claims.ListForAdministratorsAsync(admin, Cancellation));
        Assert.Equal([claim.Id], review.Claims.Where(c => c.Person.Id == personId || c.Person.Id == other).Select(c => c.Id));
        Assert.DoesNotContain(toDecide.Claims, c => c.Id == claim.Id);
        await RunAsync(services, claims => claims.RejectAsAdministratorAsync(claim.Id, "Not confirmed.", admin, Cancellation));
        await Assert.ThrowsAsync<ClaimWaitingPeriodException>(
            () => InScopeAsync(services, claims => claims.SubmitAsync(personId, null, As(bob), Cancellation)));

        time.Now = time.Now.AddDays(91);
        await RunAsync(services, claims => claims.EraseExpiredNotesAsync(Cancellation));

        await using var scope = services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<IClaimStore>().FindAsync(claim.Id, Cancellation);
        Assert.Equal((ClaimState.Rejected, (string?)null, "Not confirmed.", true), (stored!.State, stored.Evidence, stored.Rationale, stored.Appealed));
        Assert.NotNull(stored.NotesErasedAt);
        var types = await scope.ServiceProvider.GetRequiredService<FamilyGraphDbContext>()
            .Set<AuditEventRecord>().Where(record => record.SubjectId == personId.Value)
            .OrderBy(record => record.Id).Select(record => record.Type).ToListAsync(Cancellation);
        // The test clock stands still, so the rows share a time and their order is not fixed.
        Assert.Equal(
            [nameof(ClaimRejected), nameof(ClaimRejectedByAdministrator), nameof(ClaimReviewRequested), nameof(ClaimSubmitted), nameof(PersonCreated)],
            types.Order(StringComparer.Ordinal));
    }

    private static async Task RunAsync(ServiceProvider services, Func<ClaimService, Task> action)
    {
        await using var scope = services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<ClaimService>());
    }

    private static async Task<PersonId> CreatePersonAsync(ServiceProvider services, AccountId maintainer)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PersonService>().CreateAsync(new PersonName("Maya"), PersonDetails.None, maintainer, Cancellation);
    }

    private static async Task<T> InScopeAsync<T>(ServiceProvider services, Func<ClaimService, Task<T>> action)
    {
        await using var scope = services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<ClaimService>());
    }

    private async Task<ServiceProvider> NewServicesAsync(TimeProvider? time = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(await postgres.NewMigratedConnectionStringAsync());
        services.AddSingleton(time ?? TimeProvider.System);
        services.AddSingleton(new PeopleLimits());
        services.AddSingleton(new ClaimLimits());
        services.AddScoped<PersonService>();
        services.AddScoped<ClaimService>();
        return services.BuildServiceProvider();
    }

    private sealed class MovableTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
