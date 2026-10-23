using FamilyGraph.Application.People;
using FamilyGraph.Business.People;
using FamilyGraph.Infrastructure.Persistence;

namespace FamilyGraph.Infrastructure.People;

internal sealed class EfProfileImageStore(FamilyGraphDbContext context) : IProfileImageStore
{
    public async Task<ProfileImage?> FindAsync(ProfileImageId id, CancellationToken cancellationToken) =>
        await context.Set<ProfileImage>().FindAsync([id], cancellationToken);

    public void Add(ProfileImage image) => context.Add(image);

    public void Remove(ProfileImage image) => context.Remove(image);
}
