import { currentLanguage } from './language';

/**
 * The first of each language's ten digits, where it does not write ASCII ones: Nepali always writes
 * Devanagari digits (D-047). Mapped here rather than through `Intl`, because some browsers carry no
 * locale data for `ne` and would fall back to ASCII digits.
 */
const zeroDigits: Record<string, number> = { ne: 0x0966 };

const asciiDigits = /[0-9]/g;

/** Replaces the ASCII digits in text, such as a date written as `2081-04-15`, with the language's. */
export function localDigits(text: string, language: string = currentLanguage()): string {
  const zero = zeroDigits[language];
  return zero === undefined
    ? text
    : text.replace(asciiDigits, (digit) => String.fromCharCode(zero + Number(digit)));
}

/**
 * A number in the page's language, without grouping: Devanagari digits (१२३) in Nepali (D-047).
 * Years and counts both read naturally without separators.
 */
export function localNumber(value: number, language: string = currentLanguage()): string {
  return localDigits(String(value), language);
}

/** A moment such as a link's expiry, as a date and time in the page's language. */
export function localDateTime(iso: string, language: string = currentLanguage()): string {
  const formatted = new Intl.DateTimeFormat(language, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(iso));
  return localDigits(formatted, language);
}
