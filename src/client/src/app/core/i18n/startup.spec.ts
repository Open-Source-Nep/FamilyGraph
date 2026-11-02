import { clearTranslations } from '@angular/localize';

import { currentCulture, setCurrentCulture } from './culture';
import { currentLanguage, setCurrentLanguage } from './language';
import { loadStartup } from './startup';

describe('loadStartup', () => {
  const configuration = {
    google: { enabled: false, clientId: null },
    languages: {
      default: 'ne',
      available: [
        { code: 'en', name: 'English' },
        { code: 'ne', name: 'नेपाली' },
      ],
    },
  };
  const remembered = (code: string | null) => ({ getItem: () => code });

  afterEach(() => {
    setCurrentCulture(null);
    clearTranslations();
    setCurrentLanguage('en');
    document.documentElement.lang = 'en';
  });

  function server(translations: unknown) {
    const requested: string[] = [];
    const fetchJson = <T>(url: string): Promise<T | null> => {
      requested.push(url);
      return Promise.resolve(
        (url === '/api/configuration' ? configuration : translations) as T | null,
      );
    };
    return { requested, fetchJson };
  }

  it('loads the default language and translates messages with it (AC-09.13, AC-09.14)', async () => {
    const { requested, fetchJson } = server({
      locale: 'ne',
      translations: { 'test.greeting': 'नमस्ते' },
    });

    const startup = await loadStartup(fetchJson, remembered(null));

    expect(requested).toEqual(['/api/configuration', '/api/translations/ne']);
    expect(startup).toEqual({ configuration, language: 'ne', localeId: 'ne' });
    expect(currentLanguage()).toBe('ne');
    expect(document.documentElement.lang).toBe('ne');
    expect($localize`:@@test.greeting:Hello`).toBe('नमस्ते');
  });

  it('needs no translations for English (AC-09.15)', async () => {
    const { requested, fetchJson } = server(null);

    const startup = await loadStartup(fetchJson, remembered('en'));

    expect(requested).toEqual(['/api/configuration']);
    expect(startup.language).toBe('en');
    expect($localize`:@@test.greeting:Hello`).toBe('Hello');
  });

  it('starts in English when the translations cannot be loaded', async () => {
    const { fetchJson } = server(null);

    const startup = await loadStartup(fetchJson, remembered(null));

    expect(startup).toEqual({ configuration, language: 'en', localeId: 'en-US' });
  });

  it('chooses the culture first and only its languages, else its first language (AC-08.32)', async () => {
    const english = { code: 'en', name: { en: 'English' }, languages: ['en'] };
    const nepali = { code: 'ne', name: { ne: 'नेपाली', en: 'Nepali' }, languages: ['ne', 'en'] };
    const withCultures = {
      ...configuration,
      cultures: { default: 'en', available: [english, nepali] },
    };
    const fetchJson = <T>(url: string): Promise<T | null> =>
      Promise.resolve((url === '/api/configuration' ? withCultures : null) as T | null);
    const storage = (culture: string | null, language: string | null) => ({
      getItem: (key: string) => (key === 'familygraph.culture' ? culture : language),
    });

    // The instance default language (ne) is not one of the English culture's languages.
    const startup = await loadStartup(fetchJson, storage(null, null));
    expect(currentCulture()).toEqual(english);
    expect(startup.language).toBe('en');

    // The remembered English stays with the Nepali culture.
    const kept = await loadStartup(fetchJson, storage('ne', 'en'));
    expect(currentCulture()).toEqual(nepali);
    expect(kept.language).toBe('en');
  });
});
