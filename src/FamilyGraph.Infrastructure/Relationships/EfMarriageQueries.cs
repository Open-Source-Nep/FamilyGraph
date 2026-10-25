using FamilyGraph.Application.Privacy;
using FamilyGraph.Application.Relationships;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;
using FamilyGraph.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyGraph.Infrastructure.Relationships;

/// <summary>
/// Hidden people sort after the others, by ID only, as in <see cref="EfRelationshipQueries"/> (D-025).
/// </summary>
internal sealed class EfMarriageQueries(FamilyGraphDbContext context) : IMarriageQueries
{
    public async Task<SpouseListData> ListSpousesAsync(
        PersonId personId, Viewer viewer, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        var viewerId = viewer.AccountId;
        var seesAll = viewer.IsAdministrator;

        // One extra row tells whether the list was cut short.
        var rows = await (
                from marriage in context.Set<Marriage>().AsNoTracking()
                where marriage.DeletedAt == null
                    && marriage.FirstApproval == EndpointApproval.Approved
                    && marriage.SecondApproval == EndpointApproval.Approved
                    && (marriage.FirstSpouseId == personId || marriage.SecondSpouseId == personId)
                join spouse in context.Set<Person>()
                    on (marriage.FirstSpouseId == personId ? marriage.SecondSpouseId : marriage.FirstSpouseId) equals spouse.Id
                let hidden = spouse.DeletedAt != null
                    || (!seesAll && spouse.Visibility == ProfileVisibility.Private && spouse.MaintainerId != viewerId)
                orderby marriage.IsFormer, hidden, hidden ? "" : spouse.Name.FirstName, hidden ? "" : spouse.Name.MiddleName,
                    hidden ? "" : spouse.Name.LastName, spouse.Id
                select new SpouseData(
                    marriage.Id,
                    new PersonRecord(spouse.Id, spouse.Name, spouse.Visibility, spouse.DeletedAt != null, spouse.MaintainerId == viewerId, spouse.ImageId, spouse.Gender, spouse.Description, spouse.BirthDate, spouse.BirthDateShown),
                    marriage.IsFormer))
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        return new SpouseListData(rows.Count > limit ? rows[..limit] : rows, rows.Count > limit);
    }

    public async Task<MarriageConsentListData> ListInactiveMarriagesAsync(
        Viewer viewer, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        var viewerId = viewer.AccountId;
        var seesAll = viewer.IsAdministrator;

        var rows = await (
                from marriage in context.Set<Marriage>().AsNoTracking()
                where marriage.DeletedAt == null
                    && !(marriage.FirstApproval == EndpointApproval.Approved && marriage.SecondApproval == EndpointApproval.Approved)
                join first in context.Set<Person>() on marriage.FirstSpouseId equals first.Id
                join second in context.Set<Person>() on marriage.SecondSpouseId equals second.Id
                let ownsFirst = first.MaintainerId == viewerId && first.DeletedAt == null
                let ownsSecond = second.MaintainerId == viewerId && second.DeletedAt == null
                where ownsFirst || ownsSecond
                let awaitingViewer = (ownsFirst && marriage.FirstApproval == EndpointApproval.Pending)
                    || (ownsSecond && marriage.SecondApproval == EndpointApproval.Pending)
                let firstHidden = first.DeletedAt != null
                    || (!seesAll && first.Visibility == ProfileVisibility.Private && first.MaintainerId != viewerId)
                let secondHidden = second.DeletedAt != null
                    || (!seesAll && second.Visibility == ProfileVisibility.Private && second.MaintainerId != viewerId)
                orderby awaitingViewer descending, firstHidden, firstHidden ? "" : first.Name.FirstName,
                    firstHidden ? "" : first.Name.MiddleName, firstHidden ? "" : first.Name.LastName,
                    secondHidden, secondHidden ? "" : second.Name.FirstName, secondHidden ? "" : second.Name.MiddleName,
                    secondHidden ? "" : second.Name.LastName, marriage.Id
                select new MarriageConsentData(
                    marriage.Id,
                    new LinkEndpointData(
                        new PersonRecord(first.Id, first.Name, first.Visibility, first.DeletedAt != null, first.MaintainerId == viewerId, first.ImageId, first.Gender, first.Description, first.BirthDate, first.BirthDateShown),
                        marriage.FirstApproval),
                    new LinkEndpointData(
                        new PersonRecord(second.Id, second.Name, second.Visibility, second.DeletedAt != null, second.MaintainerId == viewerId, second.ImageId, second.Gender, second.Description, second.BirthDate, second.BirthDateShown),
                        marriage.SecondApproval),
                    marriage.IsFormer,
                    false))
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        return new MarriageConsentListData(rows.Count > limit ? rows[..limit] : rows, rows.Count > limit);
    }

    public async Task<MarriageConsentListData> ListPendingMarriagesAsync(
        Viewer viewer, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        var viewerId = viewer.AccountId;

        var rows = await (
                from marriage in context.Set<Marriage>().AsNoTracking()
                where marriage.DeletedAt == null
                    && (marriage.FirstApproval == EndpointApproval.Pending || marriage.SecondApproval == EndpointApproval.Pending)
                join first in context.Set<Person>() on marriage.FirstSpouseId equals first.Id
                join second in context.Set<Person>() on marriage.SecondSpouseId equals second.Id
                where (marriage.FirstApproval == EndpointApproval.Pending && first.DeletedAt == null && first.MaintainerId != viewerId)
                    || (marriage.SecondApproval == EndpointApproval.Pending && second.DeletedAt == null && second.MaintainerId != viewerId)
                let firstHidden = first.DeletedAt != null
                let secondHidden = second.DeletedAt != null
                orderby firstHidden, firstHidden ? "" : first.Name.FirstName,
                    firstHidden ? "" : first.Name.MiddleName, firstHidden ? "" : first.Name.LastName,
                    secondHidden, secondHidden ? "" : second.Name.FirstName, secondHidden ? "" : second.Name.MiddleName,
                    secondHidden ? "" : second.Name.LastName, marriage.Id
                select new MarriageConsentData(
                    marriage.Id,
                    new LinkEndpointData(
                        new PersonRecord(first.Id, first.Name, first.Visibility, first.DeletedAt != null, first.MaintainerId == viewerId, first.ImageId, first.Gender, first.Description, first.BirthDate, first.BirthDateShown),
                        marriage.FirstApproval),
                    new LinkEndpointData(
                        new PersonRecord(second.Id, second.Name, second.Visibility, second.DeletedAt != null, second.MaintainerId == viewerId, second.ImageId, second.Gender, second.Description, second.BirthDate, second.BirthDateShown),
                        marriage.SecondApproval),
                    marriage.IsFormer,
                    false))
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        return new MarriageConsentListData(rows.Count > limit ? rows[..limit] : rows, rows.Count > limit);
    }
}
