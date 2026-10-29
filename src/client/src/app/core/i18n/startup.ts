import { registerLocaleData } from '@angular/common';
import { loadTranslations } from '@angular/localize';

import { ClientConfiguration } from '../config/client-configuration';
import { chooseLanguage, rememberedLanguage, setCurrentLanguage, sourceLanguage } from './language';

/** What the app starts with: the instance settings and the language the page runs in. */
export interface Startup {
  configuration: ClientConfiguration | null;
  language: string;
  /** Angular's `LOCALE_ID`: the language when its locale data loaded, else Angular's `en-US`. */
  localeId: string;
}

/** The translations of one language, from `GET /api/translations/{code}`. */
interface TranslationFile {
  locale: string;
  translations: Record<string, string>;
}

/** Angular's locale data (plural rules, month names) for each translated language. */
const localeData: Record<string, () => Promise<{ default: unknown }>> = {
  ne: () => import('@angular/common/locales/ne'),
};

/**
 * Loads the instance settings and the chosen language's translations before the app is imported,
 * because `$localize` translates messages as their modules load (D-047). Anything that fails leaves
 * the app in English rather than not starting.
 */
export async function loadStartup(
  fetchJson: <T>(url: string) => Promise<T | null> = getJson,
  storage?: Pick<Storage, 'getItem'>,
): Promise<Startup> {
  const configuration = await fetchJson<ClientConfiguration>('/api/configuration');
  const languages = configuration?.languages;
  let localeId = 'en-US';
  let language = chooseLanguage(
    languages?.available ?? [],
    languages?.default ?? null,
    rememberedLanguage(storage),
  );

  if (language !== sourceLanguage) {
    const file = await fetchJson<TranslationFile>(
      `/api/translations/${encodeURIComponent(language)}`,
    );
    if (file) {
      loadTranslations(file.translations);
      const data = await localeData[language]?.().catch(() => null);
      if (data) {
        registerLocaleData(data.default, language);
        localeId = language;
      }
    } else {
      language = sourceLanguage;
    }
  }

  setCurrentLanguage(language);
  document.documentElement.lang = language;
  return { configuration, language, localeId };
}

async function getJson<T>(url: string): Promise<T | null> {
  try {
    const response = await fetch(url, { headers: { Accept: 'application/json' } });
    return response.ok ? ((await response.json()) as T) : null;
  } catch {
    return null;
  }
}
