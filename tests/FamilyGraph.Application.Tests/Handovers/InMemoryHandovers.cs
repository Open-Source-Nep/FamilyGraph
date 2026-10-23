using FamilyGraph.Application.Handovers;
using FamilyGraph.Business.Handovers;
using FamilyGraph.Business.People;

namespace FamilyGraph.Application.Tests.Handovers;

/// <summary>Handover links over an in-memory list. Added links are kept at once.</summary>
internal sealed class InMemoryHandovers : IHandoverStore
{
    public List<ProfileHandover> Handovers { get; } = [];

    public Task<ProfileHandover?> FindOpenAsync(PersonId personId, CancellationToken cancellationToken) =>
        Task.FromResult(Handovers.SingleOrDefault(handover => handover.PersonId == personId && handover.State == HandoverState.Open));

    public Task<ProfileHandover?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(Handovers.SingleOrDefault(handover => handover.TokenHash == tokenHash));

    public void Add(ProfileHandover handover) => Handovers.Add(handover);
}
