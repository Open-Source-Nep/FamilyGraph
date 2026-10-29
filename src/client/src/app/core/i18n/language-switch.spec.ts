import { TestBed } from '@angular/core/testing';

import { ConfigurationService } from '../config/configuration.service';
import { languageStorageKey } from './language';
import { LanguageSwitch } from './language-switch';

describe('LanguageSwitch', () => {
  const reload = vi.fn();

  function render(available: { code: string; name: string }[]) {
    const configuration = () => ({
      google: { enabled: false, clientId: null },
      languages: { default: 'en', available },
    });
    TestBed.configureTestingModule({
      providers: [{ provide: ConfigurationService, useValue: { configuration } }],
    });
    const fixture = TestBed.createComponent(LanguageSwitch);
    Object.defineProperty(fixture.componentInstance, 'location', { value: { reload } });
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  afterEach(() => {
    reload.mockReset();
    localStorage.removeItem(languageStorageKey);
  });

  it('lists each language in its own name (AC-09.15)', () => {
    const element = render([
      { code: 'en', name: 'English' },
      { code: 'ne', name: 'नेपाली' },
    ]);

    const options = [...element.querySelectorAll('option')].map((option) =>
      option.textContent?.trim(),
    );
    expect(options).toEqual(['English', 'नेपाली']);
  });

  it('remembers the choice in this browser and reloads the page (AC-09.15)', () => {
    const element = render([
      { code: 'en', name: 'English' },
      { code: 'ne', name: 'नेपाली' },
    ]);

    const select = element.querySelector('select')!;
    select.value = 'ne';
    select.dispatchEvent(new Event('change'));

    expect(localStorage.getItem(languageStorageKey)).toBe('ne');
    expect(reload).toHaveBeenCalled();
  });

  it('is hidden when the instance offers one language', () => {
    const element = render([{ code: 'en', name: 'English' }]);

    expect(element.querySelector('select')).toBeNull();
  });
});
