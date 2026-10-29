import { DOCUMENT } from '@angular/common';
import { Component, computed, inject } from '@angular/core';

import { ConfigurationService } from '../config/configuration.service';
import { currentLanguage, rememberLanguage } from './language';

/**
 * The header's language switch (D-047). The choice is remembered in this browser only, and the
 * page reloads so that every text is loaded in the new language. Hidden with a single language.
 */
@Component({
  selector: 'app-language-switch',
  template: `
    @if (languages().length > 1) {
      <select
        aria-label="Language"
        i18n-aria-label="@@header.language"
        (change)="switchTo($any($event.target).value)"
      >
        @for (language of languages(); track language.code) {
          <option
            [value]="language.code"
            [selected]="language.code === current"
            [attr.lang]="language.code"
          >
            {{ language.name }}
          </option>
        }
      </select>
    }
  `,
})
export class LanguageSwitch {
  private readonly location = inject(DOCUMENT).location;
  private readonly configuration = inject(ConfigurationService).configuration;

  protected readonly current = currentLanguage();
  protected readonly languages = computed(() => this.configuration()?.languages.available ?? []);

  protected switchTo(code: string): void {
    if (code === this.current) {
      return;
    }
    rememberLanguage(code);
    this.location.reload();
  }
}
