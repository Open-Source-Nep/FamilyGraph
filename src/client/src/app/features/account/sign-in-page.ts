import { Component, computed, input } from '@angular/core';

import { signInUrl } from '../../core/auth/account';

@Component({
  selector: 'app-sign-in-page',
  templateUrl: './sign-in-page.html',
})
export class SignInPage {
  readonly returnUrl = input<string>();
  readonly failed = input<string>();

  protected readonly googleUrl = computed(() => signInUrl(this.returnUrl() || '/'));
}
