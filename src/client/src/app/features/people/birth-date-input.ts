import { Component, computed, effect, input, output, signal, untracked } from '@angular/core';
import { AdDate, BsDate, isSupportedYear, toBs, todayBs } from '@opensourcenepal/nepali-calendar';
import { NepaliCalendarComponent } from '@opensourcenepal/nepali-calendar-angular';

import { DigitsPipe } from '../../core/i18n/local-format.pipes';
import {
  BirthCalendar,
  BirthDate,
  calendarScript,
  daysIn,
  isValidBirthDate,
  monthNames,
} from './birth-date';

/** How much of a birth date is known: the year, the year and month, or the full date (D-046). */
export type BirthDateKnown = 'year' | 'month' | 'day';

/**
 * An optional birth date in BS or AD: a year, and the month and day if known (D-037). It first
 * asks how much is known, and the Nepali calendar then picks a year, a month or a day; it reaches
 * BS 1970 to 2090 (AD 1913 to 2034), so older dates are typed (D-046). Emits the date, or null
 * while it is cleared, the year is empty or a part that is known is not chosen yet.
 */
@Component({
  selector: 'app-birth-date-input',
  imports: [DigitsPipe, NepaliCalendarComponent],
  templateUrl: './birth-date-input.html',
})
export class BirthDateInput {
  readonly initial = input<BirthDate | null>(null);
  readonly changed = output<BirthDate | null>();

  protected readonly open = signal(false);
  protected readonly calendar = signal<BirthCalendar>('Bs');
  protected readonly known = signal<BirthDateKnown>('day');
  protected readonly year = signal('');
  protected readonly month = signal<number | null>(null);
  protected readonly day = signal<number | null>(null);
  protected readonly picking = signal(false);
  protected readonly today = todayBs();
  protected readonly knownChoices: readonly { value: BirthDateKnown; label: string }[] = [
    { value: 'year', label: $localize`:@@birthDate.knownYear:Year only` },
    { value: 'month', label: $localize`:@@birthDate.knownMonth:Year and month` },
    { value: 'day', label: $localize`:@@birthDate.knownDay:Full date` },
  ];
  protected readonly script = calendarScript();

  protected readonly months = computed(() => monthNames(this.calendar()));
  protected readonly days = computed(() => {
    const year = Number(this.year());
    const month = this.month();
    return month === null || !Number.isInteger(year) || year < 1
      ? []
      : Array.from({ length: daysIn(this.calendar(), year, month) }, (_, index) => index + 1);
  });
  /** The date as typed, with only the known parts, or null while the year is empty. */
  protected readonly date = computed<BirthDate | null>(() =>
    this.open() && this.year().trim() !== ''
      ? {
          calendar: this.calendar(),
          year: Number(this.year()),
          month: this.known() === 'year' ? null : this.month(),
          day: this.known() === 'day' ? this.day() : null,
        }
      : null,
  );
  /** Which known parts are still to choose, or null when none are missing. */
  protected readonly missing = computed(() => {
    if (this.date() === null) {
      return null;
    }
    const month = this.known() !== 'year' && this.month() === null;
    const day = this.known() === 'day' && this.day() === null;
    if (month && day) {
      return $localize`:@@birthDate.missingMonthAndDay:Choose the month and day, or change what is known.`;
    }
    if (month) {
      return $localize`:@@birthDate.missingMonth:Choose the month, or change what is known.`;
    }
    if (day) {
      return $localize`:@@birthDate.missingDay:Choose the day, or change what is known.`;
    }
    return null;
  });
  /**
   * True while a known part is not chosen or the parts are not a date that exists; the parent does
   * not save then.
   */
  readonly invalid = computed(() => {
    const date = this.date();
    return date !== null && (this.missing() !== null || !isValidBirthDate(date));
  });
  /** The calendar can show the typed year, or no year is typed yet. */
  protected readonly canPick = computed(() => {
    const year = this.year().trim();
    if (year === '') {
      return true;
    }
    const value = Number(year);
    return this.calendar() === 'Bs' ? isSupportedYear(value) : value >= 1914 && value <= 2033;
  });
  /** The picker's value: always a BS date, the first day of the year or month when picking those. */
  protected readonly picked = computed<BsDate | null>(() => {
    const date = this.date();
    if (!date || this.invalid()) {
      return null;
    }
    try {
      const parts = { year: date.year, month: date.month ?? 1, day: date.day ?? 1 };
      return date.calendar === 'Bs' ? parts : toBs(parts);
    } catch {
      return null;
    }
  });

  /** The BS month the calendar opens at: the typed year and month, while a part is still missing too. */
  protected readonly shownMonth = computed<{ year: number; month: number } | null>(() => {
    const date = this.date();
    if (!date || !Number.isInteger(date.year) || !this.canPick()) {
      return null;
    }
    try {
      const parts = { year: date.year, month: date.month ?? 1, day: 1 };
      const bs = date.calendar === 'Bs' ? parts : toBs(parts);
      return { year: bs.year, month: bs.month };
    } catch {
      return null;
    }
  });

  constructor() {
    effect(() => {
      const initial = this.initial();
      untracked(() => {
        this.open.set(initial !== null);
        this.calendar.set(initial?.calendar ?? 'Bs');
        this.known.set(
          initial === null || initial.day !== null
            ? 'day'
            : initial.month !== null
              ? 'month'
              : 'year',
        );
        this.year.set(initial ? String(initial.year) : '');
        this.month.set(initial?.month ?? null);
        this.day.set(initial?.day ?? null);
        this.picking.set(false);
      });
    });
  }

  protected add(): void {
    this.open.set(true);
    this.emit();
  }

  protected remove(): void {
    this.open.set(false);
    this.year.set('');
    this.month.set(null);
    this.day.set(null);
    this.picking.set(false);
    this.emit();
  }

  protected setCalendar(calendar: BirthCalendar): void {
    this.calendar.set(calendar);
    this.emit();
  }

  protected setKnown(known: BirthDateKnown): void {
    this.known.set(known);
    this.emit();
  }

  protected setYear(year: string): void {
    this.year.set(year);
    this.emit();
  }

  protected setMonth(month: string): void {
    this.month.set(month === '' ? null : Number(month));
    this.emit();
  }

  protected setDay(day: string): void {
    this.day.set(day === '' ? null : Number(day));
    this.emit();
  }

  /**
   * The calendar gives both dates, the first day of the period when picking a year or month; keep
   * the known parts in the calendar the date is entered in.
   */
  protected pick({ bs, ad }: { bs: BsDate; ad: AdDate }): void {
    const date = this.calendar() === 'Bs' ? bs : ad;
    this.year.set(String(date.year));
    if (this.known() !== 'year') {
      this.month.set(date.month);
    }
    if (this.known() === 'day') {
      this.day.set(date.day);
    }
    this.picking.set(false);
    this.emit();
  }

  private emit(): void {
    this.changed.emit(this.invalid() ? null : this.date());
  }
}
