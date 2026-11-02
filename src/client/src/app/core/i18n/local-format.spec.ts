import { localDateTime, localDigits, localNumber } from './local-format';

describe('local formats', () => {
  it('writes numbers with Devanagari digits in Nepali (AC-09.16)', () => {
    expect(localNumber(2081, 'ne')).toBe('२०८१');
    expect(localNumber(12, 'en')).toBe('12');
  });

  it('replaces the digits in text (AC-09.16)', () => {
    expect(localDigits('15 Shrawan 2081', 'ne')).toBe('१५ Shrawan २०८१');
  });

  it('writes a moment with Devanagari digits in Nepali (AC-09.16)', () => {
    expect(localDateTime('2026-10-29T12:00:00Z', 'ne')).toMatch(/२०२६/);
    expect(localDateTime('2026-10-29T12:00:00Z', 'ne')).not.toMatch(/[0-9]/);
  });
});
