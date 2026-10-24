using System.ComponentModel.DataAnnotations;
using FamilyGraph.Business.People;
using GenderValue = FamilyGraph.Business.People.Gender;

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

/// <summary>
/// A new profile: its name and, optionally, its gender (<c>Male</c>, <c>Female</c> or <c>Other</c>;
/// 400 otherwise). Only the first name is required (PRD 00, D-030).
/// </summary>
public sealed class CreatePersonRequest
{
    [StringLength(PersonNameRequest.MaxPartLength)]
    public string? FirstName { get; init; }

    [StringLength(PersonNameRequest.MaxPartLength)]
    public string? MiddleName { get; init; }

    [StringLength(PersonNameRequest.MaxPartLength)]
    public string? LastName { get; init; }

    [AllowedValues(null, nameof(GenderValue.Male), nameof(GenderValue.Female), nameof(GenderValue.Other))]
    public string? Gender { get; init; }

    internal PersonName ToName() => new(FirstName ?? "", MiddleName, LastName);

    internal GenderValue? ToGender() => GenderRequest.Parse(Gender);
}

/// <summary>A profile's new gender: <c>Male</c>, <c>Female</c>, <c>Other</c>, or null to clear it (400 otherwise).</summary>
public sealed class GenderRequest
{
    [AllowedValues(null, nameof(GenderValue.Male), nameof(GenderValue.Female), nameof(GenderValue.Other))]
    public string? Gender { get; init; }

    internal GenderValue? ToGender() => Parse(Gender);

    internal static GenderValue? Parse(string? gender) => gender is null ? null : GenderValue.FromName(gender);
}

/// <summary>A profile's new visibility: <c>Public</c> or <c>Private</c> (400 otherwise).</summary>
public sealed class VisibilityRequest
{
    [Required]
    [AllowedValues(nameof(ProfileVisibility.Public), nameof(ProfileVisibility.Private))]
    public string? Visibility { get; init; }

    internal ProfileVisibility ToVisibility() => ProfileVisibility.FromName(Visibility!);
}
