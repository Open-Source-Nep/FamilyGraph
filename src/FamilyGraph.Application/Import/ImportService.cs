using FamilyGraph.Application.Claims;
using FamilyGraph.Application.Handovers;
using FamilyGraph.Application.People;
using FamilyGraph.Application.Persistence;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Application.Relationships;
using FamilyGraph.Business;
using FamilyGraph.Business.Audit;
using FamilyGraph.Business.Claims;
using FamilyGraph.Business.Handovers;
using FamilyGraph.Business.Import;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Application.Import;

/// <summary>
/// An administrator's import of an export file into an empty instance (D-033, D-049). Every ID, name,
/// visibility, gender, approval, birth order, deletion, image, handover link, identity claim and audit row is kept as in
/// the file. Each file account is recreated with its ID, unless an account here has the same Google ID,
/// which then takes its place. Any problem refuses the whole file. The import adds one
/// <see cref="DataImported"/> event to the restored history.
/// </summary>
public sealed class ImportService(
    IImportStore store,
    IPersonStore people,
    IParentChildLinkStore links,
    IMarriageStore marriages,
    IHandoverStore handovers,
    IClaimStore claims,
    IAuditLog audit,
    IUnitOfWork unitOfWork,
    TimeProvider time)
{
    /// <summary>How many problems a refused file reports.</summary>
    public const int MaxProblems = 50;

    /// <summary>The longest name part, as for a profile made through the API (D-019).</summary>
    public const int MaxNamePartLength = 100;

    /// <summary>The longest email, as Identity stores it.</summary>
    public const int MaxEmailLength = 256;

    /// <summary>How many audit rows are saved at a time.</summary>
    private const int AuditBatchSize = 1000;

    /// <summary>
    /// Imports the file in one transaction. A non-administrator is 403 <c>import.not_administrator</c>,
    /// an instance with any profile 409 <c>import.instance_not_empty</c>, and a file with any problem
    /// 422 <c>import.invalid_file</c> with the problems; in each case nothing is saved.
    /// </summary>
    public async Task<ImportResult> ImportAsync(Viewer viewer, ImportData data, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        ArgumentNullException.ThrowIfNull(data);

        if (!viewer.IsAdministrator)
        {
            throw new ImportNotAdministratorException();
        }

        await using var transaction = await store.BeginAsync(cancellationToken);
        if (await store.HasPeopleAsync(cancellationToken))
        {
            throw new InstanceNotEmptyException();
        }

        var checker = new ImportCheck(data);
        var rows = checker.Build();
        var accounts = await ResolveAccountsAsync(checker, cancellationToken);
        checker.ThrowIfProblems();

        foreach (var account in accounts.Create)
        {
            await store.CreateAccountAsync(account, cancellationToken);
        }

        foreach (var person in rows.People)
        {
            people.Add(person(accounts.Local));
        }

        foreach (var link in rows.Links)
        {
            links.Add(link);
        }

        foreach (var marriage in rows.Marriages)
        {
            marriages.Add(marriage);
        }

        foreach (var handover in rows.Handovers)
        {
            handovers.Add(handover(accounts.Local));
        }

        foreach (var claim in rows.Claims)
        {
            claims.Add(claim(accounts.Local));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var image in rows.Images)
        {
            var content = await data.Files.ReadAsync(image.File, cancellationToken);
            try
            {
                await store.AddImageAsync(ProfileImage.Import(image.Id, image.PersonId, content), cancellationToken);
            }
            catch (DomainException e)
            {
                throw new InvalidImportFileException([new ImportProblem(image.Path, e.Code, e.Message)], moreProblems: false);
            }
        }

        foreach (var batch in rows.AuditEvents.Chunk(AuditBatchSize))
        {
            await store.AddAuditEventsAsync(batch.Select(row => row(accounts)).ToList(), cancellationToken);
        }

        audit.Add([new DataImported(viewer.AccountId)], time.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ImportResult(
            rows.People.Count,
            rows.Links.Count,
            rows.Marriages.Count,
            rows.Images.Count,
            rows.Handovers.Count,
            rows.AuditEvents.Count,
            accounts.Create.Count,
            accounts.Reused.Count,
            rows.Claims.Count);
    }

    /// <summary>
    /// Matches each file account with an account here by Google ID (D-049); the others are created with
    /// their ID, which must not be taken here already.
    /// </summary>
    private async Task<AccountMap> ResolveAccountsAsync(ImportCheck checker, CancellationToken cancellationToken)
    {
        var byGoogleId = await store.FindAccountsByGoogleIdAsync(
            checker.Accounts.Values.Select(row => row.Account.GoogleId).OfType<string>().ToList(), cancellationToken);
        var reused = new Dictionary<Guid, AccountId>();
        var create = new List<(string Path, ImportedAccount Account)>();
        foreach (var (id, (path, account)) in checker.Accounts)
        {
            if (account.GoogleId is { } googleId && byGoogleId.TryGetValue(googleId, out var local))
            {
                reused[id] = local;
            }
            else
            {
                create.Add((path, account));
            }
        }

        var taken = await store.FindExistingAccountsAsync(create.Select(row => row.Account.Id).ToList(), cancellationToken);
        foreach (var (path, account) in create.Where(row => taken.Contains(row.Account.Id)))
        {
            checker.Add(path, "import.account_exists", $"Account {account.Id.Value} already exists here with another Google login.");
        }

        return new AccountMap(reused, create.Select(row => row.Account).ToList());
    }

    /// <summary>Where each file account's references go here: the reused account, or the same ID.</summary>
    private sealed class AccountMap(Dictionary<Guid, AccountId> reused, List<ImportedAccount> create)
    {
        public IReadOnlyDictionary<Guid, AccountId> Reused => reused;

        public List<ImportedAccount> Create => create;

        public AccountId Local(Guid fileId) => reused.GetValueOrDefault(fileId, new AccountId(fileId));

        /// <summary>The event's data with each reused account's ID replaced; IDs are written in one form (D-015).</summary>
        public string MapData(string data)
        {
            foreach (var (from, to) in reused)
            {
                data = data.Replace(from.ToString(), to.Value.ToString(), StringComparison.OrdinalIgnoreCase);
            }

            return data;
        }
    }

    private sealed record ImageRow(ProfileImageId Id, PersonId PersonId, string File, string Path);

    private sealed record Rows(
        List<Func<Func<Guid, AccountId>, Person>> People,
        List<ParentChildLink> Links,
        List<Marriage> Marriages,
        List<ImageRow> Images,
        List<Func<Func<Guid, AccountId>, ProfileHandover>> Handovers,
        List<Func<Func<Guid, AccountId>, ProfileClaim>> Claims,
        List<Func<AccountMap, ImportedAuditEvent>> AuditEvents);

    /// <summary>Checks the file's rows in order, collecting problems, and builds the entities from the valid ones.</summary>
    private sealed class ImportCheck(ImportData data)
    {
        private readonly List<ImportProblem> _problems = [];
        private int _problemCount;

        /// <summary>Each valid file account, by its ID in the file, with its place.</summary>
        public Dictionary<Guid, (string Path, ImportedAccount Account)> Accounts { get; } = [];

        public Rows Build()
        {
            CheckAccounts();
            var (people, imageOf) = BuildPeople();
            var personIds = people.Select(row => row.Id.Value).ToHashSet();
            var deletedIds = data.People.Where(person => person.DeletedAt is not null).Select(person => person.Id).ToHashSet();
            var links = BuildLinks(personIds);
            var current = links.Where(link => link.DeletedAt is null).ToList();
            var marriages = BuildMarriages(
                personIds, current.Select(link => Ordered(link.ParentId.Value, link.ChildId.Value)).ToHashSet());
            if (LinkCycleCheck.HasCycle(current.Select(link => (link.ParentId, link.ChildId))))
            {
                Add("links", "import.link_cycle", "The links make someone their own ancestor.");
            }

            var images = BuildImages(personIds, deletedIds, imageOf);
            var handovers = BuildHandovers(personIds);
            var claims = BuildClaims(personIds);
            var auditEvents = BuildAuditEvents();
            return new Rows(people.Select(row => row.Create).ToList(), links, marriages, images, handovers, claims, auditEvents);
        }

        public void ThrowIfProblems()
        {
            if (_problemCount > 0)
            {
                throw new InvalidImportFileException(_problems, _problemCount > _problems.Count);
            }
        }

        public void Add(string path, string code, string message)
        {
            _problemCount++;
            if (_problems.Count < MaxProblems)
            {
                _problems.Add(new ImportProblem(path, code, message));
            }
        }

        private void CheckAccounts()
        {
            var googleIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < data.Accounts.Count; i++)
            {
                var account = data.Accounts[i];
                var path = $"accounts[{i}]";
                var problems = _problemCount;
                if (account.Id == Guid.Empty || Accounts.ContainsKey(account.Id))
                {
                    Add(path, "import.duplicate_id", $"Account {account.Id} has no ID or appears more than once.");
                }

                var googleId = string.IsNullOrWhiteSpace(account.GoogleId) ? null : account.GoogleId.Trim();
                if (googleId is not null && !googleIds.Add(googleId))
                {
                    Add(path, "import.duplicate_google_id", "Another account in the file has the same Google ID.");
                }

                var email = account.Email?.Trim() ?? "";
                if (email.Length > MaxEmailLength)
                {
                    Add(path, "import.email_too_long", $"The email is longer than {MaxEmailLength} characters.");
                }

                if (_problemCount == problems)
                {
                    Accounts[account.Id] = (path, new ImportedAccount(new AccountId(account.Id), googleId, email, account.EmailVerified));
                }
            }
        }

        private void RequireAccount(Guid id, string path, string role)
        {
            if (!data.Accounts.Any(account => account.Id == id))
            {
                Add(path, "import.unknown_account", $"The {role} {id} is not an account in the file.");
            }
        }

        private (List<(PersonId Id, Func<Func<Guid, AccountId>, Person> Create)> People, Dictionary<Guid, Guid> ImageOf) BuildPeople()
        {
            var result = new List<(PersonId, Func<Func<Guid, AccountId>, Person>)>();
            var imageOf = new Dictionary<Guid, Guid>();
            var seen = new HashSet<Guid>();
            var claimants = new HashSet<Guid>();
            for (var i = 0; i < data.People.Count; i++)
            {
                var person = data.People[i];
                var path = $"people[{i}]";
                var problems = _problemCount;
                if (person.Id == Guid.Empty || !seen.Add(person.Id))
                {
                    Add(path, "import.duplicate_id", $"Person {person.Id} has no ID or appears more than once.");
                }

                RequireAccount(person.MaintainerId, path, "maintainer");
                var visibility = Parse<ProfileVisibility>(person.Visibility, path, "visibility");
                var gender = person.Gender is null ? null : Parse<Gender>(person.Gender, path, "gender");
                PersonName? name = PersonName.Erased;
                string? description = null;
                BirthDate? birthDate = null;
                if (person.DeletedAt is null)
                {
                    name = Name(person, path);
                    description = Try(path, () => PersonDescription.Normalize(person.Description));
                    birthDate = person.BirthDate is null ? null : BirthDateOf(person.BirthDate, path);
                }
                else if (person.ImageId is not null)
                {
                    Add(path, "import.deleted_image", "A deleted profile has no image.");
                }

                // A claimed person is their profile's maintainer, and an account is one claimed person (D-051).
                if (person.ClaimedById is { } claimedBy
                    && (person.DeletedAt is not null || claimedBy != person.MaintainerId || !claimants.Add(claimedBy)))
                {
                    Add(path, "import.invalid_claim", "A claimed profile is not deleted, is maintained by its claimant, and is the claimant's only one.");
                }

                if (_problemCount == problems)
                {
                    var (id, deletedAt, maintainerId, shown) = (new PersonId(person.Id), person.DeletedAt, person.MaintainerId, person.BirthDateShown);
                    var imageId = person.ImageId is { } image ? new ProfileImageId(image) : (ProfileImageId?)null;
                    if (person.ImageId is { } referenced)
                    {
                        imageOf[person.Id] = referenced;
                    }

                    var claimedById = person.ClaimedById;
                    result.Add((id, local => Person.Import(
                        id, name!, visibility!, deletedAt, local(maintainerId), gender, description, birthDate, shown, imageId,
                        claimedById is { } claimant ? local(claimant) : null)));
                }
            }

            return (result, imageOf);
        }

        private BirthDate? BirthDateOf(ImportBirthDate date, string path) =>
            Parse<BirthCalendar>(date.Calendar, path, "birth date calendar") is { } calendar
                ? Try(path, () => BirthDates.Create(calendar, date.Year, date.Month, date.Day))
                : null;

        private PersonName? Name(ImportPerson person, string path)
        {
            foreach (var (part, value) in new[] { ("firstName", person.FirstName), ("middleName", person.MiddleName), ("lastName", person.LastName) })
            {
                if (value?.Trim().Length > MaxNamePartLength)
                {
                    Add(path, "import.name_too_long", $"The {part} is longer than {MaxNamePartLength} characters.");
                    return null;
                }
            }

            return Try(path, () => new PersonName(person.FirstName ?? "", person.MiddleName, person.LastName));
        }

        private List<ParentChildLink> BuildLinks(HashSet<Guid> personIds)
        {
            var result = new List<ParentChildLink>();
            var seen = new HashSet<Guid>();
            var pairs = new HashSet<(Guid, Guid)>();
            for (var i = 0; i < data.Links.Count; i++)
            {
                var link = data.Links[i];
                var path = $"links[{i}]";
                var problems = _problemCount;
                if (link.Id == Guid.Empty || !seen.Add(link.Id))
                {
                    Add(path, "import.duplicate_id", $"Link {link.Id} has no ID or appears more than once.");
                }

                RequirePerson(personIds, link.ParentId, path, "parent");
                RequirePerson(personIds, link.ChildId, path, "child");

                // Deleted links stay in the history; only one current link per pair (PRD 10).
                if (link.DeletedAt is null && !pairs.Add((link.ParentId, link.ChildId)))
                {
                    Add(path, "import.duplicate_link", "This parent and child are already linked earlier in the file.");
                }

                var type = Parse<ParentChildType>(link.Type, path, "type");
                var parentApproval = Parse<EndpointApproval>(link.ParentApproval, path, "parentApproval");
                var childApproval = Parse<EndpointApproval>(link.ChildApproval, path, "childApproval");
                if (_problemCount == problems
                    && Try(path, () => ParentChildLink.Import(
                        new LinkId(link.Id),
                        new PersonId(link.ParentId),
                        new PersonId(link.ChildId),
                        type!,
                        parentApproval!,
                        childApproval!,
                        link.BirthOrder,
                        link.DeletedAt)) is { } created)
                {
                    result.Add(created);
                }
            }

            return result;
        }

        /// <param name="linkedPairs">Each currently linked parent and child, in ID order: spouses cannot also be parent and child (D-031).</param>
        private List<Marriage> BuildMarriages(HashSet<Guid> personIds, HashSet<(Guid, Guid)> linkedPairs)
        {
            var result = new List<Marriage>();
            var seen = new HashSet<Guid>();
            var pairs = new HashSet<(Guid, Guid)>();
            for (var i = 0; i < data.Marriages.Count; i++)
            {
                var marriage = data.Marriages[i];
                var path = $"marriages[{i}]";
                var problems = _problemCount;
                if (marriage.Id == Guid.Empty || !seen.Add(marriage.Id))
                {
                    Add(path, "import.duplicate_id", $"Marriage {marriage.Id} has no ID or appears more than once.");
                }

                RequirePerson(personIds, marriage.FirstSpouseId, path, "first spouse");
                RequirePerson(personIds, marriage.SecondSpouseId, path, "second spouse");
                var firstApproval = Parse<EndpointApproval>(marriage.FirstApproval, path, "firstApproval");
                var secondApproval = Parse<EndpointApproval>(marriage.SecondApproval, path, "secondApproval");
                if (_problemCount == problems
                    && Try(path, () => Marriage.Import(
                        new LinkId(marriage.Id),
                        new PersonId(marriage.FirstSpouseId),
                        new PersonId(marriage.SecondSpouseId),
                        firstApproval!,
                        secondApproval!,
                        marriage.IsFormer,
                        marriage.DeletedAt)) is { } created)
                {
                    var pair = (created.FirstSpouseId.Value, created.SecondSpouseId.Value);
                    if (created.DeletedAt is not null)
                    {
                        result.Add(created);
                    }
                    else if (!pairs.Add(pair))
                    {
                        Add(path, "import.duplicate_marriage", "These two people are already married earlier in the file.");
                    }
                    else if (linkedPairs.Contains(pair))
                    {
                        Add(path, "import.parent_child_marriage", "One of these two people is the other's parent.");
                    }
                    else
                    {
                        result.Add(created);
                    }
                }
            }

            return result;
        }

        /// <param name="imageOf">Each profile's image ID; every image must be one profile's current image (D-028).</param>
        private List<ImageRow> BuildImages(HashSet<Guid> personIds, HashSet<Guid> deletedIds, Dictionary<Guid, Guid> imageOf)
        {
            var result = new List<ImageRow>();
            var seen = new HashSet<Guid>();
            for (var i = 0; i < data.Images.Count; i++)
            {
                var image = data.Images[i];
                var path = $"images[{i}]";
                var problems = _problemCount;
                if (image.Id == Guid.Empty || !seen.Add(image.Id))
                {
                    Add(path, "import.duplicate_id", $"Image {image.Id} has no ID or appears more than once.");
                }

                RequirePerson(personIds, image.PersonId, path, "person");
                if (imageOf.GetValueOrDefault(image.PersonId) != image.Id || deletedIds.Contains(image.PersonId))
                {
                    Add(path, "import.image_not_used", "The image is not its person's current image.");
                }

                var length = image.File is null ? null : data.Files.LengthOf(image.File);
                if (length is null)
                {
                    Add(path, "import.missing_file", $"The image file \"{image.File}\" is not in the export.");
                }
                else if (length > ProfileImage.MaxBytes)
                {
                    Add(path, "profile_image.too_large", $"A profile image can be at most {ProfileImage.MaxBytes} bytes.");
                }

                if (_problemCount == problems)
                {
                    result.Add(new ImageRow(new ProfileImageId(image.Id), new PersonId(image.PersonId), image.File!, path));
                }
            }

            foreach (var (personId, imageId) in imageOf)
            {
                if (!seen.Contains(imageId))
                {
                    var index = data.People.ToList().FindIndex(person => person.Id == personId);
                    Add($"people[{index}]", "import.unknown_image", $"The image {imageId} is not in the file.");
                }
            }

            return result;
        }

        private List<Func<Func<Guid, AccountId>, ProfileHandover>> BuildHandovers(HashSet<Guid> personIds)
        {
            var result = new List<Func<Func<Guid, AccountId>, ProfileHandover>>();
            var seen = new HashSet<Guid>();
            var hashes = new HashSet<string>(StringComparer.Ordinal);
            var open = new HashSet<Guid>();
            for (var i = 0; i < data.Handovers.Count; i++)
            {
                var handover = data.Handovers[i];
                var path = $"handovers[{i}]";
                var problems = _problemCount;
                if (handover.Id == Guid.Empty || !seen.Add(handover.Id))
                {
                    Add(path, "import.duplicate_id", $"Handover {handover.Id} has no ID or appears more than once.");
                }

                RequirePerson(personIds, handover.PersonId, path, "person");
                RequireAccount(handover.OfferedById, path, "offering account");
                if (handover.AcceptedById is { } accepted)
                {
                    RequireAccount(accepted, path, "accepting account");
                }

                if (string.IsNullOrWhiteSpace(handover.TokenHash) || handover.TokenHash.Length > 64 || !hashes.Add(handover.TokenHash))
                {
                    Add(path, "import.invalid_value", "The token hash is missing, too long or used twice.");
                }

                var state = Parse<HandoverState>(handover.State, path, "state");
                if (state == HandoverState.Open && !open.Add(handover.PersonId))
                {
                    Add(path, "import.duplicate_handover", "The profile already has an open handover link earlier in the file.");
                }

                if (_problemCount == problems)
                {
                    var row = handover;
                    result.Add(local => ProfileHandover.Import(
                        new HandoverId(row.Id),
                        new PersonId(row.PersonId),
                        local(row.OfferedById),
                        row.TokenHash!,
                        row.CreatedAt,
                        row.ExpiresAt,
                        state!,
                        row.AcceptedById is { } acceptedBy ? local(acceptedBy) : null,
                        row.ClosedAt));
                }
            }

            return result;
        }

        private List<Func<Func<Guid, AccountId>, ProfileClaim>> BuildClaims(HashSet<Guid> personIds)
        {
            var result = new List<Func<Func<Guid, AccountId>, ProfileClaim>>();
            var source = data.Claims ?? [];
            var seen = new HashSet<Guid>();
            var open = new HashSet<Guid>();
            for (var i = 0; i < source.Count; i++)
            {
                var claim = source[i];
                var path = $"claims[{i}]";
                var problems = _problemCount;
                if (claim.Id == Guid.Empty || !seen.Add(claim.Id))
                {
                    Add(path, "import.duplicate_id", $"Claim {claim.Id} has no ID or appears more than once.");
                }

                RequirePerson(personIds, claim.PersonId, path, "person");
                RequireAccount(claim.ClaimantId, path, "claimant");
                if (claim.ClosedById is { } closedBy)
                {
                    RequireAccount(closedBy, path, "closing account");
                }

                var state = Parse<ClaimState>(claim.State, path, "state");
                if (state == ClaimState.Open && !open.Add(claim.ClaimantId))
                {
                    Add(path, "import.duplicate_claim", "The claimant already has an open claim earlier in the file.");
                }

                var evidence = Try(path, () => ClaimEvidence.Normalize(claim.Evidence) ?? "");
                if (_problemCount == problems)
                {
                    var row = claim;
                    result.Add(local => ProfileClaim.Import(
                        new ClaimId(row.Id),
                        new PersonId(row.PersonId),
                        local(row.ClaimantId),
                        evidence,
                        row.SubmittedAt,
                        state!,
                        row.ClosedById is { } closedById ? local(closedById) : null,
                        row.ClosedAt));
                }
            }

            return result;
        }

        private List<Func<AccountMap, ImportedAuditEvent>> BuildAuditEvents()
        {
            var result = new List<Func<AccountMap, ImportedAuditEvent>>();
            var seen = new HashSet<Guid>();
            for (var i = 0; i < data.AuditEvents.Count; i++)
            {
                var row = data.AuditEvents[i];
                var path = $"auditEvents[{i}]";
                var problems = _problemCount;
                if (row.Id == Guid.Empty || !seen.Add(row.Id))
                {
                    Add(path, "import.duplicate_id", $"Audit event {row.Id} has no ID or appears more than once.");
                }

                var family = Parse<AuditFamily>(row.Family, path, "family");
                if (string.IsNullOrWhiteSpace(row.Type) || row.Type.Length > 100)
                {
                    Add(path, "import.invalid_value", "The audit event has no type or one longer than 100 characters.");
                }

                if (row.Data is null)
                {
                    Add(path, "import.invalid_value", "The audit event's data is not a JSON object.");
                }

                if (_problemCount == problems)
                {
                    result.Add(map => new ImportedAuditEvent(
                        row.Id,
                        row.OccurredAt,
                        family!,
                        row.Type!,
                        map.Local(row.ActorId),
                        map.Reused.TryGetValue(row.SubjectId, out var subject) ? subject.Value : row.SubjectId,
                        map.MapData(row.Data!)));
                }
            }

            return result;
        }

        private static (Guid, Guid) Ordered(Guid one, Guid other) => one.CompareTo(other) < 0 ? (one, other) : (other, one);

        private void RequirePerson(HashSet<Guid> personIds, Guid id, string path, string role)
        {
            if (!personIds.Contains(id))
            {
                Add(path, "import.unknown_person", $"The {role} {id} is not a valid person in the file.");
            }
        }

        private TEnum? Parse<TEnum>(string? name, string path, string field)
            where TEnum : BaseEnum<TEnum>
        {
            var value = BaseEnum<TEnum>.All.SingleOrDefault(item => item.Name == name);
            if (value is null)
            {
                Add(path, "import.invalid_value", $"The {field} \"{name}\" is not one of {string.Join(", ", BaseEnum<TEnum>.All)}.");
            }

            return value;
        }

        /// <summary>Runs a domain rule and turns its refusal into a problem.</summary>
        private T? Try<T>(string path, Func<T> create)
            where T : class
        {
            try
            {
                return create();
            }
            catch (DomainException e)
            {
                Add(path, e.Code, e.Message);
                return null;
            }
        }
    }
}
