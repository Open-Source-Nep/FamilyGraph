using FamilyGraph.Application.Import;
using FamilyGraph.Application.People;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Application.Tests.People;
using FamilyGraph.Business.Audit;
using FamilyGraph.Business.Import;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Application.Tests.Import;

public class ImportServiceTests
{
    private static readonly AccountId Admin = new(Guid.NewGuid());
    private static readonly AccountId Bina = new(Guid.NewGuid());
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 16, 0, 0, TimeSpan.Zero);
    private static readonly Viewer Administrator = new(Admin, IsAdministrator: true);

    private static readonly Guid FileBina = Guid.NewGuid();
    private static readonly Guid FileGone = Guid.NewGuid();

    private readonly InMemoryPeople _people = new();
    private readonly FakeImportStore _store;

    public ImportServiceTests() => _store = new FakeImportStore(_people);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private ImportService Service() => new(_store, _people, _people, _people, _people, _people, new FixedTimeProvider(Now));

    private static ImportPerson Person(string firstName, Guid maintainerId, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), firstName, null, null, "Public", null, maintainerId, null);

    private static ImportLink Link(Guid parentId, Guid childId, string type = "Biological") =>
        new(Guid.NewGuid(), parentId, childId, type, "Approved", "Approved", null);

    [Fact]
    [Trait("AC", "AC-09.9")]
    public async Task Only_an_administrator_can_import_and_a_refusal_saves_nothing()
    {
        var data = new ImportData([], [Person("Maya", FileBina)], [], []);

        await Assert.ThrowsAsync<ImportNotAdministratorException>(
            () => Service().ImportAsync(new Viewer(Bina, IsAdministrator: false), data, Cancellation));

        Assert.Empty(_people.People);
        Assert.Equal(0, _people.SaveCount);
    }

    [Fact]
    [Trait("AC", "AC-09.9")]
    public async Task An_instance_with_any_profile_refuses_the_import()
    {
        var existing = Business.People.Person.Create(new PersonName("Hari"), Bina);
        existing.Delete(Bina, actorIsAdministrator: false, Now);
        _people.People[existing.Id] = existing;

        await Assert.ThrowsAsync<InstanceNotEmptyException>(
            () => Service().ImportAsync(Administrator, new ImportData([], [Person("Maya", FileBina)], [], []), Cancellation));

        Assert.Single(_people.People);
        Assert.False(_store.Committed);
    }

    [Fact]
    [Trait("AC", "AC-09.8")]
    [Trait("AC", "AC-09.10")]
    [Trait("AC", "AC-09.12")]
    public async Task Rows_keep_their_ids_and_states_and_maintainers_are_matched_by_verified_email_or_go_to_the_importer()
    {
        _store.Accounts["bina@example.org"] = Bina;
        var parent = Person("Hari", FileBina);
        var child = Person("Maya", FileGone) with { Visibility = "Private", Gender = "Female" };
        var orphan = Person("Sita", Guid.NewGuid());
        var deleted = new ImportPerson(Guid.NewGuid(), null, null, null, "Public", Now, FileBina, null);
        var link = new ImportLink(Guid.NewGuid(), parent.Id, child.Id, "Adoptive", "Approved", "Disagreed", 1);
        var marriage = new ImportMarriage(Guid.NewGuid(), deleted.Id, parent.Id, "Pending", "Approved", IsFormer: true);
        var data = new ImportData(
            [new ImportAccount(FileBina, " BINA@example.org "), new ImportAccount(FileGone, "gone@example.org")],
            [parent, child, orphan, deleted],
            [link],
            [marriage]);

        var result = await Service().ImportAsync(Administrator, data, Cancellation);

        Assert.Equal(new ImportResult(People: 4, Links: 1, Marriages: 1, MatchedAccounts: 1, AssignedToImporter: 2), result);
        Assert.True(_store.Committed);
        var maya = _people.People[new PersonId(child.Id)];
        Assert.Equal((new PersonName("Maya"), ProfileVisibility.Private, Gender.Female, Admin), (maya.Name, maya.Visibility, maya.Gender, maya.MaintainerId));
        Assert.Equal(Bina, _people.People[new PersonId(parent.Id)].MaintainerId);
        Assert.Equal(Admin, _people.People[new PersonId(orphan.Id)].MaintainerId);
        Assert.Equal((PersonName.Erased, Now), (_people.People[new PersonId(deleted.Id)].Name, _people.People[new PersonId(deleted.Id)].DeletedAt));
        var savedLink = _people.Links[new LinkId(link.Id)];
        Assert.Equal(
            (ParentChildType.Adoptive, EndpointApproval.Approved, EndpointApproval.Disagreed, (int?)1),
            (savedLink.Type, savedLink.ParentApproval, savedLink.ChildApproval, savedLink.BirthOrder));
        var savedMarriage = _people.Marriages[new LinkId(marriage.Id)];
        Assert.Equal(EndpointApproval.Pending, savedMarriage.ApprovalOf(new PersonId(deleted.Id)));
        Assert.True(savedMarriage.IsFormer);
        var (auditEvent, occurredAt) = Assert.Single(_people.AuditEvents);
        Assert.Equal((new DataImported(Admin), AuditFamily.Administrative, Now), (auditEvent, auditEvent.Family, occurredAt));
    }

    [Fact]
    [Trait("AC", "AC-09.11")]
    public async Task Any_invalid_row_refuses_the_whole_file_and_each_problem_is_listed_with_its_place()
    {
        var parent = Person("Hari", FileBina);
        var child = Person("Maya", FileBina);
        var spouse = Person("Sita", FileBina);
        var data = new ImportData(
            [],
            [parent, child, spouse, Person(" ", FileBina), Person("Ram", FileBina, parent.Id), Person("Gita", FileBina) with { Gender = "Unknown" }],
            [Link(parent.Id, child.Id), Link(parent.Id, child.Id), Link(child.Id, Guid.NewGuid()), Link(child.Id, parent.Id), Link(spouse.Id, child.Id, "Step")],
            [new ImportMarriage(Guid.NewGuid(), child.Id, parent.Id, "Approved", "Approved", false)]);

        var refused = await Assert.ThrowsAsync<InvalidImportFileException>(() => Service().ImportAsync(Administrator, data, Cancellation));

        Assert.Equal(
            [
                ("people[3]", "person.first_name_required"),
                ("people[4]", "import.duplicate_id"),
                ("people[5]", "import.invalid_value"),
                ("links[1]", "import.duplicate_link"),
                ("links[2]", "import.unknown_person"),
                ("links[4]", "import.invalid_value"),
                ("marriages[0]", "import.parent_child_marriage"),
                ("links", "import.link_cycle"),
            ],
            refused.Problems.Select(problem => (problem.Path, problem.Code)));
        Assert.False(refused.MoreProblems);
        Assert.Empty(_people.People);
        Assert.Empty(_people.AuditEvents);
        Assert.False(_store.Committed);
    }

    [Fact]
    [Trait("AC", "AC-09.11")]
    public async Task A_refused_file_lists_the_first_fifty_problems_and_says_there_are_more()
    {
        var data = new ImportData([], Enumerable.Range(0, 60).Select(_ => Person("", FileBina)).ToList(), [], []);

        var refused = await Assert.ThrowsAsync<InvalidImportFileException>(() => Service().ImportAsync(Administrator, data, Cancellation));

        Assert.Equal(ImportService.MaxProblems, refused.Problems.Count);
        Assert.Equal("people[49]", refused.Problems[^1].Path);
        Assert.True(refused.MoreProblems);
    }

    [Fact]
    [Trait("AC", "AC-09.11")]
    public async Task A_name_part_longer_than_a_profile_allows_is_a_problem()
    {
        var data = new ImportData([], [Person("Maya", FileBina) with { LastName = new string('x', ImportService.MaxNamePartLength + 1) }], [], []);

        var refused = await Assert.ThrowsAsync<InvalidImportFileException>(() => Service().ImportAsync(Administrator, data, Cancellation));

        Assert.Equal("import.name_too_long", Assert.Single(refused.Problems).Code);
    }

    /// <summary>Emails map to accounts as the real store would answer; the transaction only records a commit.</summary>
    private sealed class FakeImportStore(InMemoryPeople people) : IImportStore, IImportTransaction
    {
        public Dictionary<string, AccountId> Accounts { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool Committed { get; private set; }

        public Task<IImportTransaction> BeginAsync(CancellationToken cancellationToken) => Task.FromResult<IImportTransaction>(this);

        public Task<bool> HasPeopleAsync(CancellationToken cancellationToken) => Task.FromResult(people.People.Count > 0);

        public Task<IReadOnlyDictionary<string, AccountId>> FindAccountsByVerifiedEmailAsync(
            IReadOnlyCollection<string> emails, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, AccountId>>(emails
                .Where(Accounts.ContainsKey)
                .ToDictionary(email => email, email => Accounts[email], StringComparer.OrdinalIgnoreCase));

        public Task CommitAsync(CancellationToken cancellationToken)
        {
            Committed = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    [Trait("AC", "AC-09.8")]
    public async Task Description_and_birth_date_come_back_and_files_without_them_still_import()
    {
        var maya = Person("Maya", FileBina) with
        {
            Description = "Teacher",
            BirthDate = new ImportBirthDate("Bs", 2082, 6, 16),
            BirthDateShown = true,
        };
        var hari = Person("Hari", FileBina);

        await Service().ImportAsync(Administrator, new ImportData([], [maya, hari], [], []), Cancellation);

        var imported = _people.People[new PersonId(maya.Id)];
        Assert.Equal(("Teacher", BirthDates.Create(BirthCalendar.Bs, 2082, 6, 16), true), (imported.Description, imported.BirthDate, imported.BirthDateShown));
        Assert.Equal((null, null, false), (_people.People[new PersonId(hari.Id)].Description, _people.People[new PersonId(hari.Id)].BirthDate, _people.People[new PersonId(hari.Id)].BirthDateShown));
    }

    [Fact]
    [Trait("AC", "AC-09.11")]
    public async Task A_birth_date_that_does_not_exist_or_a_long_description_refuses_the_file()
    {
        var data = new ImportData(
            [],
            [
                Person("Maya", FileBina) with { BirthDate = new ImportBirthDate("Ad", 1952, 2, 30) },
                Person("Hari", FileBina) with { BirthDate = new ImportBirthDate("Lunar", 1952, null, null) },
                Person("Sita", FileBina) with { Description = new string('a', 201) },
            ],
            [],
            []);

        var refused = await Assert.ThrowsAsync<InvalidImportFileException>(() => Service().ImportAsync(Administrator, data, Cancellation));

        Assert.Equal(
            [("people[0]", "person.birth_date_invalid"), ("people[1]", "import.invalid_value"), ("people[2]", "person.description_too_long")],
            refused.Problems.Select(problem => (problem.Path, problem.Code)));
        Assert.Empty(_people.People);
    }
}
