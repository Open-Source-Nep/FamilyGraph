using FamilyGraph.Application.Accounts;
using FamilyGraph.Application.Export;
using FamilyGraph.Application.Import;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.Handovers;
using FamilyGraph.Business.Import;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;
using FamilyGraph.Infrastructure.Persistence;
using FamilyGraph.Infrastructure.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyGraph.Infrastructure.Tests.Import;

/// <summary>The import's transaction, accounts and saved rows against PostgreSQL (D-033, D-049).</summary>
public class ImportPersistenceTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 16, 0, 0, TimeSpan.Zero);

    /// <summary>A JPEG's first bytes, which is all the format check reads.</summary>
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 1, 2, 3];

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("AC", "AC-09.8")]
    [Trait("AC", "AC-09.10")]
    [Trait("AC", "AC-09.12")]
    public async Task An_export_imported_into_an_empty_instance_exports_the_same_rows_and_accounts()
    {
        await using var source = await NewServicesAsync();
        var alice = await CreateAccountAsync(source, "alice@example.org", "google-alice");
        var sourceAdmin = await CreateAccountAsync(source, "admin@example.org", "google-admin");
        var parent = TestData.NewPerson(alice, "Hari");
        var child = TestData.NewPerson(sourceAdmin, "Maya");
        var deleted = TestData.NewPerson(alice, "Zebediah");
        child.SetVisibility(ProfileVisibility.Private, sourceAdmin);
        child.SetGender(Gender.Female, sourceAdmin);
        var image = parent.SetImage(Jpeg, alice);
        var link = TestData.NewLink(parent, child, alice);
        link.SetBirthOrder(parent, alice, 2);
        var removed = TestData.NewLink(deleted, parent, alice);
        removed.Delete(deleted, parent, alice, Now);
        var marriage = Marriage.Create(parent, deleted, alice, alreadyMarried: false, directlyRelated: false);
        marriage.SetFormer(parent, deleted, alice, isFormer: true);
        deleted.Delete(alice, actorIsAdministrator: false, Now);
        var handover = ProfileHandover.Offer(child, sourceAdmin, "hash", Now, Now.AddDays(14));
        await SaveAsync(source, parent, child, deleted, image, link, removed, marriage, handover);
        var exported = await ExportAsync(source, sourceAdmin);

        await using var target = await NewServicesAsync();
        var admin = await CreateAccountAsync(target, "admin@example.org", "google-admin");
        ImportResult result;
        await using (var scope = target.CreateAsyncScope())
        {
            result = await scope.ServiceProvider.GetRequiredService<ImportService>()
                .ImportAsync(new Viewer(admin, IsAdministrator: true), ToImport(exported), Cancellation);
        }

        Assert.Equal((1, 1, 1), (result.CreatedAccounts, result.ReusedAccounts, result.Images));
        AccountId Here(AccountId id) => id == sourceAdmin ? admin : id;
        var reimported = await ExportAsync(target, admin);
        Assert.Equal(
            exported.Accounts.Select(account => account with { Id = Here(account.Id) }).ToHashSet(),
            reimported.Accounts.ToHashSet());
        Assert.Equal(exported.People.Select(person => person with { MaintainerId = Here(person.MaintainerId) }), reimported.People);
        Assert.Equal(exported.Links, reimported.Links);
        Assert.Equal(exported.Marriages, reimported.Marriages);
        Assert.Equal(exported.Images, reimported.Images);
        Assert.Equal(Jpeg, Assert.Single(reimported.Contents).Content);
        Assert.Equal(exported.Handovers.Select(row => row with { OfferedById = admin }), reimported.Handovers);
        Assert.Equal(
            exported.AuditEvents.Select(row => row with { ActorId = Here(row.ActorId), SubjectId = Here(new AccountId(row.SubjectId)).Value }),
            reimported.AuditEvents.Where(row => exported.AuditEvents.Any(old => old.Id == row.Id)));
        await using var check = target.CreateAsyncScope();
        var record = await check.ServiceProvider.GetRequiredService<FamilyGraphDbContext>()
            .Set<AuditEventRecord>().SingleAsync(r => r.Type == nameof(DataImported), Cancellation);
        Assert.Equal((admin.Value, admin.Value, Now), (record.ActorId, record.SubjectId, record.OccurredAt));

        // The recreated account signs in with its Google login and keeps its ID.
        var signedIn = await FindByGoogleIdAsync(target, "google-alice");
        Assert.Equal(alice, signedIn);
    }

    [Fact]
    [Trait("AC", "AC-09.10")]
    public async Task Accounts_are_found_by_google_id_and_taken_ids_are_reported()
    {
        await using var services = await NewServicesAsync();
        var bina = await CreateAccountAsync(services, "bina@example.org", "google-bina");

        await using var scope = services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IImportStore>();
        var found = await store.FindAccountsByGoogleIdAsync(["google-bina", "google-nobody"], Cancellation);
        var taken = await store.FindExistingAccountsAsync([bina, new AccountId(Guid.NewGuid())], Cancellation);

        Assert.Equal(new Dictionary<string, AccountId> { ["google-bina"] = bina }, found);
        Assert.Equal([bina], taken);
    }

    [Fact]
    [Trait("AC", "AC-09.9")]
    public async Task An_instance_with_a_profile_refuses_the_import_and_nothing_is_saved()
    {
        await using var services = await NewServicesAsync();
        var admin = await CreateAccountAsync(services, "admin@example.org", "google-admin");
        await SaveAsync(services, TestData.NewPerson(admin, "Hari"));
        var account = Guid.NewGuid();
        var data = new ImportData(
            [new ImportAccount(account, "google-maya", "maya@example.org", true)],
            [new ImportPerson(Guid.NewGuid(), "Maya", null, null, "Public", null, account, null)],
            [], [], [], [], [], new MemoryFiles([]));

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
        List<ExportedAccount> Accounts,
        List<ExportedPerson> People,
        List<ExportedLink> Links,
        List<ExportedMarriage> Marriages,
        List<ExportedImage> Images,
        List<ExportedImageContent> Contents,
        List<ExportedHandover> Handovers,
        List<ExportedAuditEvent> AuditEvents);

    private static async Task<Exported> ExportAsync(ServiceProvider services, AccountId admin)
    {
        await using var scope = services.CreateAsyncScope();
        await using var export = await scope.ServiceProvider.GetRequiredService<ExportService>()
            .StartAsync(new Viewer(admin, IsAdministrator: true), Cancellation);
        return new Exported(
            await export.AccountsAsync(Cancellation).ToListAsync(Cancellation),
            await export.PeopleAsync(Cancellation).ToListAsync(Cancellation),
            await export.LinksAsync(Cancellation).ToListAsync(Cancellation),
            await export.MarriagesAsync(Cancellation).ToListAsync(Cancellation),
            await export.ImagesAsync(Cancellation).ToListAsync(Cancellation),
            await export.ImageContentsAsync(Cancellation).ToListAsync(Cancellation),
            await export.HandoversAsync(Cancellation).ToListAsync(Cancellation),
            await export.AuditEventsAsync(Cancellation).ToListAsync(Cancellation));
    }

    /// <summary>The export's rows as Server would read them back from the zip.</summary>
    private static ImportData ToImport(Exported exported) => new(
        exported.Accounts.Select(account => new ImportAccount(account.Id.Value, account.GoogleId, account.Email, account.EmailVerified)).ToList(),
        exported.People.Select(person => new ImportPerson(
            person.Id.Value,
            person.Name?.FirstName,
            person.Name?.MiddleName,
            person.Name?.LastName,
            person.Visibility.Name,
            person.DeletedAt,
            person.MaintainerId.Value,
            person.Gender?.Name,
            ImageId: person.ImageId?.Value)).ToList(),
        exported.Links.Select(link => new ImportLink(
            link.Id.Value,
            link.ParentId.Value,
            link.ChildId.Value,
            link.Type.Name,
            link.ParentApproval.Name,
            link.ChildApproval.Name,
            link.BirthOrder,
            link.DeletedAt)).ToList(),
        exported.Marriages.Select(marriage => new ImportMarriage(
            marriage.Id.Value,
            marriage.FirstSpouseId.Value,
            marriage.SecondSpouseId.Value,
            marriage.FirstApproval.Name,
            marriage.SecondApproval.Name,
            marriage.IsFormer,
            marriage.DeletedAt)).ToList(),
        exported.Images.Select(image => new ImportImage(image.Id.Value, image.PersonId.Value, image.Id.Value.ToString())).ToList(),
        exported.Handovers.Select(handover => new ImportHandover(
            handover.Id.Value,
            handover.PersonId.Value,
            handover.OfferedById.Value,
            handover.TokenHash,
            handover.CreatedAt,
            handover.ExpiresAt,
            handover.State.Name,
            handover.AcceptedById?.Value,
            handover.ClosedAt)).ToList(),
        exported.AuditEvents.Select(row => new ImportAuditEvent(
            row.Id, row.OccurredAt, row.Family.Name, row.Type, row.ActorId.Value, row.SubjectId, row.Data)).ToList(),
        new MemoryFiles(exported.Contents.ToDictionary(content => content.Id.Value.ToString(), content => content.Content)));

    private sealed class MemoryFiles(Dictionary<string, byte[]> files) : IImportFiles
    {
        public long? LengthOf(string path) => files.TryGetValue(path, out var content) ? content.Length : null;

        public Task<byte[]> ReadAsync(string path, CancellationToken cancellationToken) => Task.FromResult(files[path]);
    }

    private static async Task<AccountId?> FindByGoogleIdAsync(ServiceProvider services, string googleId)
    {
        await using var scope = services.CreateAsyncScope();
        var account = await scope.ServiceProvider.GetRequiredService<IAccountStore>().FindByLoginAsync("Google", googleId, Cancellation);
        return account?.Id;
    }

    private static async Task<AccountId> CreateAccountAsync(ServiceProvider services, string email, string googleId)
    {
        await using var scope = services.CreateAsyncScope();
        var account = await scope.ServiceProvider.GetRequiredService<IAccountStore>()
            .CreateAsync(new ExternalLogin("Google", googleId, email, EmailVerified: true), isAdministrator: false, Cancellation);
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
