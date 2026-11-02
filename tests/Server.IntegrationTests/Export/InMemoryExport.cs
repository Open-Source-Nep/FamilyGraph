using FamilyGraph.Application.Export;
using FamilyGraph.Business.People;
using Server.IntegrationTests.Accounts;
using Server.IntegrationTests.Handovers;
using Server.IntegrationTests.People;

namespace Server.IntegrationTests.Export;

/// <summary>
/// Export queries over the in-memory accounts, people, links, images and handovers, copied when the
/// snapshot opens. Audit events are kept as objects here, so each row's data is an empty object.
/// </summary>
public sealed class InMemoryExport(InMemoryAccountStore accounts, InMemoryPeople people, InMemoryHandovers handovers) : IExportQueries
{
    public Task<IExportSnapshot> OpenSnapshotAsync(CancellationToken cancellationToken)
    {
        IExportSnapshot snapshot = new Snapshot(
            accounts.Accounts.Values
                .OrderBy(account => account.Id.Value)
                .Select(account => new ExportedAccount(account.Id, accounts.GoogleIdOf(account.Id), account.Email, account.EmailVerified))
                .ToList(),
            people.People.Values
                .OrderBy(person => person.Id.Value)
                .Select(person => new ExportedPerson(
                    person.Id, person.Name, person.Visibility, person.DeletedAt, person.MaintainerId, person.Gender, person.Description,
                    person.BirthDate, person.BirthDateShown, person.ImageId))
                .ToList(),
            people.Links.Values
                .OrderBy(link => link.Id.Value)
                .Select(link => new ExportedLink(
                    link.Id, link.ParentId, link.ChildId, link.Type, link.ParentApproval, link.ChildApproval, link.BirthOrder, link.DeletedAt))
                .ToList(),
            people.Marriages.Values
                .OrderBy(marriage => marriage.Id.Value)
                .Select(marriage => new ExportedMarriage(
                    marriage.Id,
                    marriage.FirstSpouseId,
                    marriage.SecondSpouseId,
                    marriage.FirstApproval,
                    marriage.SecondApproval,
                    marriage.IsFormer,
                    marriage.DeletedAt))
                .ToList(),
            people.Images.Values.OrderBy(image => image.Id.Value).ToList(),
            handovers.All
                .OrderBy(handover => handover.Id.Value)
                .Select(handover => new ExportedHandover(
                    handover.Id,
                    handover.PersonId,
                    handover.OfferedById,
                    handover.TokenHash,
                    handover.CreatedAt,
                    handover.ExpiresAt,
                    handover.State,
                    handover.AcceptedById,
                    handover.ClosedAt))
                .ToList(),
            people.AuditEvents
                .Select(entry => new ExportedAuditEvent(
                    Guid.CreateVersion7(entry.OccurredAt),
                    entry.OccurredAt,
                    entry.Event.Family,
                    entry.Event.GetType().Name,
                    entry.Event.ActorId,
                    entry.Event.ActorId.Value,
                    "{}"))
                .ToList());
        return Task.FromResult(snapshot);
    }

    private sealed class Snapshot(
        List<ExportedAccount> accounts,
        List<ExportedPerson> people,
        List<ExportedLink> links,
        List<ExportedMarriage> marriages,
        List<ProfileImage> images,
        List<ExportedHandover> handovers,
        List<ExportedAuditEvent> auditEvents) : IExportSnapshot
    {
        public IAsyncEnumerable<ExportedAccount> AccountsAsync(CancellationToken cancellationToken) => accounts.ToAsyncEnumerable();

        public IAsyncEnumerable<ExportedPerson> PeopleAsync(CancellationToken cancellationToken) => people.ToAsyncEnumerable();

        public IAsyncEnumerable<ExportedLink> LinksAsync(CancellationToken cancellationToken) => links.ToAsyncEnumerable();

        public IAsyncEnumerable<ExportedMarriage> MarriagesAsync(CancellationToken cancellationToken) => marriages.ToAsyncEnumerable();

        public IAsyncEnumerable<ExportedImage> ImagesAsync(CancellationToken cancellationToken) =>
            images.Select(image => new ExportedImage(image.Id, image.PersonId, image.Format)).ToAsyncEnumerable();

        public IAsyncEnumerable<ExportedImageContent> ImageContentsAsync(CancellationToken cancellationToken) =>
            images.Select(image => new ExportedImageContent(image.Id, image.Format, image.Content)).ToAsyncEnumerable();

        public IAsyncEnumerable<ExportedHandover> HandoversAsync(CancellationToken cancellationToken) => handovers.ToAsyncEnumerable();

        public IAsyncEnumerable<ExportedAuditEvent> AuditEventsAsync(CancellationToken cancellationToken) => auditEvents.ToAsyncEnumerable();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
