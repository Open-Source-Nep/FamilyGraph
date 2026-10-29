/** A language this instance has translations for, from `GET /api/configuration` (D-047). */
export interface Language {
  /** The language tag, such as `en` or `ne`. */
  code: string;
  /** The language's own name for itself, such as "नेपाली". */
  name: string;
}

/** The language the Client is written in; it needs no translations. */
export const sourceLanguage = 'en';

/** Where the header's language switch remembers each browser's choice (D-047). */
export const languageStorageKey = 'familygraph.language';

let current = sourceLanguage;

/** The language the page runs in. It is chosen once at startup; switching reloads the page. */
export function currentLanguage(): string {
  return current;
}

/** Called once by the startup, before the app is imported. */
export function setCurrentLanguage(code: string): void {
  current = code;
}

/**
 * The browser's remembered choice when the instance still offers it, else the instance default,
 * else English.
 */
export function chooseLanguage(
  available: readonly Language[],
  instanceDefault: string | null,
  remembered: string | null,
): string {
  const offered = (code: string | null) =>
    code !== null && available.some((language) => language.code === code);
  if (offered(remembered)) {
    return remembered!;
  }
  if (offered(instanceDefault)) {
    return instanceDefault!;
  }
  return sourceLanguage;
}

/** Browser storage can be blocked, which throws as soon as it is touched. */
export function rememberedLanguage(storage?: Pick<Storage, 'getItem'>): string | null {
  try {
    return (storage ?? localStorage).getItem(languageStorageKey);
  } catch {
    return null;
  }
}

export function rememberLanguage(code: string, storage?: Pick<Storage, 'setItem'>): void {
  try {
    (storage ?? localStorage).setItem(languageStorageKey, code);
  } catch {
    // Without storage the choice lasts only for this page.
  }
}
