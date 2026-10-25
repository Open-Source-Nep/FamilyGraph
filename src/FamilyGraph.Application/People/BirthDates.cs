using FamilyGraph.Business.People;
using OSN.NepaliCalendar;

namespace FamilyGraph.Application.People;

/// <summary>
/// Builds a <see cref="BirthDate"/> from its parts (D-037). A BS date is checked and converted to
/// its earliest AD day with <c>OSN.NepaliCalendar</c>, which knows BS 1970 to 2090. Outside those
/// years only the parts' ranges can be checked, and the sort date is an estimate: a BS year starts
/// about 13 April of the AD year 57 years earlier.
/// </summary>
public static class BirthDates
{
    private const int BsYearOffset = 57;

    public static BirthDate Create(BirthCalendar calendar, int year, int? month, int? day)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        return calendar == BirthCalendar.Ad
            ? BirthDate.Ad(year, month, day)
            : BirthDate.Bs(year, month, day, BsSortDate(year, month, day));
    }

    private static DateOnly BsSortDate(int year, int? month, int? day)
    {
        if (year >= NepaliDate.MinValue.Year && year <= NepaliDate.MaxValue.Year)
        {
            if (month is < 1 or > 12 || !NepaliDate.IsValid(year, month ?? 1, day ?? 1))
            {
                throw new InvalidBirthDateException();
            }

            return NepaliDateConverter.ToGregorianDateOnly(new NepaliDate(year, month ?? 1, day ?? 1));
        }

        if (year - BsYearOffset is < 1 or > 9998 || month is < 1 or > 12 || day is < 1 or > BirthDate.MaxBsDay)
        {
            throw new InvalidBirthDateException();
        }

        return new DateOnly(year - BsYearOffset, 4, 13).AddMonths((month ?? 1) - 1).AddDays((day ?? 1) - 1);
    }
}
