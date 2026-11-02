import { TestBed } from '@angular/core/testing';

import { ConfigurationService } from '../config/configuration.service';
import { Culture, cultureStorageKey, setCurrentCulture } from './culture';
import { CultureSwitch } from './culture-switch';
import { languageStorageKey, setCurrentLanguage } from './language';

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

describe('CultureSwitch', () => {
  const reload = vi.fn();

  function render(available: Culture[], current: Culture, language: string) {
    setCurrentCulture(current);
    setCurrentLanguage(language);
    const configuration = () => ({
      google: { enabled: false, clientId: null },
      languages: { default: 'en', available: [] },
      cultures: { default: 'en', available },
    });
    TestBed.configureTestingModule({
      providers: [{ provide: ConfigurationService, useValue: { configuration } }],
    });
    const fixture = TestBed.createComponent(CultureSwitch);
    Object.defineProperty(fixture.componentInstance, 'location', { value: { reload } });
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  function choose(element: HTMLElement, code: string) {
    const select = element.querySelector('select')!;
    select.value = code;
    select.dispatchEvent(new Event('change'));
  }

  afterEach(() => {
    reload.mockReset();
    localStorage.removeItem(cultureStorageKey);
    localStorage.removeItem(languageStorageKey);
    setCurrentCulture(null);
    setCurrentLanguage('en');
  });

  it('names each culture in the current language (AC-08.32)', () => {
    const element = render([english, nepali], nepali, 'ne');

    const options = [...element.querySelectorAll('option')].map((option) =>
      option.textContent?.trim(),
    );
    expect(options).toEqual(['अङ्ग्रेजी', 'नेपाली']);
  });

  it('keeps the language when the new culture has it and reloads (AC-08.32)', () => {
    const element = render([english, nepali], english, 'en');

    choose(element, 'ne');

    expect(localStorage.getItem(cultureStorageKey)).toBe('ne');
    expect(localStorage.getItem(languageStorageKey)).toBe('en');
    expect(reload).toHaveBeenCalled();
  });

  it("switches to the culture's first language when it lacks the current one (AC-08.32)", () => {
    const element = render([english, nepali], nepali, 'ne');

    choose(element, 'en');

    expect(localStorage.getItem(cultureStorageKey)).toBe('en');
    expect(localStorage.getItem(languageStorageKey)).toBe('en');
  });

  it('is hidden when the instance offers one culture', () => {
    const element = render([english], english, 'en');

    expect(element.querySelector('select')).toBeNull();
  });
});
