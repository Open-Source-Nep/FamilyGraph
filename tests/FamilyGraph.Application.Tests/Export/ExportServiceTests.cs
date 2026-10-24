using FamilyGraph.Application.Export;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Application.Tests.People;
using FamilyGraph.Business.Audit;
using FamilyGraph.Business.Export;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Application.Tests.Export;

public class ExportServiceTests
{
    private static readonly AccountId Admin = new(Guid.NewGuid());
    private static readonly AccountId Alice = new(Guid.NewGuid());
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

    private readonly InMemoryPeople _people = new();
    private readonly FakeExportQueries _queries;

    public ExportServiceTests() => _queries = new FakeExportQueries(() => _people.SaveCount);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private ExportService Service() => new(_queries, _people, _people, new FixedTimeProvider(Now));

    [Fact]
    [Trait("AC", "AC-09.5")]
    public async Task Only_an_administrator_can_export_and_a_refusal_records_nothing()
    {
        await Assert.ThrowsAsync<NotAdministratorException>(
            () => Service().StartAsync(new Viewer(Alice, IsAdministrator: false), Cancellation));

        Assert.Empty(_people.AuditEvents);
        Assert.Null(_queries.SavesWhenOpened);
    }

    [Fact]
    [Trait("AC", "AC-09.7")]
    public async Task An_export_is_recorded_with_the_administrator_and_time_before_the_data_is_read()
    {
        await using var export = await Service().StartAsync(new Viewer(Admin, IsAdministrator: true), Cancellation);

        var (auditEvent, occurredAt) = Assert.Single(_people.AuditEvents);
        Assert.Equal(new DataExported(Admin), auditEvent);
        Assert.Equal(AuditFamily.Administrative, auditEvent.Family);
        Assert.Equal((Now, Now), (occurredAt, export.ExportedAt));
        Assert.Equal(1, _queries.SavesWhenOpened);
    }

    [Fact]
    [Trait("AC", "AC-09.4")]
    [Trait("AC", "AC-09.6")]
    public async Task People_links_and_accounts_pass_through_and_a_deleted_profile_has_no_name()
    {
        var kept = new ExportedPerson(new PersonId(Guid.NewGuid()), new PersonName("Maya"), ProfileVisibility.Private, null, Alice, Gender.Female);
        var deleted = new ExportedPerson(new PersonId(Guid.NewGuid()), PersonName.Erased, ProfileVisibility.Public, Now, Alice, null);
        var link = new ExportedLink(
            new LinkId(Guid.NewGuid()), deleted.Id, kept.Id, ParentChildType.Adoptive, EndpointApproval.Approved, EndpointApproval.Pending, null);
        var account = new ExportedAccount(Alice, "alice@example.org", true);
        _queries.Data = ([account], [kept, deleted], [link]);

        await using var export = await Service().StartAsync(new Viewer(Admin, IsAdministrator: true), Cancellation);

        Assert.Equal([account], await export.AccountsAsync(Cancellation).ToListAsync(Cancellation));
        Assert.Equal([kept, deleted with { Name = null }], await export.PeopleAsync(Cancellation).ToListAsync(Cancellation));
        Assert.Equal([link], await export.LinksAsync(Cancellation).ToListAsync(Cancellation));
    }

    [Fact]
    public async Task Disposing_the_export_closes_the_snapshot()
    {
        var export = await Service().StartAsync(new Viewer(Admin, IsAdministrator: true), Cancellation);

        await export.DisposeAsync();

        Assert.True(_queries.Disposed);
    }

    /// <summary>Remembers how many saves had happened when the snapshot opened.</summary>
    private sealed class FakeExportQueries(Func<int> saveCount) : IExportQueries, IExportSnapshot
    {
        public (List<ExportedAccount> Accounts, List<ExportedPerson> People, List<ExportedLink> Links) Data { get; set; } = ([], [], []);

        public int? SavesWhenOpened { get; private set; }

        public bool Disposed { get; private set; }

        public Task<IExportSnapshot> OpenSnapshotAsync(CancellationToken cancellationToken)
        {
            SavesWhenOpened = saveCount();
            return Task.FromResult<IExportSnapshot>(this);
        }

        public IAsyncEnumerable<ExportedAccount> AccountsAsync(CancellationToken cancellationToken) => Data.Accounts.ToAsyncEnumerable();

        public IAsyncEnumerable<ExportedPerson> PeopleAsync(CancellationToken cancellationToken) => Data.People.ToAsyncEnumerable();

        public IAsyncEnumerable<ExportedLink> LinksAsync(CancellationToken cancellationToken) => Data.Links.ToAsyncEnumerable();

        public IAsyncEnumerable<ExportedMarriage> MarriagesAsync(CancellationToken cancellationToken) =>
            AsyncEnumerable.Empty<ExportedMarriage>();

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
