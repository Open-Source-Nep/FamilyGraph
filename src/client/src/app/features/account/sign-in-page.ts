import { Component, computed, inject, input } from '@angular/core';

import { GoogleSignInService } from '../../core/auth/google-sign-in.service';
import { ConfigurationService } from '../../core/config/configuration.service';

/**
 * Signs users in with Google. The button sends the browser to Google's sign-in page with this
 * instance's public client ID; this page never loads Google scripts or fonts.
 */
@Component({
  selector: 'app-sign-in-page',
  templateUrl: './sign-in-page.html',
  styleUrl: './sign-in-page.css',
})
export class SignInPage {
  readonly returnUrl = input<string>();
  readonly failed = input<string>();

  private readonly configuration = inject(ConfigurationService).configuration;
  private readonly google = inject(GoogleSignInService);

  protected readonly unreachable = computed(() => this.configuration() === null);
  protected readonly clientId = computed(() => {
    const google = this.configuration()?.google;
    return google?.enabled ? google.clientId : null;
  });

  protected signInWithGoogle(clientId: string): Promise<void> {
    return this.google.start(clientId, this.returnUrl() || '/');
  }
}
