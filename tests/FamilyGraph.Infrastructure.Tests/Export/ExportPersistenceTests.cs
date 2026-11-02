using FamilyGraph.Application.Accounts;
using FamilyGraph.Application.Export;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.Audit;
using FamilyGraph.Business.Export;
using FamilyGraph.Business.Handovers;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;
using FamilyGraph.Infrastructure.Persistence;
using FamilyGraph.Infrastructure.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyGraph.Infrastructure.Tests.Export;

/// <summary>The export's queries and audit record against PostgreSQL (D-029, D-049).</summary>
public class ExportPersistenceTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A JPEG's first bytes, which is all the format check reads.</summary>
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 1, 2, 3];

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("AC", "AC-09.4")]
    [Trait("AC", "AC-09.6")]
    [Trait("AC", "AC-09.7")]
    public async Task Export_reads_every_account_profile_link_marriage_image_handover_and_the_audit_history()
    {
        await using var services = await NewServicesAsync();
        var alice = await CreateAccountAsync(services, "alice@example.org", "google-alice");
        var idle = await CreateAccountAsync(services, "idle@example.org", "google-idle");
        var admin = await CreateAccountAsync(services, "admin@example.org", "google-admin");
        var parent = TestData.NewPerson(alice, "Hari");
        var child = TestData.NewPerson(alice, "Maya");
        var deleted = TestData.NewPerson(alice, "Zebediah");
        child.SetVisibility(ProfileVisibility.Private, alice);
        var image = parent.SetImage(Jpeg, alice);
        var kept = TestData.NewLink(parent, child, alice);
        var removed = TestData.NewLink(deleted, child, alice);
        removed.Delete(deleted, child, alice, Now);
        var marriage = Marriage.Create(parent, deleted, alice, alreadyMarried: false, directlyRelated: false);
        marriage.SetFormer(parent, deleted, alice, isFormer: true);
        var deletedMarriage = Marriage.Create(parent, child, alice, alreadyMarried: false, directlyRelated: false);
        deletedMarriage.Delete(parent, child, alice, Now);
        deleted.Delete(alice, actorIsAdministrator: false, Now);
        var handover = ProfileHandover.Offer(child, alice, "hash", Now, Now.AddDays(14));
        await SaveAsync(services, parent, child, deleted, image, kept, removed, marriage, deletedMarriage, handover);

        await using var scope = services.CreateAsyncScope();
        await using var export = await scope.ServiceProvider.GetRequiredService<ExportService>()
            .StartAsync(new Viewer(admin, IsAdministrator: true), Cancellation);
        var accounts = await export.AccountsAsync(Cancellation).ToListAsync(Cancellation);
        var people = await export.PeopleAsync(Cancellation).ToListAsync(Cancellation);
        var links = await export.LinksAsync(Cancellation).ToListAsync(Cancellation);
        var marriages = await export.MarriagesAsync(Cancellation).ToListAsync(Cancellation);
        var images = await export.ImagesAsync(Cancellation).ToListAsync(Cancellation);
        var contents = await export.ImageContentsAsync(Cancellation).ToListAsync(Cancellation);
        var handovers = await export.HandoversAsync(Cancellation).ToListAsync(Cancellation);
        var history = await export.AuditEventsAsync(Cancellation).ToListAsync(Cancellation);

        Assert.Equal(
            new HashSet<ExportedAccount>
            {
                new(alice, "google-alice", "alice@example.org", true),
                new(idle, "google-idle", "idle@example.org", true),
                new(admin, "google-admin", "admin@example.org", true),
            },
            accounts.ToHashSet());
        Assert.Equal(
            new HashSet<ExportedPerson>
            {
                new ExportedPerson(parent.Id, new PersonName("Hari"), ProfileVisibility.Public, null, alice, null, null, null, false, image.Id),
                new ExportedPerson(child.Id, new PersonName("Maya"), ProfileVisibility.Private, null, alice, null, null, null, false, null),
                new ExportedPerson(deleted.Id, null, ProfileVisibility.Public, Now, alice, null, null, null, false, null),
            },
            people.ToHashSet());
        Assert.Equal(
            new HashSet<ExportedLink>
            {
                new(kept.Id, parent.Id, child.Id, ParentChildType.Biological, EndpointApproval.Approved, EndpointApproval.Approved, null, null),
                new(removed.Id, deleted.Id, child.Id, ParentChildType.Biological, EndpointApproval.Approved, EndpointApproval.Approved, null, Now),
            },
            links.ToHashSet());
        Assert.Equal(
            new HashSet<ExportedMarriage>
            {
                new(marriage.Id, marriage.FirstSpouseId, marriage.SecondSpouseId, EndpointApproval.Approved, EndpointApproval.Approved, true, null),
                new(deletedMarriage.Id, deletedMarriage.FirstSpouseId, deletedMarriage.SecondSpouseId, EndpointApproval.Approved, EndpointApproval.Approved, false, Now),
            },
            marriages.ToHashSet());
        Assert.Equal([new ExportedImage(image.Id, parent.Id, ImageFormat.Jpeg)], images);
        Assert.Equal(Jpeg, Assert.Single(contents).Content);
        Assert.Equal(
            [new ExportedHandover(handover.Id, child.Id, alice, "hash", Now, Now.AddDays(14), HandoverState.Open, null, null)],
            handovers);
        var exported = Assert.Single(history, row => row.Type == nameof(DataExported));
        Assert.Equal((admin, admin.Value, AuditFamily.Administrative), (exported.ActorId, exported.SubjectId, exported.Family));

        await using var check = services.CreateAsyncScope();
        var record = await check.ServiceProvider.GetRequiredService<FamilyGraphDbContext>()
            .Set<AuditEventRecord>().SingleAsync(r => r.Type == nameof(DataExported), Cancellation);
        Assert.Equal((admin.Value, admin.Value), (record.ActorId, record.SubjectId));
    }

    [Fact]
    [Trait("AC", "AC-09.6")]
    public async Task Export_reads_one_moment_even_when_data_changes_while_it_streams()
    {
        await using var services = await NewServicesAsync();
        var alice = await CreateAccountAsync(services, "alice@example.org");
        var before = TestData.NewPerson(alice, "Hari");
        await SaveAsync(services, before);

        await using var scope = services.CreateAsyncScope();
        await using var export = await scope.ServiceProvider.GetRequiredService<ExportService>()
            .StartAsync(new Viewer(alice, IsAdministrator: true), Cancellation);
        _ = await export.AccountsAsync(Cancellation).ToListAsync(Cancellation);
        var later = TestData.NewPerson(alice, "Maya");
        await SaveAsync(services, later);
        var people = await export.PeopleAsync(Cancellation).ToListAsync(Cancellation);

        Assert.Equal([before.Id], people.Select(person => person.Id));
    }

    private static async Task<AccountId> CreateAccountAsync(ServiceProvider services, string email, string? googleId = null)
    {
        await using var scope = services.CreateAsyncScope();
        var account = await scope.ServiceProvider.GetRequiredService<IAccountStore>()
            .CreateAsync(new ExternalLogin("Google", googleId ?? Guid.NewGuid().ToString(), email, EmailVerified: true), isAdministrator: false, Cancellation);
        return account.Id;
    }

    private static async Task SaveAsync(ServiceProvider services, params object[] entities)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<FamilyGraphDbContext>();
        context.AddRange(entities);
        await context.SaveChangesAsync(Cancellation);
    }

    private async Task<ServiceProvider> NewServicesAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(await postgres.NewMigratedConnectionStringAsync());
        services.AddSingleton<TimeProvider>(new FixedTime(Now));
        services.AddScoped<ExportService>();
        return services.BuildServiceProvider();
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
