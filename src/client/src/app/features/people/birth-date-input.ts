import { Component, computed, effect, input, output, signal, untracked } from '@angular/core';
import { AdDate, BsDate, isSupportedYear, toBs, todayBs } from '@opensourcenepal/nepali-calendar';
import { NepaliCalendarComponent } from '@opensourcenepal/nepali-calendar-angular';

import { BirthCalendar, BirthDate, daysIn, isValidBirthDate, monthNames } from './birth-date';

/**
 * An optional birth date in BS or AD: a year, and the month and day if known (D-037). A full date
 * can also be picked on the Nepali calendar, which reaches BS 1970 to 2090 (AD 1913 to 2034);
 * older dates are typed. Emits the date, or null while it is cleared or the year is empty.
 */
@Component({
  selector: 'app-birth-date-input',
  imports: [NepaliCalendarComponent],
  templateUrl: './birth-date-input.html',
})
export class BirthDateInput {
  readonly initial = input<BirthDate | null>(null);
  readonly changed = output<BirthDate | null>();

  protected readonly open = signal(false);
  protected readonly calendar = signal<BirthCalendar>('Bs');
  protected readonly year = signal('');
  protected readonly month = signal<number | null>(null);
  protected readonly day = signal<number | null>(null);
  protected readonly picking = signal(false);
  protected readonly today = todayBs();

  protected readonly months = computed(() => monthNames(this.calendar()));
  protected readonly days = computed(() => {
    const year = Number(this.year());
    const month = this.month();
    return month === null || !Number.isInteger(year) || year < 1
      ? []
      : Array.from({ length: daysIn(this.calendar(), year, month) }, (_, index) => index + 1);
  });
  /** The date as typed, or null while the year is empty. */
  protected readonly date = computed<BirthDate | null>(() =>
    this.open() && this.year().trim() !== ''
      ? {
          calendar: this.calendar(),
          year: Number(this.year()),
          month: this.month(),
          day: this.month() === null ? null : this.day(),
        }
      : null,
  );
  /** True while the typed parts are not a date that exists; the parent does not save then. */
  readonly invalid = computed(() => {
    const date = this.date();
    return date !== null && !isValidBirthDate(date);
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
  /** The picker's value: always a BS date. */
  protected readonly picked = computed<BsDate | null>(() => {
    const date = this.date();
    if (!date || date.day === null || this.invalid()) {
      return null;
    }
    try {
      const parts = { year: date.year, month: date.month!, day: date.day };
      return date.calendar === 'Bs' ? parts : toBs(parts);
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

  protected setYear(year: string): void {
    this.year.set(year);
    this.emit();
  }

  protected setMonth(month: string): void {
    this.month.set(month === '' ? null : Number(month));
    if (this.month() === null) {
      this.day.set(null);
    }
    this.emit();
  }

  protected setDay(day: string): void {
    this.day.set(day === '' ? null : Number(day));
    this.emit();
  }

  /** The calendar gives both dates; keep the one in the calendar the date is entered in. */
  protected pick({ bs, ad }: { bs: BsDate; ad: AdDate }): void {
    const date = this.calendar() === 'Bs' ? bs : ad;
    this.year.set(String(date.year));
    this.month.set(date.month);
    this.day.set(date.day);
    this.picking.set(false);
    this.emit();
  }

  private emit(): void {
    this.changed.emit(this.invalid() ? null : this.date());
  }
}
