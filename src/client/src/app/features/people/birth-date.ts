import {
  FULL_AD_MONTH_NAMES,
  daysInMonth,
  formatBs,
  isSupportedYear,
  isValidBsDate,
  monthName,
  toAd,
  toBs,
} from '@opensourcenepal/nepali-calendar';

/** The calendar a birth date was entered in: Bikram Sambat or Gregorian (D-037). */
export type BirthCalendar = 'Bs' | 'Ad';

/** A birth date as entered: a year, and the month and day if known (D-037). */
export interface BirthDate {
  calendar: BirthCalendar;
  year: number;
  month: number | null;
  day: number | null;
}

/** The month names of a calendar, first month first. */
export function monthNames(calendar: BirthCalendar): readonly string[] {
  return calendar === 'Bs'
    ? Array.from({ length: 12 }, (_, index) => monthName(index + 1, 'roman'))
    : FULL_AD_MONTH_NAMES.roman;
}

/** How many days the month has. BS months outside the conversion table may have up to 32. */
export function daysIn(calendar: BirthCalendar, year: number, month: number): number {
  if (calendar === 'Ad') {
    return new Date(Date.UTC(year, month, 0)).getUTCDate();
  }
  return isSupportedYear(year) ? daysInMonth(year, month) : 32;
}

/** Whether the parts make a date the server accepts. */
export function isValidBirthDate(date: BirthDate): boolean {
  const { calendar, year, month, day } = date;
  if (!Number.isInteger(year) || year < 1 || year > 9999) {
    return false;
  }
  if (month === null) {
    return day === null;
  }
  if (month < 1 || month > 12) {
    return false;
  }
  if (day === null) {
    return true;
  }
  if (calendar === 'Bs' && isSupportedYear(year)) {
    return isValidBsDate(year, month, day);
  }
  return day >= 1 && day <= daysIn(calendar, year, month);
}

/**
 * "1952", "March 1952" or "4 March 1952" in its own calendar. A full date also shows in the other
 * calendar where the conversion table reaches (BS 1970 to 2090); a partial one does not, because
 * it is not one date there (D-037).
 */
export function formatBirthDate(date: BirthDate): string {
  const own = formatOwn(date);
  const other = date.day !== null ? otherCalendar(date) : null;
  return other ? `${own} (${other})` : own;
}

function formatOwn({ calendar, year, month, day }: BirthDate): string {
  const suffix = calendar === 'Bs' ? ' BS' : '';
  const parts = [day, month !== null ? monthNames(calendar)[month - 1] : null, year];
  return parts.filter((part) => part !== null).join(' ') + suffix;
}

function otherCalendar({ calendar, year, month, day }: BirthDate): string | null {
  try {
    if (calendar === 'Bs') {
      const ad = toAd({ year, month: month!, day: day! });
      return formatOwn({ calendar: 'Ad', ...ad });
    }
    return `${formatBs(toBs({ year, month: month!, day: day! }), 'D', 'roman')} BS`;
  } catch {
    return null;
  }
}
