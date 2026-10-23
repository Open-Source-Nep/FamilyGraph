using System.Collections.Concurrent;
using FamilyGraph.Application.Handovers;
using FamilyGraph.Business.Handovers;
using FamilyGraph.Business.People;

namespace Server.IntegrationTests.Handovers;

/// <summary>Handover links in memory. Added links are kept at once.</summary>
public sealed class InMemoryHandovers : IHandoverStore
{
    private readonly ConcurrentDictionary<HandoverId, ProfileHandover> _handovers = [];

    public Task<ProfileHandover?> FindOpenAsync(PersonId personId, CancellationToken cancellationToken) =>
        Task.FromResult(_handovers.Values.SingleOrDefault(handover => handover.PersonId == personId && handover.State == HandoverState.Open));

    public Task<ProfileHandover?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(_handovers.Values.SingleOrDefault(handover => handover.TokenHash == tokenHash));

    public void Add(ProfileHandover handover) => _handovers[handover.Id] = handover;
}
