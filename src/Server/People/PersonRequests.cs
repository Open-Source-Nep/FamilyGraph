using System.ComponentModel.DataAnnotations;
using FamilyGraph.Business.People;

namespace Server.People;

/// <summary>
/// A person's name as the Client sends it. A missing first name is a domain rule (422,
/// <c>person.first_name_required</c>); only the length limit is checked here (400, D-017).
/// </summary>
public sealed class PersonNameRequest
{
    public const int MaxPartLength = 100;

    [StringLength(MaxPartLength)]
    public string? FirstName { get; init; }

    [StringLength(MaxPartLength)]
    public string? MiddleName { get; init; }

    [StringLength(MaxPartLength)]
    public string? LastName { get; init; }

    internal PersonName ToName() => new(FirstName ?? "", MiddleName, LastName);
}

/// <summary>A profile's new visibility: <c>Public</c> or <c>Private</c> (400 otherwise).</summary>
public sealed class VisibilityRequest
{
    [Required]
    [AllowedValues(nameof(ProfileVisibility.Public), nameof(ProfileVisibility.Private))]
    public string? Visibility { get; init; }

    internal ProfileVisibility ToVisibility() => ProfileVisibility.FromName(Visibility!);
}
