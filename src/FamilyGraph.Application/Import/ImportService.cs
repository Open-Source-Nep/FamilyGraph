using FamilyGraph.Application.People;
using FamilyGraph.Application.Persistence;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Application.Relationships;
using FamilyGraph.Business;
using FamilyGraph.Business.Import;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Application.Import;

/// <summary>
/// An administrator's import of an export file into an empty instance (D-033). Every ID, name,
/// visibility, gender, approval, birth order and former flag is kept as in the file. A maintainer is
/// the account here with the file account's verified email; when there is none, the importing
/// administrator becomes the maintainer. Any problem refuses the whole file. The import records one
/// <see cref="DataImported"/> event and nothing per row.
/// </summary>
public sealed class ImportService(
    IImportStore store,
    IPersonStore people,
    IParentChildLinkStore links,
    IMarriageStore marriages,
    IAuditLog audit,
    IUnitOfWork unitOfWork,
    TimeProvider time)
{
    /// <summary>How many problems a refused file reports.</summary>
    public const int MaxProblems = 50;

    /// <summary>The longest name part, as for a profile made through the API (D-019).</summary>
    public const int MaxNamePartLength = 100;

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
        var (newPeople, newLinks, newMarriages) = checker.Build();
        checker.ThrowIfProblems();

        var emails = checker.AccountEmails.Values.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var accounts = await store.FindAccountsByVerifiedEmailAsync(emails, cancellationToken);
        var matched = new Dictionary<Guid, AccountId>();
        foreach (var (fileId, email) in checker.AccountEmails)
        {
            if (accounts.TryGetValue(email, out var accountId))
            {
                matched[fileId] = accountId;
            }
        }

        var assignedToImporter = 0;
        foreach (var (person, fileMaintainerId) in newPeople)
        {
            var maintainerId = matched.GetValueOrDefault(fileMaintainerId);
            if (maintainerId == default)
            {
                maintainerId = viewer.AccountId;
                assignedToImporter++;
            }

            people.Add(person(maintainerId));
        }

        foreach (var link in newLinks)
        {
            links.Add(link);
        }

        foreach (var marriage in newMarriages)
        {
            marriages.Add(marriage);
        }

        audit.Add([new DataImported(viewer.AccountId)], time.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ImportResult(newPeople.Count, newLinks.Count, newMarriages.Count, matched.Count, assignedToImporter);
    }

    /// <summary>Checks the file's rows in order, collecting problems, and builds the entities from the valid ones.</summary>
    private sealed class ImportCheck(ImportData data)
    {
        private readonly List<ImportProblem> _problems = [];
        private int _problemCount;

        /// <summary>Each file account's trimmed email, by its ID in the file.</summary>
        public Dictionary<Guid, string> AccountEmails { get; } = [];

        public (List<(Func<AccountId, Person> Person, Guid FileMaintainerId)> People, List<ParentChildLink> Links, List<Marriage> Marriages) Build()
        {
            CheckAccounts();
            var people = BuildPeople();
            var personIds = people.Select(row => row.Id.Value).ToHashSet();
            var links = BuildLinks(personIds);
            var marriages = BuildMarriages(personIds, links.Select(link => Ordered(link.ParentId.Value, link.ChildId.Value)).ToHashSet());
            if (LinkCycleCheck.HasCycle(links.Select(link => (link.ParentId, link.ChildId))))
            {
                Add("links", "import.link_cycle", "The links make someone their own ancestor.");
            }

            return (people.Select(row => (row.Create, row.MaintainerId)).ToList(), links, marriages);
        }

        public void ThrowIfProblems()
        {
            if (_problemCount > 0)
            {
                throw new InvalidImportFileException(_problems, _problemCount > _problems.Count);
            }
        }

        private void CheckAccounts()
        {
            for (var i = 0; i < data.Accounts.Count; i++)
            {
                var account = data.Accounts[i];
                var path = $"accounts[{i}]";
                if (!AccountEmails.TryAdd(account.Id, account.Email?.Trim() ?? ""))
                {
                    Add(path, "import.duplicate_id", $"Account {account.Id} appears more than once.");
                }
            }
        }

        private List<(PersonId Id, Guid MaintainerId, Func<AccountId, Person> Create)> BuildPeople()
        {
            var result = new List<(PersonId, Guid, Func<AccountId, Person>)>();
            var seen = new HashSet<Guid>();
            for (var i = 0; i < data.People.Count; i++)
            {
                var person = data.People[i];
                var path = $"people[{i}]";
                var problems = _problemCount;
                if (person.Id == Guid.Empty || !seen.Add(person.Id))
                {
                    Add(path, "import.duplicate_id", $"Person {person.Id} has no ID or appears more than once.");
                }

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

                if (_problemCount == problems)
                {
                    var (id, deletedAt, maintainerId, shown) = (new PersonId(person.Id), person.DeletedAt, person.MaintainerId, person.BirthDateShown);
                    result.Add((id, maintainerId, account =>
                        Person.Import(id, name!, visibility!, deletedAt, account, gender, description, birthDate, shown)));
                }
            }

            return result;
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
                if (!pairs.Add((link.ParentId, link.ChildId)))
                {
                    Add(path, "import.duplicate_link", "This parent and child are already linked earlier in the file.");
                }

                var type = Parse<ParentChildType>(link.Type, path, "type");
                var parentApproval = Parse<EndpointApproval>(link.ParentApproval, path, "parentApproval");
                var childApproval = Parse<EndpointApproval>(link.ChildApproval, path, "childApproval");
                if (_problemCount == problems
                    && Try(path, () => ParentChildLink.Import(
                        new LinkId(link.Id), new PersonId(link.ParentId), new PersonId(link.ChildId), type!, parentApproval!, childApproval!, link.BirthOrder)) is { } created)
                {
                    result.Add(created);
                }
            }

            return result;
        }

        /// <param name="linkedPairs">Each linked parent and child, in ID order: spouses cannot also be parent and child (D-031).</param>
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
                        firstApproval!, secondApproval!, marriage.IsFormer)) is { } created)
                {
                    var pair = (created.FirstSpouseId.Value, created.SecondSpouseId.Value);
                    if (!pairs.Add(pair))
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

        private void Add(string path, string code, string message)
        {
            _problemCount++;
            if (_problems.Count < MaxProblems)
            {
                _problems.Add(new ImportProblem(path, code, message));
            }
        }
    }
}
