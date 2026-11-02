import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { GoogleSignInService } from '../../core/auth/google-sign-in.service';

/**
 * Where Google sends the browser after sign-in, with the ID token in the URL fragment. It checks
 * the answer belongs to the sign-in this browser started and hands the token to the API, which
 * checks it. The URL is replaced so the token leaves the history.
 */
@Component({
  selector: 'app-google-callback-page',
  template: '<p role="status" i18n="@@signIn.signingIn">Signing you in…</p>',
})
export class GoogleCallbackPage implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly google = inject(GoogleSignInService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  async ngOnInit(): Promise<void> {
    let target = '/sign-in?failed=true';
    try {
      const result = this.google.complete(this.route.snapshot.fragment);
      if (result) {
        await this.auth.completeGoogleSignIn(result.idToken, result.nonceSecret);
        target = result.returnUrl;
      }
    } catch {
      // Stays on the failure target.
    }
    await this.router.navigateByUrl(target, { replaceUrl: true });
  }
}
