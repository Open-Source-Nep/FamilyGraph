import { Component, computed, inject, input } from '@angular/core';

import { signInUrl } from '../../core/auth/account';
import { ConfigurationService } from '../../core/config/configuration.service';

/**
 * Signs users in with Google through the API. The button navigates to the server, which runs the
 * whole Google flow; this page never loads Google scripts or fonts.
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

  protected readonly unreachable = computed(() => this.configuration() === null);
  protected readonly googleEnabled = computed(() => this.configuration()?.google.enabled ?? false);
  protected readonly googleUrl = computed(() => signInUrl(this.returnUrl() || '/'));
}
