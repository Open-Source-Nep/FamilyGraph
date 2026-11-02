import { DOCUMENT } from '@angular/common';
import { Component, computed, inject } from '@angular/core';

import { ConfigurationService } from '../config/configuration.service';
import {
  Culture,
  cultureName,
  currentCulture,
  languageAfterSwitch,
  rememberCulture,
} from './culture';
import { currentLanguage, rememberLanguage } from './language';

/**
 * The header's culture switch for relationship labels (D-048). The choice is remembered in this
 * browser only. The language stays when the new culture has it, else becomes the culture's first
 * language, and the page reloads. Hidden with a single culture.
 */
@Component({
  selector: 'app-culture-switch',
  template: `
    @if (cultures().length > 1) {
      <select
        aria-label="Culture for relationship names"
        i18n-aria-label="@@header.culture"
        (change)="switchTo($any($event.target).value)"
      >
        @for (culture of cultures(); track culture.code) {
          <option [value]="culture.code" [selected]="culture.code === current?.code">
            {{ name(culture) }}
          </option>
        }
      </select>
    }
  `,
})
export class CultureSwitch {
  private readonly location = inject(DOCUMENT).location;
  private readonly configuration = inject(ConfigurationService).configuration;
  private readonly language = currentLanguage();

  protected readonly current = currentCulture();
  protected readonly cultures = computed(() => this.configuration()?.cultures?.available ?? []);

  protected name(culture: Culture): string {
    return cultureName(culture, this.language);
  }

  protected switchTo(code: string): void {
    const culture = this.cultures().find((offered) => offered.code === code);
    if (!culture || code === this.current?.code) {
      return;
    }
    rememberCulture(code);
    rememberLanguage(languageAfterSwitch(culture, this.language));
    this.location.reload();
  }
}
