using FamilyGraph.Business;
using FamilyGraph.Business.Handovers;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FamilyGraph.Infrastructure.Persistence;

/// <summary>Stores typed IDs as <c>uuid</c> and <see cref="BaseEnum{TEnum}"/> values by their integer <c>Value</c>.</summary>
internal static class ValueConverters
{
    public static readonly ValueConverter<PersonId, Guid> PersonId = new(id => id.Value, value => new PersonId(value));

    public static readonly ValueConverter<LinkId, Guid> LinkId = new(id => id.Value, value => new LinkId(value));

    public static readonly ValueConverter<HandoverId, Guid> HandoverId = new(id => id.Value, value => new HandoverId(value));

    public static readonly ValueConverter<ProfileImageId, Guid> ProfileImageId = new(id => id.Value, value => new ProfileImageId(value));

    public static readonly ValueConverter<AccountId, Guid> AccountId = new(id => id.Value, value => new AccountId(value));

    public static ValueConverter<TEnum, int> Enum<TEnum>()
        where TEnum : BaseEnum<TEnum> =>
        new(item => item.Value, value => BaseEnum<TEnum>.FromValue(value));
}
