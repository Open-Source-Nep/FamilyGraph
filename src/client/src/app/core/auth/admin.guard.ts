import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';

/** Sends users who are not administrators home. The server refuses them anyway. */
export const adminGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  await auth.load();
  return auth.isAdministrator() ? true : router.createUrlTree(['/']);
};
