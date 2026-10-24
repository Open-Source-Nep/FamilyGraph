using FamilyGraph.Application.Accounts;
using FamilyGraph.Application.Export;
using FamilyGraph.Application.Import;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.Import;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;
using FamilyGraph.Infrastructure.Persistence;
using FamilyGraph.Infrastructure.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyGraph.Infrastructure.Tests.Import;

/// <summary>The import's transaction, account matching and saved rows against PostgreSQL (D-033).</summary>
public class ImportPersistenceTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 16, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("AC", "AC-09.8")]
    [Trait("AC", "AC-09.12")]
    public async Task An_export_imported_into_an_empty_instance_exports_the_same_rows()
    {
        await using var source = await NewServicesAsync();
        var alice = await CreateAccountAsync(source, "alice@example.org");
        var parent = TestData.NewPerson(alice, "Hari");
        var child = TestData.NewPerson(alice, "Maya");
        var deleted = TestData.NewPerson(alice, "Zebediah");
        child.SetVisibility(ProfileVisibility.Private, alice);
        child.SetGender(Gender.Female, alice);
        var link = TestData.NewLink(parent, child, alice);
        link.SetBirthOrder(parent, alice, 2);
        var marriage = Marriage.Create(parent, deleted, alice, alreadyMarried: false, directlyRelated: false);
        marriage.SetFormer(parent, deleted, alice, isFormer: true);
        deleted.Delete(alice, actorIsAdministrator: false, Now);
        await SaveAsync(source, parent, child, deleted, link, marriage);
        var exported = await ExportAsync(source, alice);

        await using var target = await NewServicesAsync();
        var admin = await CreateAccountAsync(target, "admin@example.org");
        var aliceHere = await CreateAccountAsync(target, "ALICE@example.org");
        await using (var scope = target.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ImportService>()
                .ImportAsync(new Viewer(admin, IsAdministrator: true), ToImport(exported), Cancellation);
        }

        var reimported = await ExportAsync(target, admin);
        Assert.Equal(
            exported.People.Select(person => person with { MaintainerId = aliceHere }),
            reimported.People);
        Assert.Equal(exported.Links, reimported.Links);
        Assert.Equal(exported.Marriages, reimported.Marriages);
        await using var check = target.CreateAsyncScope();
        var record = await check.ServiceProvider.GetRequiredService<FamilyGraphDbContext>()
            .Set<AuditEventRecord>().SingleAsync(r => r.Type == nameof(DataImported), Cancellation);
        Assert.Equal((admin.Value, admin.Value, Now), (record.ActorId, record.SubjectId, record.OccurredAt));
    }

    [Fact]
    [Trait("AC", "AC-09.10")]
    public async Task Only_an_email_that_exactly_one_verified_account_has_finds_an_account()
    {
        await using var services = await NewServicesAsync();
        var bina = await CreateAccountAsync(services, "Bina@Example.org");
        await CreateAccountAsync(services, "unverified@example.org", verified: false);
        await CreateAccountAsync(services, "shared@example.org");
        await CreateAccountAsync(services, "shared@example.org");

        await using var scope = services.CreateAsyncScope();
        var found = await scope.ServiceProvider.GetRequiredService<IImportStore>().FindAccountsByVerifiedEmailAsync(
            ["bina@example.org", "unverified@example.org", "shared@example.org", "nobody@example.org"], Cancellation);

        Assert.Equal(new Dictionary<string, AccountId> { ["bina@example.org"] = bina }, found);
    }

    [Fact]
    [Trait("AC", "AC-09.9")]
    public async Task An_instance_with_a_profile_refuses_the_import_and_nothing_is_saved()
    {
        await using var services = await NewServicesAsync();
        var admin = await CreateAccountAsync(services, "admin@example.org");
        await SaveAsync(services, TestData.NewPerson(admin, "Hari"));
        var data = new ImportData([], [new ImportPerson(Guid.NewGuid(), "Maya", null, null, "Public", null, Guid.NewGuid(), null)], [], []);

        await using (var scope = services.CreateAsyncScope())
        {
            await Assert.ThrowsAsync<InstanceNotEmptyException>(() => scope.ServiceProvider.GetRequiredService<ImportService>()
                .ImportAsync(new Viewer(admin, IsAdministrator: true), data, Cancellation));
        }

        await using var check = services.CreateAsyncScope();
        var context = check.ServiceProvider.GetRequiredService<FamilyGraphDbContext>();
        Assert.Equal(1, await context.Set<Person>().CountAsync(Cancellation));
        Assert.False(await context.Set<AuditEventRecord>().AnyAsync(r => r.Type == nameof(DataImported), Cancellation));
    }

    private sealed record Exported(
        List<ExportedAccount> Accounts, List<ExportedPerson> People, List<ExportedLink> Links, List<ExportedMarriage> Marriages);

    private static async Task<Exported> ExportAsync(ServiceProvider services, AccountId admin)
    {
        await using var scope = services.CreateAsyncScope();
        await using var export = await scope.ServiceProvider.GetRequiredService<ExportService>()
            .StartAsync(new Viewer(admin, IsAdministrator: true), Cancellation);
        return new Exported(
            await export.AccountsAsync(Cancellation).ToListAsync(Cancellation),
            await export.PeopleAsync(Cancellation).ToListAsync(Cancellation),
            await export.LinksAsync(Cancellation).ToListAsync(Cancellation),
            await export.MarriagesAsync(Cancellation).ToListAsync(Cancellation));
    }

    /// <summary>The export's rows as Server would read them back from the file.</summary>
    private static ImportData ToImport(Exported exported) => new(
        exported.Accounts.Select(account => new ImportAccount(account.Id.Value, account.Email)).ToList(),
        exported.People.Select(person => new ImportPerson(
            person.Id.Value,
            person.Name?.FirstName,
            person.Name?.MiddleName,
            person.Name?.LastName,
            person.Visibility.Name,
            person.DeletedAt,
            person.MaintainerId.Value,
            person.Gender?.Name)).ToList(),
        exported.Links.Select(link => new ImportLink(
            link.Id.Value,
            link.ParentId.Value,
            link.ChildId.Value,
            link.Type.Name,
            link.ParentApproval.Name,
            link.ChildApproval.Name,
            link.BirthOrder)).ToList(),
        exported.Marriages.Select(marriage => new ImportMarriage(
            marriage.Id.Value,
            marriage.FirstSpouseId.Value,
            marriage.SecondSpouseId.Value,
            marriage.FirstApproval.Name,
            marriage.SecondApproval.Name,
            marriage.IsFormer)).ToList());

    private static async Task<AccountId> CreateAccountAsync(ServiceProvider services, string email, bool verified = true)
    {
        await using var scope = services.CreateAsyncScope();
        var account = await scope.ServiceProvider.GetRequiredService<IAccountStore>()
            .CreateAsync(new ExternalLogin("Google", Guid.NewGuid().ToString(), email, verified), isAdministrator: false, Cancellation);
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
        services.AddScoped<ImportService>();
        return services.BuildServiceProvider();
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
