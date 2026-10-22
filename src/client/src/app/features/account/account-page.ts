import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-account-page',
  templateUrl: './account-page.html',
})
export class AccountPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly account = this.auth.account;
  protected readonly isAdministrator = this.auth.isAdministrator;
  protected readonly signingOut = signal(false);

  protected async signOut(): Promise<void> {
    this.signingOut.set(true);
    try {
      await this.auth.signOut();
      await this.router.navigateByUrl('/sign-in');
    } finally {
      this.signingOut.set(false);
    }
  }
}
