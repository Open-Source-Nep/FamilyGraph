using FamilyGraph.Application.Export;
using Server.IntegrationTests.Accounts;
using Server.IntegrationTests.People;

namespace Server.IntegrationTests.Export;

/// <summary>Export queries over the in-memory accounts, people and links, copied when the snapshot opens.</summary>
public sealed class InMemoryExport(InMemoryAccountStore accounts, InMemoryPeople people) : IExportQueries
{
    public Task<IExportSnapshot> OpenSnapshotAsync(CancellationToken cancellationToken)
    {
        var maintainers = people.People.Values.Select(person => person.MaintainerId).ToHashSet();
        IExportSnapshot snapshot = new Snapshot(
            accounts.Accounts.Values
                .Where(account => maintainers.Contains(account.Id))
                .OrderBy(account => account.Id.Value)
                .Select(account => new ExportedAccount(account.Id, account.Email, account.EmailVerified))
                .ToList(),
            people.People.Values
                .OrderBy(person => person.Id.Value)
                .Select(person => new ExportedPerson(person.Id, person.Name, person.Visibility, person.DeletedAt, person.MaintainerId, person.Gender))
                .ToList(),
            people.Links.Values
                .Where(link => !link.IsDeleted)
                .OrderBy(link => link.Id.Value)
                .Select(link => new ExportedLink(
                    link.Id, link.ParentId, link.ChildId, link.Type, link.ParentApproval, link.ChildApproval, link.BirthOrder))
                .ToList());
        return Task.FromResult(snapshot);
    }

    private sealed class Snapshot(
        List<ExportedAccount> accounts, List<ExportedPerson> people, List<ExportedLink> links) : IExportSnapshot
    {
        public IAsyncEnumerable<ExportedAccount> AccountsAsync(CancellationToken cancellationToken) => accounts.ToAsyncEnumerable();

        public IAsyncEnumerable<ExportedPerson> PeopleAsync(CancellationToken cancellationToken) => people.ToAsyncEnumerable();

        public IAsyncEnumerable<ExportedLink> LinksAsync(CancellationToken cancellationToken) => links.ToAsyncEnumerable();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
