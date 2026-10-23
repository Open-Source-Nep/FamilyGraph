using FamilyGraph.Business.People;

namespace FamilyGraph.Application.People;

/// <summary>Adds and removes <see cref="ProfileImage"/> rows for changes (D-002, D-028). Reads go through <see cref="IPersonQueries"/>.</summary>
public interface IProfileImageStore
{
    Task<ProfileImage?> FindAsync(ProfileImageId id, CancellationToken cancellationToken);

    void Add(ProfileImage image);

    void Remove(ProfileImage image);
}
