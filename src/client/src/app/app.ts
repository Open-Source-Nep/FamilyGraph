import { Component, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { AuthService } from './core/auth/auth.service';
import { CultureSwitch } from './core/i18n/culture-switch';
import { LanguageSwitch } from './core/i18n/language-switch';

@Component({
  selector: 'app-root',
  imports: [CultureSwitch, LanguageSwitch, RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  private readonly auth = inject(AuthService);
  protected readonly account = this.auth.account;
  protected readonly isAdministrator = this.auth.isAdministrator;
  /** On narrow screens the links and switches fold behind a menu button (D-050). */
  protected readonly menuOpen = signal(false);
}
