import { Culture, chooseCulture, cultureName, languageAfterSwitch } from './culture';

const english: Culture = {
  code: 'en',
  name: { en: 'English', ne: 'अङ्ग्रेजी' },
  languages: ['en'],
};
const nepali: Culture = {
  code: 'ne',
  name: { ne: 'नेपाली', en: 'Nepali' },
  languages: ['ne', 'en'],
};

describe('culture', () => {
  it('chooses the remembered culture, else the instance default, else the first (AC-08.32)', () => {
    expect(chooseCulture([english, nepali], 'en', 'ne')).toBe(nepali);
    expect(chooseCulture([english, nepali], 'ne', 'gone')).toBe(nepali);
    expect(chooseCulture([english, nepali], 'gone', null)).toBe(english);
    expect(chooseCulture([], 'en', null)).toBeNull();
  });

  it('keeps the language after a switch when the culture has it, else takes its first (AC-08.32)', () => {
    expect(languageAfterSwitch(nepali, 'en')).toBe('en');
    expect(languageAfterSwitch(english, 'ne')).toBe('en');
    expect(languageAfterSwitch(nepali, 'fr')).toBe('ne');
  });

  it("names a culture in the current language, else in the culture's first", () => {
    expect(cultureName(english, 'ne')).toBe('अङ्ग्रेजी');
    expect(cultureName(nepali, 'fr')).toBe('नेपाली');
  });
});
