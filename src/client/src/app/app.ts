import { Component, inject } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';

import { AuthService } from './core/auth/auth.service';
import { LanguageSwitch } from './core/i18n/language-switch';

@Component({
  selector: 'app-root',
  imports: [LanguageSwitch, RouterLink, RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  private readonly auth = inject(AuthService);
  protected readonly account = this.auth.account;
  protected readonly isAdministrator = this.auth.isAdministrator;
}
