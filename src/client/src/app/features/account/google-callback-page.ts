import { HttpParams } from '@angular/common/http';
import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';

/**
 * Where Google sends the browser after sign-in. It hands Google's result to the API and moves on;
 * the server does every check. The URL is replaced so the one-time code leaves the history.
 */
@Component({
  selector: 'app-google-callback-page',
  template: '<p role="status">Signing you in…</p>',
})
export class GoogleCallbackPage implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  async ngOnInit(): Promise<void> {
    const googleResult = new HttpParams({ fromObject: this.route.snapshot.queryParams });
    let target: string;
    try {
      target = await this.auth.completeGoogleSignIn(googleResult);
    } catch {
      target = '/sign-in?failed=true';
    }
    await this.router.navigateByUrl(target, { replaceUrl: true });
  }
}
