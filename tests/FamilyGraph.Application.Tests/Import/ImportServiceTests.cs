using FamilyGraph.Application.Import;
using FamilyGraph.Application.People;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Application.Tests.Handovers;
using FamilyGraph.Application.Tests.People;
using FamilyGraph.Business.Audit;
using FamilyGraph.Business.Handovers;
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
    private static readonly Guid FileAdmin = Guid.NewGuid();

    /// <summary>A JPEG's first bytes, which is all the format check reads.</summary>
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10];

    private readonly InMemoryPeople _people = new();
    private readonly InMemoryHandovers _handovers = new();
    private readonly FakeImportStore _store;
    private readonly FakeFiles _files = new();

    public ImportServiceTests() => _store = new FakeImportStore(_people);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private ImportService Service() => new(_store, _people, _people, _people, _handovers, _people, _people, new FixedTimeProvider(Now));

    private static ImportPerson Person(string firstName, Guid maintainerId, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), firstName, null, null, "Public", null, maintainerId, null);

    private static ImportLink Link(Guid parentId, Guid childId, string type = "Biological") =>
        new(Guid.NewGuid(), parentId, childId, type, "Approved", "Approved", null);

    private static ImportAccount Account(Guid id, string googleId = "") =>
        new(id, googleId.Length > 0 ? googleId : $"google-{id}", $"{id}@example.org", true);

    private ImportData Data(
        IReadOnlyList<ImportPerson> people,
        IReadOnlyList<ImportLink>? links = null,
        IReadOnlyList<ImportMarriage>? marriages = null,
        IReadOnlyList<ImportAccount>? accounts = null,
        IReadOnlyList<ImportImage>? images = null,
        IReadOnlyList<ImportHandover>? handovers = null,
        IReadOnlyList<ImportAuditEvent>? auditEvents = null) =>
        new(accounts ?? [Account(FileBina)], people, links ?? [], marriages ?? [], images ?? [], handovers ?? [], auditEvents ?? [], _files);

    [Fact]
    [Trait("AC", "AC-09.9")]
    public async Task Only_an_administrator_can_import_and_a_refusal_saves_nothing()
    {
        var data = Data([Person("Maya", FileBina)]);

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
            () => Service().ImportAsync(Administrator, Data([Person("Maya", FileBina)]), Cancellation));

        Assert.Single(_people.People);
        Assert.False(_store.Committed);
    }

    [Fact]
    [Trait("AC", "AC-09.8")]
    [Trait("AC", "AC-09.10")]
    [Trait("AC", "AC-09.12")]
    public async Task Rows_keep_their_ids_and_states_and_accounts_are_recreated_or_matched_by_google_id()
    {
        _store.GoogleLogins["google-admin"] = Admin;
        var parent = Person("Hari", FileBina);
        var child = Person("Maya", FileAdmin) with { Visibility = "Private", Gender = "Female" };
        var deleted = new ImportPerson(Guid.NewGuid(), null, null, null, "Public", Now, FileBina, null);
        var link = new ImportLink(Guid.NewGuid(), parent.Id, child.Id, "Adoptive", "Approved", "Disagreed", 1);
        var gone = new ImportLink(Guid.NewGuid(), parent.Id, child.Id, "Biological", "Approved", "Approved", null, Now);
        var marriage = new ImportMarriage(Guid.NewGuid(), deleted.Id, parent.Id, "Pending", "Approved", IsFormer: true);
        var data = Data(
            [parent, child, deleted],
            [link, gone],
            [marriage],
            [new ImportAccount(FileBina, "google-bina", "bina@example.org", true), new ImportAccount(FileAdmin, "google-admin", "old@example.org", false)]);

        var result = await Service().ImportAsync(Administrator, data, Cancellation);

        Assert.Equal(new ImportResult(People: 3, Links: 2, Marriages: 1, Images: 0, Handovers: 0, AuditEvents: 0, CreatedAccounts: 1, ReusedAccounts: 1), result);
        Assert.True(_store.Committed);
        Assert.Equal([new ImportedAccount(new AccountId(FileBina), "google-bina", "bina@example.org", true)], _store.Created);
        var maya = _people.People[new PersonId(child.Id)];
        Assert.Equal((new PersonName("Maya"), ProfileVisibility.Private, Gender.Female, Admin), (maya.Name, maya.Visibility, maya.Gender, maya.MaintainerId));
        Assert.Equal(new AccountId(FileBina), _people.People[new PersonId(parent.Id)].MaintainerId);
        Assert.Equal((PersonName.Erased, Now), (_people.People[new PersonId(deleted.Id)].Name, _people.People[new PersonId(deleted.Id)].DeletedAt));
        var savedLink = _people.Links[new LinkId(link.Id)];
        Assert.Equal(
            (ParentChildType.Adoptive, EndpointApproval.Approved, EndpointApproval.Disagreed, (int?)1),
            (savedLink.Type, savedLink.ParentApproval, savedLink.ChildApproval, savedLink.BirthOrder));
        Assert.Equal(Now, _people.Links[new LinkId(gone.Id)].DeletedAt);
        var savedMarriage = _people.Marriages[new LinkId(marriage.Id)];
        Assert.Equal(EndpointApproval.Pending, savedMarriage.ApprovalOf(new PersonId(deleted.Id)));
        Assert.True(savedMarriage.IsFormer);
        var (auditEvent, occurredAt) = Assert.Single(_people.AuditEvents);
        Assert.Equal((new DataImported(Admin), AuditFamily.Administrative, Now), (auditEvent, auditEvent.Family, occurredAt));
    }

    [Fact]
    [Trait("AC", "AC-09.8")]
    [Trait("AC", "AC-09.12")]
    public async Task Images_handovers_and_the_audit_history_come_back_with_reused_accounts_mapped()
    {
        _store.GoogleLogins["google-admin"] = Admin;
        var imageId = Guid.NewGuid();
        var maya = Person("Maya", FileAdmin) with { ImageId = imageId };
        _files.Files["images/maya.jpeg"] = Jpeg;
        var handover = new ImportHandover(
            Guid.NewGuid(), maya.Id, FileAdmin, "hash", Now.AddDays(-2), Now.AddDays(12), "Accepted", FileBina, Now.AddDays(-1));
        var history = new ImportAuditEvent(
            Guid.NewGuid(), Now.AddDays(-3), "Profile", "PersonCreated", FileAdmin, maya.Id, $$"""{"personId":"{{maya.Id}}","note":"{{FileAdmin}}"}""");
        var data = Data(
            [maya],
            accounts: [Account(FileBina), Account(FileAdmin, "google-admin")],
            images: [new ImportImage(imageId, maya.Id, "images/maya.jpeg")],
            handovers: [handover],
            auditEvents: [history]);

        var result = await Service().ImportAsync(Administrator, data, Cancellation);

        Assert.Equal((1, 1, 1), (result.Images, result.Handovers, result.AuditEvents));
        var image = Assert.Single(_store.Images);
        Assert.Equal((new ProfileImageId(imageId), new PersonId(maya.Id), ImageFormat.Jpeg), (image.Id, image.PersonId, image.Format));
        Assert.Equal(new ProfileImageId(imageId), _people.People[new PersonId(maya.Id)].ImageId);
        var saved = Assert.Single(_handovers.Handovers);
        Assert.Equal(
            (HandoverState.Accepted, Admin, (AccountId?)new AccountId(FileBina), "hash"),
            (saved.State, saved.OfferedById, saved.AcceptedById, saved.TokenHash));
        var restored = Assert.Single(_store.AuditEvents);
        Assert.Equal((history.Id, Admin, maya.Id, AuditFamily.Profile), (restored.Id, restored.ActorId, restored.SubjectId, restored.Family));
        Assert.Equal($$"""{"personId":"{{maya.Id}}","note":"{{Admin.Value}}"}""", restored.Data);
    }

    [Fact]
    [Trait("AC", "AC-09.11")]
    public async Task Unknown_accounts_taken_ids_missing_images_and_bad_history_refuse_the_file()
    {
        var taken = Guid.NewGuid();
        _store.Existing.Add(new AccountId(taken));
        var sita = Person("Sita", Guid.NewGuid());
        var maya = Person("Maya", FileBina) with { ImageId = Guid.NewGuid() };
        var hari = Person("Hari", FileBina);
        var data = Data(
            [sita, maya, hari],
            accounts: [Account(FileBina, "same"), Account(taken, "same")],
            images: [new ImportImage(maya.ImageId!.Value, maya.Id, "images/missing.jpeg"), new ImportImage(Guid.NewGuid(), hari.Id, "images/x.jpeg")],
            handovers: [new ImportHandover(Guid.NewGuid(), hari.Id, Guid.NewGuid(), "h", Now, Now.AddDays(1), "Lost", null, null)],
            auditEvents: [new ImportAuditEvent(Guid.NewGuid(), Now, "Profile", "PersonCreated", FileBina, hari.Id, null)]);

        var refused = await Assert.ThrowsAsync<InvalidImportFileException>(() => Service().ImportAsync(Administrator, data, Cancellation));

        Assert.Equal(
            [
                ("accounts[1]", "import.duplicate_google_id"),
                ("people[0]", "import.unknown_account"),
                ("images[0]", "import.missing_file"),
                ("images[1]", "import.image_not_used"),
                ("images[1]", "import.missing_file"),
                ("handovers[0]", "import.unknown_account"),
                ("handovers[0]", "import.invalid_value"),
                ("auditEvents[0]", "import.invalid_value"),
            ],
            refused.Problems.Select(problem => (problem.Path, problem.Code)));
        Assert.Empty(_store.Created);
        Assert.False(_store.Committed);
    }

    [Fact]
    [Trait("AC", "AC-09.11")]
    public async Task An_account_id_taken_here_by_another_google_login_refuses_the_file()
    {
        var taken = Guid.NewGuid();
        _store.Existing.Add(new AccountId(taken));

        var refused = await Assert.ThrowsAsync<InvalidImportFileException>(
            () => Service().ImportAsync(Administrator, Data([Person("Maya", taken)], accounts: [Account(taken)]), Cancellation));

        Assert.Equal(("accounts[0]", "import.account_exists"), (Assert.Single(refused.Problems).Path, refused.Problems[0].Code));
        Assert.Empty(_store.Created);
    }

    [Fact]
    [Trait("AC", "AC-09.11")]
    public async Task Any_invalid_row_refuses_the_whole_file_and_each_problem_is_listed_with_its_place()
    {
        var parent = Person("Hari", FileBina);
        var child = Person("Maya", FileBina);
        var spouse = Person("Sita", FileBina);
        var data = Data(
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
        var data = Data(Enumerable.Range(0, 60).Select(_ => Person("", FileBina)).ToList());

        var refused = await Assert.ThrowsAsync<InvalidImportFileException>(() => Service().ImportAsync(Administrator, data, Cancellation));

        Assert.Equal(ImportService.MaxProblems, refused.Problems.Count);
        Assert.Equal("people[49]", refused.Problems[^1].Path);
        Assert.True(refused.MoreProblems);
    }

    [Fact]
    [Trait("AC", "AC-09.11")]
    public async Task A_name_part_longer_than_a_profile_allows_is_a_problem()
    {
        var data = Data([Person("Maya", FileBina) with { LastName = new string('x', ImportService.MaxNamePartLength + 1) }]);

        var refused = await Assert.ThrowsAsync<InvalidImportFileException>(() => Service().ImportAsync(Administrator, data, Cancellation));

        Assert.Equal("import.name_too_long", Assert.Single(refused.Problems).Code);
    }

    /// <summary>Google logins and taken IDs as the real store would answer; saved rows are kept to look at.</summary>
    private sealed class FakeImportStore(InMemoryPeople people) : IImportStore, IImportTransaction
    {
        public Dictionary<string, AccountId> GoogleLogins { get; } = [];

        public HashSet<AccountId> Existing { get; } = [];

        public List<ImportedAccount> Created { get; } = [];

        public List<ProfileImage> Images { get; } = [];

        public List<ImportedAuditEvent> AuditEvents { get; } = [];

        public bool Committed { get; private set; }

        public Task<IImportTransaction> BeginAsync(CancellationToken cancellationToken) => Task.FromResult<IImportTransaction>(this);

        public Task<bool> HasPeopleAsync(CancellationToken cancellationToken) => Task.FromResult(people.People.Count > 0);

        public Task<IReadOnlyDictionary<string, AccountId>> FindAccountsByGoogleIdAsync(
            IReadOnlyCollection<string> googleIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, AccountId>>(
                googleIds.Distinct().Where(GoogleLogins.ContainsKey).ToDictionary(id => id, id => GoogleLogins[id]));

        public Task<IReadOnlySet<AccountId>> FindExistingAccountsAsync(
            IReadOnlyCollection<AccountId> ids, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<AccountId>>(ids.Where(Existing.Contains).ToHashSet());

        public Task CreateAccountAsync(ImportedAccount account, CancellationToken cancellationToken)
        {
            Created.Add(account);
            return Task.CompletedTask;
        }

        public Task AddImageAsync(ProfileImage image, CancellationToken cancellationToken)
        {
            Images.Add(image);
            return Task.CompletedTask;
        }

        public Task AddAuditEventsAsync(IReadOnlyList<ImportedAuditEvent> auditEvents, CancellationToken cancellationToken)
        {
            AuditEvents.AddRange(auditEvents);
            return Task.CompletedTask;
        }

        public Task CommitAsync(CancellationToken cancellationToken)
        {
            Committed = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeFiles : IImportFiles
    {
        public Dictionary<string, byte[]> Files { get; } = [];

        public long? LengthOf(string path) => Files.TryGetValue(path, out var content) ? content.Length : null;

        public Task<byte[]> ReadAsync(string path, CancellationToken cancellationToken) => Task.FromResult(Files[path]);
    }

    [Fact]
    [Trait("AC", "AC-09.8")]
    public async Task Description_and_birth_date_come_back_and_profiles_without_them_still_import()
    {
        var maya = Person("Maya", FileBina) with
        {
            Description = "Teacher",
            BirthDate = new ImportBirthDate("Bs", 2082, 6, 16),
            BirthDateShown = true,
        };
        var hari = Person("Hari", FileBina);

        await Service().ImportAsync(Administrator, Data([maya, hari]), Cancellation);

        var imported = _people.People[new PersonId(maya.Id)];
        Assert.Equal(("Teacher", BirthDates.Create(BirthCalendar.Bs, 2082, 6, 16), true), (imported.Description, imported.BirthDate, imported.BirthDateShown));
        Assert.Equal((null, null, false), (_people.People[new PersonId(hari.Id)].Description, _people.People[new PersonId(hari.Id)].BirthDate, _people.People[new PersonId(hari.Id)].BirthDateShown));
    }

    [Fact]
    [Trait("AC", "AC-09.11")]
    public async Task A_birth_date_that_does_not_exist_or_a_long_description_refuses_the_file()
    {
        var data = Data(
            [
                Person("Maya", FileBina) with { BirthDate = new ImportBirthDate("Ad", 1952, 2, 30) },
                Person("Hari", FileBina) with { BirthDate = new ImportBirthDate("Lunar", 1952, null, null) },
                Person("Sita", FileBina) with { Description = new string('a', 201) },
            ]);

        var refused = await Assert.ThrowsAsync<InvalidImportFileException>(() => Service().ImportAsync(Administrator, data, Cancellation));

        Assert.Equal(
            [("people[0]", "person.birth_date_invalid"), ("people[1]", "import.invalid_value"), ("people[2]", "person.description_too_long")],
            refused.Problems.Select(problem => (problem.Path, problem.Code)));
        Assert.Empty(_people.People);
    }
}
