import {
  chooseLanguage,
  languageStorageKey,
  rememberLanguage,
  rememberedLanguage,
} from './language';

describe('chooseLanguage', () => {
  const available = [
    { code: 'en', name: 'English' },
    { code: 'ne', name: 'नेपाली' },
  ];

  it('starts in the instance default without a remembered choice (AC-09.14)', () => {
    expect(chooseLanguage(available, 'ne', null)).toBe('ne');
  });

  it('prefers the language this browser remembers (AC-09.15)', () => {
    expect(chooseLanguage(available, 'ne', 'en')).toBe('en');
  });

  it('ignores a remembered language the instance no longer offers (AC-09.15)', () => {
    expect(chooseLanguage(available, 'ne', 'fr')).toBe('ne');
  });

  it('falls back to English when nothing else is offered', () => {
    expect(chooseLanguage([], null, 'ne')).toBe('en');
  });
});

describe('remembering the language', () => {
  it('keeps the choice in browser storage (AC-09.15)', () => {
    const values = new Map<string, string>();
    rememberLanguage('ne', { setItem: (key, value) => void values.set(key, value) });

    expect(values.get(languageStorageKey)).toBe('ne');
    expect(rememberedLanguage({ getItem: (key) => values.get(key) ?? null })).toBe('ne');
  });

  it('has no choice when storage is blocked', () => {
    const blocked = {
      getItem: () => {
        throw new DOMException('blocked', 'SecurityError');
      },
    };

    expect(rememberedLanguage(blocked)).toBeNull();
  });
});
