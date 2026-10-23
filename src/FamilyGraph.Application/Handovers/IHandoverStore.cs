using FamilyGraph.Business.Handovers;
using FamilyGraph.Business.People;

namespace FamilyGraph.Application.Handovers;

/// <summary>Loads and adds <see cref="ProfileHandover"/> aggregates (D-002).</summary>
public interface IHandoverStore
{
    /// <summary>The profile's open handover link, expired or not, or <see langword="null"/>. There is at most one.</summary>
    Task<ProfileHandover?> FindOpenAsync(PersonId personId, CancellationToken cancellationToken);

    /// <summary>The handover link with this token hash, in any state, or <see langword="null"/>.</summary>
    Task<ProfileHandover?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    void Add(ProfileHandover handover);
}
