import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { localReturnUrl } from './account';
import { AuthService } from './auth.service';

/** Sends users who are already signed in from the sign-in page to where they were going. */
export const signedOutGuard: CanActivateFn = async (route) => {
  const router = inject(Router);
  try {
    const account = await inject(AuthService).load();
    return account ? router.parseUrl(localReturnUrl(route.queryParamMap.get('returnUrl'))) : true;
  } catch {
    // The page still shows, so the user can try again.
    return true;
  }
};
