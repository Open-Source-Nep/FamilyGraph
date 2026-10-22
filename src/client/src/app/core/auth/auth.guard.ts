import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';

/** Sends signed-out users to the sign-in page. The graph is visible only to signed-in users. */
export const authGuard: CanActivateFn = async (_route, state) => {
  const router = inject(Router);
  const account = await inject(AuthService).load();
  return account
    ? true
    : router.createUrlTree(['/sign-in'], { queryParams: { returnUrl: state.url } });
};
