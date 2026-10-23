using System.Text.Json;
using FamilyGraph.Application.People;
using FamilyGraph.Application.Persistence;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.Audit;
using FamilyGraph.Business.People;
using FamilyGraph.Infrastructure.Persistence;
using FamilyGraph.Infrastructure.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyGraph.Infrastructure.Tests.People;

/// <summary>The person store, queries, audit log and unit of work against PostgreSQL (#12).</summary>
public class PersonPersistenceServicesTests(PostgresFixture postgres)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static Viewer As(AccountId account) => new(account, IsAdministrator: false);

    [Fact]
    public async Task Added_person_and_audit_events_are_saved_together()
    {
        await using var services = await NewServicesAsync();
        var maintainer = TestData.NewAccount();
        var occurredAt = new DateTimeOffset(2026, 10, 22, 9, 30, 0, TimeSpan.Zero);
        var person = TestData.NewPerson(maintainer, "Maya");

        await using (var scope = services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IPersonStore>().Add(person);
            scope.ServiceProvider.GetRequiredService<IAuditLog>().Add(person.TakeEvents(), occurredAt);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Cancellation);
        }

        await using (var scope = services.CreateAsyncScope())
        {
            var loaded = await scope.ServiceProvider.GetRequiredService<IPersonStore>().FindAsync(person.Id, Cancellation);
            var record = await scope.ServiceProvider.GetRequiredService<FamilyGraphDbContext>()
                .Set<AuditEventRecord>().SingleAsync(Cancellation);

            Assert.Equal(new PersonName("Maya"), loaded!.Name);
            Assert.Equal(nameof(PersonCreated), record.Type);
            Assert.Equal(AuditFamily.Profile.Value, record.Family);
            Assert.Equal(person.Id.Value, record.SubjectId);
            Assert.Equal(occurredAt, record.OccurredAt);
        }
    }

    [Fact]
    [Trait("AC", "AC-05.7")]
    [Trait("AC", "AC-05.1")]
    public async Task Visibility_change_is_saved_with_its_audit_event_and_hides_the_name_from_others()
    {
        await using var services = await NewServicesAsync();
        var maintainer = TestData.NewAccount();

        PersonId id;
        await using (var scope = services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<PersonService>();
            id = await service.CreateAsync(new PersonName("Maya"), maintainer, Cancellation);
        }

        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<PersonService>()
                .SetVisibilityAsync(id, ProfileVisibility.Private, maintainer, Cancellation);
        }

        await using (var scope = services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<PersonService>();
            var asMaintainer = await service.GetAsync(id, As(maintainer), Cancellation);
            var asOther = await service.GetAsync(id, As(TestData.NewAccount()), Cancellation);
            var record = await scope.ServiceProvider.GetRequiredService<FamilyGraphDbContext>()
                .Set<AuditEventRecord>().SingleAsync(r => r.Type == nameof(PersonVisibilityChanged), Cancellation);

            Assert.Equal((ProfileVisibility.Private, new PersonName("Maya")), (asMaintainer.Visibility, asMaintainer.Name));
            Assert.Equal((null, PersonPlaceholder.Private), (asOther.Name, asOther.Placeholder));
            using var data = JsonDocument.Parse(record.Data);
            Assert.Equal(
                [("personId", id.Value.ToString()), ("visibility", "2")],
                data.RootElement.EnumerateObject().Select(property => (property.Name, property.Value.ToString())));
        }
    }

    [Fact]
    [Trait("AC", "AC-01.8")]
    public async Task Rename_through_the_service_is_saved_with_its_audit_event()
    {
        await using var services = await NewServicesAsync();
        var maintainer = TestData.NewAccount();

        PersonId id;
        await using (var scope = services.CreateAsyncScope())
        {
            id = await scope.ServiceProvider.GetRequiredService<PersonService>()
                .CreateAsync(new PersonName("Maya"), maintainer, Cancellation);
        }

        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<PersonService>()
                .RenameAsync(id, new PersonName("Maya", "Devi", "Rai"), maintainer, Cancellation);
        }

        await using (var scope = services.CreateAsyncScope())
        {
            var view = await scope.ServiceProvider.GetRequiredService<PersonService>().GetAsync(id, As(maintainer), Cancellation);
            var types = await scope.ServiceProvider.GetRequiredService<FamilyGraphDbContext>()
                .Set<AuditEventRecord>().OrderBy(record => record.Id).Select(record => record.Type).ToListAsync(Cancellation);

            Assert.Equal(new PersonName("Maya", "Devi", "Rai"), view.Name);
            Assert.Equal([nameof(PersonCreated), nameof(PersonRenamed)], types);
        }
    }

    [Fact]
    public async Task Find_says_whether_the_viewer_maintains_the_profile()
    {
        await using var services = await NewServicesAsync();
        var maintainer = TestData.NewAccount();
        var person = await SaveAsync(services, TestData.NewPerson(maintainer, "Maya"));

        await using var scope = services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IPersonQueries>();

        Assert.True((await queries.FindAsync(person.Id, maintainer, Cancellation))!.MaintainedByViewer);
        Assert.False((await queries.FindAsync(person.Id, TestData.NewAccount(), Cancellation))!.MaintainedByViewer);
        Assert.Null(await queries.FindAsync(PersonId.New(), maintainer, Cancellation));
    }

    [Fact]
    [Trait("AC", "AC-01.2")]
    public async Task Maintained_list_is_sorted_by_name_limited_and_excludes_other_maintainers()
    {
        await using var services = await NewServicesAsync();
        var maintainer = TestData.NewAccount();
        await SaveAsync(services, TestData.NewPerson(maintainer, "Maya"));
        await SaveAsync(services, TestData.NewPerson(maintainer, "Gita"));
        await SaveAsync(services, TestData.NewPerson(maintainer, "Asha"));
        await SaveAsync(services, TestData.NewPerson(TestData.NewAccount(), "Bina"));

        await using var scope = services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IPersonQueries>();
        var all = await queries.ListMaintainedByAsync(maintainer, limit: 3, Cancellation);
        var cut = await queries.ListMaintainedByAsync(maintainer, limit: 2, Cancellation);

        Assert.Equal(["Asha", "Gita", "Maya"], all.People.Select(person => person.Name.FirstName));
        Assert.False(all.IsTruncated);
        Assert.Equal(["Asha", "Gita"], cut.People.Select(person => person.Name.FirstName));
        Assert.True(cut.IsTruncated);
    }

    private static async Task<Person> SaveAsync(ServiceProvider services, Person person)
    {
        await using var scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IPersonStore>().Add(person);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Cancellation);
        return person;
    }

    private async Task<ServiceProvider> NewServicesAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(await postgres.NewMigratedConnectionStringAsync());
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(new PeopleLimits());
        services.AddScoped<PersonService>();
        return services.BuildServiceProvider();
    }
}
