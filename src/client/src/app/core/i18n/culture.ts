/**
 * A culture for relationship labels, from `GET /api/configuration` (D-048). A culture exists only
 * to define labels such as Kaka or Mama, and lists the languages it is written in.
 */
export interface Culture {
  /** The culture's code, such as `ne`. */
  code: string;
  /** The culture's name in each of its languages, and possibly in others. */
  name: Record<string, string>;
  /** Its languages; the first applies when the current language is not one of them. */
  languages: string[];
}

/** Where the header's culture switch remembers each browser's choice (D-048). */
export const cultureStorageKey = 'familygraph.culture';

let current: Culture | null = null;

/** The culture the page runs with, chosen once at startup; switching reloads the page. */
export function currentCulture(): Culture | null {
  return current;
}

/** Called once by the startup, before the app is imported. */
export function setCurrentCulture(culture: Culture | null): void {
  current = culture;
}

/** The browser's remembered culture when still offered, else the instance default, else the first. */
export function chooseCulture(
  available: readonly Culture[],
  instanceDefault: string | null,
  remembered: string | null,
): Culture | null {
  return (
    available.find((culture) => culture.code === remembered) ??
    available.find((culture) => culture.code === instanceDefault) ??
    available[0] ??
    null
  );
}

/** The culture's name in `language`, else in the culture's first language. */
export function cultureName(culture: Culture, language: string): string {
  return culture.name[language] ?? culture.name[culture.languages[0]] ?? culture.code;
}

/**
 * The language to run in after switching to `culture`: the current one when the culture has it,
 * else the culture's first language (D-048).
 */
export function languageAfterSwitch(culture: Culture, language: string): string {
  return culture.languages.includes(language) ? language : culture.languages[0];
}

/** Browser storage can be blocked, which throws as soon as it is touched. */
export function rememberedCulture(storage?: Pick<Storage, 'getItem'>): string | null {
  try {
    return (storage ?? localStorage).getItem(cultureStorageKey);
  } catch {
    return null;
  }
}

export function rememberCulture(code: string, storage?: Pick<Storage, 'setItem'>): void {
  try {
    (storage ?? localStorage).setItem(cultureStorageKey, code);
  } catch {
    // Without storage the choice lasts only for this page.
  }
}
