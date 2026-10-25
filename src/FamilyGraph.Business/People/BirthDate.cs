namespace FamilyGraph.Business.People;

/// <summary>
/// The calendar a birth date was entered in (D-037): Bikram Sambat or Gregorian. A date keeps its
/// own calendar, because a partial BS date such as a year alone is not one AD year.
/// </summary>
public sealed class BirthCalendar : BaseEnum<BirthCalendar>
{
    public static readonly BirthCalendar Bs = new(1, nameof(Bs));

    public static readonly BirthCalendar Ad = new(2, nameof(Ad));

    private BirthCalendar(int value, string name)
        : base(value, name)
    {
    }
}

/// <summary>
/// A person's birth date as entered (PRD 01, D-037): a year, optionally a month, and with a month
/// optionally a day, in <see cref="Calendar"/>. <see cref="SortDate"/> is the earliest Gregorian
/// day it can mean, used only to sort. Business cannot convert BS dates (it references nothing),
/// so the caller supplies the sort date for BS and Business checks only what it can: the parts'
/// ranges, and for AD that the day exists.
/// </summary>
public sealed record BirthDate
{
    public const int MaxBsDay = 32;

    private BirthDate(BirthCalendar calendar, int year, int? month, int? day, DateOnly sortDate)
    {
        Calendar = calendar;
        Year = year;
        Month = month;
        Day = day;
        SortDate = sortDate;
    }

    public BirthCalendar Calendar { get; }

    public int Year { get; }

    public int? Month { get; }

    public int? Day { get; }

    public DateOnly SortDate { get; }

    public bool IsFull => Day is not null;

    /// <summary>A Gregorian date; its sort date is its first day.</summary>
    public static BirthDate Ad(int year, int? month = null, int? day = null)
    {
        CheckParts(year, month, day);
        if (day is { } dayOfMonth && dayOfMonth > DateTime.DaysInMonth(year, month!.Value))
        {
            throw new InvalidBirthDateException();
        }

        return new BirthDate(BirthCalendar.Ad, year, month, day, new DateOnly(year, month ?? 1, day ?? 1));
    }

    /// <summary>A Bikram Sambat date with the earliest AD day it means, which the caller converted.</summary>
    public static BirthDate Bs(int year, int? month, int? day, DateOnly sortDate)
    {
        CheckParts(year, month, day);
        if (day > MaxBsDay)
        {
            throw new InvalidBirthDateException();
        }

        return new BirthDate(BirthCalendar.Bs, year, month, day, sortDate);
    }

    private static void CheckParts(int year, int? month, int? day)
    {
        if (year is < 1 or > 9999 || month is < 1 or > 12 || day < 1 || (day is not null && month is null))
        {
            throw new InvalidBirthDateException();
        }
    }
}

public sealed class InvalidBirthDateException()
    : DomainException("person.birth_date_invalid", "A birth date needs a year, may add a month, and with a month a day, and must exist.");
