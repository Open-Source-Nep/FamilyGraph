import { formatBirthDate, isValidBirthDate } from './birth-date';

describe('birth dates', () => {
  it('shows a partial date only in its own calendar', () => {
    expect(formatBirthDate({ calendar: 'Ad', year: 1890, month: null, day: null })).toBe('1890');
    expect(formatBirthDate({ calendar: 'Ad', year: 1952, month: 3, day: null })).toBe('March 1952');
    expect(formatBirthDate({ calendar: 'Bs', year: 2009, month: 1, day: null })).toBe(
      'Baisakh 2009 BS',
    );
  });

  it('shows a full date in both calendars where the table reaches', () => {
    expect(formatBirthDate({ calendar: 'Bs', year: 2082, month: 6, day: 16 })).toBe(
      '16 Ashwin 2082 BS (2 October 2025)',
    );
    expect(formatBirthDate({ calendar: 'Ad', year: 2025, month: 10, day: 2 })).toBe(
      '2 October 2025 (16 Ashwin 2082 BS)',
    );
    expect(formatBirthDate({ calendar: 'Ad', year: 1890, month: 1, day: 2 })).toBe(
      '2 January 1890',
    );
  });

  it('checks that the date exists', () => {
    expect(isValidBirthDate({ calendar: 'Ad', year: 1951, month: 2, day: 29 })).toBe(false);
    expect(isValidBirthDate({ calendar: 'Bs', year: 2081, month: 1, day: 32 })).toBe(false);
    expect(isValidBirthDate({ calendar: 'Bs', year: 1900, month: 1, day: 32 })).toBe(true);
    expect(isValidBirthDate({ calendar: 'Ad', year: 0, month: null, day: null })).toBe(false);
  });
});
