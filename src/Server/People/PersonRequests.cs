using System.ComponentModel.DataAnnotations;
using FamilyGraph.Application.People;
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
/// 400 otherwise), description and birth date with its switch. Only the first name is required
/// (PRD 00, D-030, D-037).
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

    public string? Description { get; init; }

    public BirthDateValue? BirthDate { get; init; }

    public bool BirthDateShown { get; init; }

    internal PersonName ToName() => new(FirstName ?? "", MiddleName, LastName);

    internal PersonDetails ToDetails() =>
        new(GenderRequest.Parse(Gender), Description, BirthDate?.ToBirthDate(), BirthDateShown);
}

/// <summary>A profile's new description, or null or blank to clear it (D-037).</summary>
public sealed class DescriptionRequest
{
    public string? Description { get; init; }
}

/// <summary>
/// A profile's birth date, or null to clear it, and whether people other than the maintainer and
/// administrators may see it (D-037).
/// </summary>
public sealed class BirthDateRequest
{
    public BirthDateValue? BirthDate { get; init; }

    public bool Shown { get; init; }
}

/// <summary>
/// A birth date as entered: <see cref="Calendar"/> <c>Bs</c> or <c>Ad</c> (400 otherwise), a year,
/// optionally a month, and with a month optionally a day. A date that does not exist is 422
/// <c>person.birth_date_invalid</c> (D-037).
/// </summary>
public sealed class BirthDateValue
{
    [Required]
    [AllowedValues(nameof(BirthCalendar.Bs), nameof(BirthCalendar.Ad))]
    public string? Calendar { get; init; }

    public int Year { get; init; }

    public int? Month { get; init; }

    public int? Day { get; init; }

    internal BirthDate ToBirthDate() => BirthDates.Create(BirthCalendar.FromName(Calendar!), Year, Month, Day);
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
