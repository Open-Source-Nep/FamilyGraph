using System.Reflection;

namespace FamilyGraph.Business;

/// <summary>
/// Base for a closed set of domain values, used instead of C# enums. Each value is a
/// <c>public static readonly</c> field of <typeparamref name="TEnum"/>, so no undefined value can
/// be constructed or cast. <see cref="Value"/> is stable and suitable for persistence.
/// </summary>
public abstract class BaseEnum<TEnum> : IEquatable<TEnum>
    where TEnum : BaseEnum<TEnum>
{
    private static readonly Lazy<IReadOnlyList<TEnum>> AllValues = new(() =>
        typeof(TEnum)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(field => field.IsInitOnly && field.FieldType == typeof(TEnum))
            .Select(field => (TEnum)field.GetValue(null)!)
            .ToList());

    protected BaseEnum(int value, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Value = value;
        Name = name;
    }

    public int Value { get; }

    public string Name { get; }

    /// <summary>Every value <typeparamref name="TEnum"/> declares, in declaration order.</summary>
    public static IReadOnlyList<TEnum> All => AllValues.Value;

    public static TEnum FromValue(int value) =>
        All.SingleOrDefault(item => item.Value == value)
        ?? throw new ArgumentOutOfRangeException(nameof(value), value, $"No {typeof(TEnum).Name} has this value.");

    public static TEnum FromName(string name) =>
        All.SingleOrDefault(item => item.Name == name)
        ?? throw new ArgumentOutOfRangeException(nameof(name), name, $"No {typeof(TEnum).Name} has this name.");

    public bool Equals(TEnum? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => obj is TEnum other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Name;

    public static bool operator ==(BaseEnum<TEnum>? left, BaseEnum<TEnum>? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(BaseEnum<TEnum>? left, BaseEnum<TEnum>? right) => !(left == right);
}
